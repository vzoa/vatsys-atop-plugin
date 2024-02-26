using System;
using System.Collections.Generic;
using System.Linq;
using AtopPlugin.Models;
using vatsys;

namespace AtopPlugin.Conflict;

public static class ConflictProbe
{
    public static Conflicts Probe(FDP2.FDR fdr)
    {
        if (!MMI.IsMySectorConcerned(fdr)) return EmptyConflicts();

        var discoveredConflicts = new List<ConflictData>();

        var block1 = AltitudeBlock.ExtractAltitudeBlock(fdr);
        foreach (var fdr2 in FDP2.GetFDRs.Where(fdr2 =>
                     fdr2 != null && fdr.Callsign != fdr2.Callsign && MMI.IsMySectorConcerned(fdr2)))
        {
            var data = new ConflictData
            {
                Fdr2 = fdr2
            };
            var rte = fdr.ParsedRoute;
            var rte2 = fdr2.ParsedRoute;
            var trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong,
                rte.Last().Intersection.LatLong);
            var trk2 = Conversions.CalculateTrack(rte2.First().Intersection.LatLong,
                rte2.Last().Intersection.LatLong);
            data.TrkAngle = Math.Abs(trk2 - trk);
            var sameDir = data.TrkAngle < 45;
            var crossing = (data.TrkAngle >= 45 && data.TrkAngle <= 135) ||
                           (data.TrkAngle >= 315 && data.TrkAngle <= 225);
            var oppoDir = data.TrkAngle > 135 && data.TrkAngle < 225;
            var block2 = AltitudeBlock.ExtractAltitudeBlock(fdr2);

            data.VerticalAct = AltitudeBlock.Difference(block1, block2);
            data.VerticalSep = MinimaCalculator.GetVerticalMinima(fdr, fdr2);

            if (data.VerticalAct >= data.VerticalSep) continue;

            data.LatSep = MinimaCalculator.GetLateralMinima(fdr, fdr2);

            // TODO(msalikhov): figure out what this was trying to do - it had no effect
            // else if (data.latSep != 100 && data.latSep == 100) ;
            // {
            //     data.latSep = (50 + 100) / 2;
            // }

            var conflictSegments1 = ConflictAreaCalculator.CalculateAreaOfConflict(fdr, fdr2, data.LatSep);
            var conflictSegments2 = ConflictAreaCalculator.CalculateAreaOfConflict(fdr2, fdr, data.LatSep);

            conflictSegments1.Sort((s, t) => s.StartTime.CompareTo(t.StartTime)); //sort by first conflict time
            conflictSegments2.Sort((s, t) => s.StartTime.CompareTo(t.StartTime)); //sort by first conflict time

            var firstConflictTime = conflictSegments1.FirstOrDefault();
            var firstConflictTime2 = conflictSegments2.FirstOrDefault();
            var failedLateral = conflictSegments1.Count > 0;
            if (firstConflictTime == null || firstConflictTime2 == null) continue;

            data.LongTimesep = MinimaCalculator.GetLongitudinalTime(fdr, fdr2);
            data.LongTimeact = (firstConflictTime2.StartTime - firstConflictTime.StartTime).Duration();
            data.LongDistsep = MinimaCalculator.GetLongitudinalDistance(fdr, fdr2);
            data.LongDistact = Conversions.CalculateDistance(firstConflictTime.StartLatlong,
                firstConflictTime2.StartLatlong);
            data.TimeLongsame = sameDir && failedLateral && firstConflictTime.EndTime > DateTime.UtcNow
                                && data.LongTimeact <
                                data.LongTimesep; //check time based longitudinal for same direction                    
            data.TimeLongcross = crossing && failedLateral && firstConflictTime.EndTime > DateTime.UtcNow
                                 && (firstConflictTime2.StartTime - firstConflictTime.StartTime)
                                 .Duration() < new TimeSpan(0, 0, 15, 0);
            data.DistLongsame = sameDir && failedLateral && firstConflictTime.EndTime > DateTime.UtcNow
                                && data.LongDistact < data.LongDistsep;

            data.TimeLongopposite = false;

            if (failedLateral && oppoDir)
                try
                {
                    data.Top = new TimeOfPassing(fdr, fdr2);
                    data.TimeLongopposite = data.Top.Time > DateTime.UtcNow
                                            && data.Top.Time.Add(data.LongTimesep) > DateTime.UtcNow &&
                                            data.Top.Time.Subtract(data.LongTimesep) < DateTime.UtcNow;
                }
                catch (Exception)
                {
                    // ignored - we were unable to calculate time of passing for some reason
                }

            data.LongType = data.LongDistsep == null
                ? data.TimeLongsame
                : data.DistLongsame;

            var lossOfSep = data.LongType || data.TimeLongcross || data.TimeLongopposite;
            data.ConflictType = lossOfSep && sameDir ? ConflictType.SameDirection :
                lossOfSep && crossing ? ConflictType.Crossing :
                lossOfSep && oppoDir ? ConflictType.OppositeDirection : null;
            data.EarliestLos = failedLateral && oppoDir
                ? data.Top?.Time.Subtract(new TimeSpan(0, 0, 10, 0)) ?? DateTime.MaxValue
                : DateTime.Compare(firstConflictTime.StartTime, firstConflictTime2.StartTime) < 0
                    ? firstConflictTime.StartTime
                    : firstConflictTime2.StartTime;

            var actual = failedLateral && oppoDir && data.VerticalAct < data.VerticalSep
                ? new TimeSpan(0, 0, 1, 0, 0) >= data.EarliestLos.Subtract(DateTime.UtcNow).Duration()
                : (lossOfSep && new TimeSpan(0, 0, 1, 0, 0) >=
                      data.EarliestLos.Subtract(DateTime.UtcNow).Duration()) ||
                  data.EarliestLos < DateTime.UtcNow;

            var imminent = failedLateral && oppoDir && data.VerticalAct < data.VerticalSep
                ? new TimeSpan(0, 0, 30, 0, 0) >= data.EarliestLos.Subtract(DateTime.UtcNow).Duration()
                : lossOfSep && new TimeSpan(0, 0, 30, 0, 0) >=
                data.EarliestLos.Subtract(DateTime.UtcNow).Duration(); //check if timediff < 30 min

            var advisory = failedLateral && oppoDir && data.VerticalAct < data.VerticalSep
                ? new TimeSpan(0, 2, 0, 0, 0) > data.EarliestLos.Subtract(DateTime.UtcNow).Duration()
                  && data.EarliestLos.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0)
                : lossOfSep && new TimeSpan(0, 2, 0, 0, 0) >
                            data.EarliestLos.Subtract(DateTime.UtcNow).Duration()
                            && data.EarliestLos.Subtract(DateTime.UtcNow).Duration() >=
                            new TimeSpan(0, 0, 30, 0, 0); //check if  2 hours > timediff > 30 mins

            data.ConflictStatus = ConflictStatusUtils.From(actual, imminent, advisory);

            if (data.ConflictStatus != ConflictStatus.None) discoveredConflicts.Add(data);
        }

        return GroupConflicts(discoveredConflicts);
    }

    private static Conflicts EmptyConflicts()
    {
        return new Conflicts(new List<ConflictData>(), new List<ConflictData>(), new List<ConflictData>());
    }

    private static Conflicts GroupConflicts(List<ConflictData> allConflicts)
    {
        var actual = new List<ConflictData>();
        var imminent = new List<ConflictData>();
        var advisory = new List<ConflictData>();

        foreach (var conflict in allConflicts)
        {
            if (conflict.ConflictStatus == ConflictStatus.Actual) actual.Add(conflict);
            if (conflict.ConflictStatus == ConflictStatus.Imminent) imminent.Add(conflict);
            if (conflict.ConflictStatus == ConflictStatus.Advisory) advisory.Add(conflict);
        }

        return new Conflicts(actual, imminent, advisory);
    }

    public record struct Conflicts(
        List<ConflictData> ActualConflicts,
        List<ConflictData> ImminentConflicts,
        List<ConflictData> AdvisoryConflicts);

    public record ConflictData
    {
        public ConflictStatus ConflictStatus;
        public ConflictType? ConflictType;
        public bool DistLongsame;
        public DateTime EarliestLos;
        public FDP2.FDR Fdr2;
        public int LatSep;
        public double LongDistact;
        public int? LongDistsep;
        public TimeSpan LongTimeact;
        public TimeSpan LongTimesep;
        public bool LongType;
        public bool TimeLongcross;
        public bool TimeLongopposite;
        public bool TimeLongsame;
        public TimeOfPassing? Top;
        public double TrkAngle;
        public int VerticalAct;
        public int VerticalSep;
    }
}