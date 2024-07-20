using System;
using System.Collections.Generic;
using System.Linq;
using AtopPlugin.Models;
using vatsys;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static vatsys.FDP2;

namespace AtopPlugin.Conflict;

public static class ConflictProbe
{
    public static List<ConflictData> ConflictDatas { get; set; } = new List<ConflictData>();

    public static event EventHandler ConflictsUpdated;
    public static Conflicts Probe(FDP2.FDR fdr)
    {
        if (!MMI.IsMySectorConcerned(fdr)) return EmptyConflicts();

        var discoveredConflicts = new List<ConflictData>();

        var block1 = AltitudeBlock.ExtractAltitudeBlock(fdr);
        foreach (var fdr2 in FDP2.GetFDRs.Where(fdr2 =>
                     fdr2 != null && fdr.Callsign != fdr2.Callsign && MMI.IsMySectorConcerned(fdr2)))
        {
            var data = new ConflictData();

            data.Active = fdr2;
            data.Intruder = fdr;

            var rte = fdr.ParsedRoute;
            var rte2 = fdr2.ParsedRoute;
            for (var p = 0; p < rte.Count + 1; p++)
            {
                var trk = Conversions.CalculateTrack(rte[p].Intersection.LatLong,         //check exception out of range
                rte[p + 1].Intersection.LatLong);

                for (var p2 = 0; p2 < rte2.Count + 1; p2++)
                {
                    var trk2 = Conversions.CalculateTrack(rte[p2].Intersection.LatLong,
                    rte[p2 + 1].Intersection.LatLong);
                    data.TrkAngle = Math.Abs(trk2 - trk);
                    var sameDir = data.TrkAngle < 45;
                    var crossing = (data.TrkAngle >= 45 && data.TrkAngle <= 135) ||
                                   (data.TrkAngle >= 315 && data.TrkAngle <= 225);
                    var oppoDir = data.TrkAngle > 135 && data.TrkAngle < 225;
                    var block2 = AltitudeBlock.ExtractAltitudeBlock(fdr2);
                    var fdr1StartTime = fdr.ATD;
                    var fdr2StartTime = fdr2.ATD;
                    var fdr1EndTime = rte.Last().ETO;
                    var fdr2EndTime = rte2.Last().ETO;
                    data.VerticalAct = AltitudeBlock.Difference(block1, block2);
                    data.VerticalSep = MinimaCalculator.Instance.GetVerticalMinima(fdr, fdr2);
                    var maxAltFilter = data.VerticalAct > PacificMinimaDelegate.Above600Vertical;


                    ///Coarse Filtering

                    //Temporal Test
                    if ((fdr1StartTime - fdr2EndTime).Duration() < TimeSpan.FromMinutes(0)
                        || (fdr2StartTime - fdr1EndTime).Duration() < TimeSpan.FromMinutes(0)) continue;

                    //Vertical Test
                    if (maxAltFilter) continue;

                    //Graphical Test
                    if (LateralConflictCalculator.CalculateRectangleOverlap(fdr, fdr2)) continue;


                    ///Detailed Filtering
                    //Vertical Test
                    if (data.VerticalAct >= data.VerticalSep) continue;

                    //Lateral Test
                    data.LatSep = MinimaCalculator.Instance.GetLateralMinima(fdr, fdr2);

                    var conflictSegments1 = LateralConflictCalculator.CalculateAreaOfConflict(fdr, fdr2, data.LatSep);
                    var conflictSegments2 = LateralConflictCalculator.CalculateAreaOfConflict(fdr2, fdr, data.LatSep);

                    conflictSegments1.Sort((s, t) => s.StartTime.CompareTo(t.StartTime)); //sort by first conflict time
                    conflictSegments2.Sort((s, t) => s.StartTime.CompareTo(t.StartTime)); //sort by first conflict time

                    var firstConflictTime = conflictSegments1.FirstOrDefault();
                    var firstConflictTime2 = conflictSegments2.FirstOrDefault();
                    var failedLateral = conflictSegments1.Count > 0;
                    if (firstConflictTime == null || firstConflictTime2 == null || !failedLateral) continue;

                    //Longitudinal Test
                    data.LongTimesep = MinimaCalculator.Instance.GetLongitudinalTimeMinima(fdr, fdr2);
                    data.LongTimeact = (firstConflictTime2.StartTime - firstConflictTime.StartTime).Duration();
                    data.LongDistsep = MinimaCalculator.Instance.GetLongitudinalDistanceMinima(fdr, fdr2);
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

                    if (!lossOfSep) continue;

                    data.ConflictType = sameDir ? ConflictType.SameDirection :
                        crossing ? ConflictType.Crossing :
                        oppoDir ? ConflictType.OppositeDirection : null;

                    data.EarliestLos = oppoDir
                        ? data.Top?.Time.Subtract(new TimeSpan(0, 0, 15, 0)) ?? DateTime.MaxValue
                        : DateTime.Compare(firstConflictTime.StartTime, firstConflictTime2.StartTime) < 0
                            ? firstConflictTime2.StartTime
                            : firstConflictTime.StartTime;

                    data.LatestLos = oppoDir
                        ? data.Top?.Time.Add(new TimeSpan(0, 0, 15, 0)) ?? DateTime.MaxValue
                        : DateTime.Compare(firstConflictTime.StartTime, firstConflictTime2.StartTime) < 0
                            ? firstConflictTime.StartTime
                            : firstConflictTime2.StartTime;

                    data.ConflictEnd = oppoDir
                        ? data.Top?.Time.Add(new TimeSpan(0, 0, 15, 1)) ?? DateTime.MaxValue
                        : DateTime.Compare(firstConflictTime.EndTime, firstConflictTime2.EndTime) < 0
                            ? firstConflictTime2.EndTime
                            : firstConflictTime.EndTime;

                    var actual = oppoDir && data.VerticalAct < data.VerticalSep
                        ? new TimeSpan(0, 0, 1, 0, 0) >= data.EarliestLos.Subtract(DateTime.UtcNow).Duration()
                        : (lossOfSep && new TimeSpan(0, 0, 1, 0, 0) >=
                              data.EarliestLos.Subtract(DateTime.UtcNow).Duration()) ||
                          data.EarliestLos < DateTime.UtcNow;

                    var imminent = oppoDir && data.VerticalAct < data.VerticalSep
                        ? new TimeSpan(0, 0, 30, 0, 0) >= data.EarliestLos.Subtract(DateTime.UtcNow).Duration()
                        : lossOfSep && new TimeSpan(0, 0, 30, 0, 0) >=
                        data.EarliestLos.Subtract(DateTime.UtcNow).Duration(); //check if timediff < 30 min

                    var advisory = oppoDir && data.VerticalAct < data.VerticalSep
                        ? new TimeSpan(0, 2, 0, 0, 0) > data.EarliestLos.Subtract(DateTime.UtcNow).Duration()
                          && data.EarliestLos.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0)
                        : lossOfSep && new TimeSpan(0, 2, 0, 0, 0) >
                                    data.EarliestLos.Subtract(DateTime.UtcNow).Duration()
                                    && data.EarliestLos.Subtract(DateTime.UtcNow).Duration() >=
                                    new TimeSpan(0, 0, 30, 0, 0); //check if  2 hours > timediff > 30 mins

                    data.ConflictStatus = ConflictStatusUtils.From(actual, imminent, advisory);

                    if (data.ConflictStatus != ConflictStatus.None) discoveredConflicts.Add(data);

                    discoveredConflicts.Add(new ConflictData(
                    data.ConflictStatus,
                    data.ConflictType,
                    data.EarliestLos,
                    data.LatestLos,
                    data.ConflictEnd,
                    data.Intruder,
                    data.Active,
                    data.LatSep,
                    data.LongDistact,
                    data.LongDistsep,
                    data.LongTimeact,
                    data.LongTimesep,
                    data.LongType,
                    data.TimeLongcross,
                    data.TimeLongsame,
                    data.Top,
                    data.TrkAngle,
                    data.VerticalSep,
                    data.VerticalAct));

                    ConflictDatas = discoveredConflicts;     //update the list
                    ConflictsUpdated?.Invoke(null, new EventArgs());
                }
            }
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

}