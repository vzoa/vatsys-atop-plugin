// Decompiled with JetBrains decompiler
// Type: vatsys.LATC
// Assembly: vatSys, Version=0.4.8114.34539, Culture=neutral, PublicKeyToken=null
// MVID: E82FB2F8-DAB0-42FD-91AA-1C44F8E62564
// Assembly location: E:\vatsys\bin\vatSys.exe
// XML documentation location: E:\vatsys\bin\vatSys.xml

using System;
using System.Collections.Generic;
using System.Linq;
using AuroraLabelItemsPlugin;

namespace vatsys
{
    public class CPAR
    {
        private readonly GEO label = new GEO();
        public List<Segment> Segments1 = new List<Segment>();
        public List<Segment> Segments2 = new List<Segment>();
        public DateTime Timeout = DateTime.MaxValue;

        public CPAR(FDP2.FDR fdr1, FDP2.FDR fdr2, int value)
        {
            CalculateLATC(fdr1, fdr2, value);
        }

        public CPAR()
        {
        }

        private void CalculateLATC(FDP2.FDR fdr1, FDP2.FDR fdr2, int value)
        {
            if (fdr1 == null || fdr2 == null)
                return;
            Segments1.AddRange(CalculateAreaOfConflict(fdr1, fdr2, value));
            Segments2.AddRange(CalculateAreaOfConflict(fdr2, fdr1, value));
        }

        private static List<Segment> CalculateAreaOfConflict(FDP2.FDR fdr1, FDP2.FDR fdr2, int value)
        {
            var segs = new List<Segment>();
            var route1waypoints = fdr1.ParsedRoute.ToList()
                .Where(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT).ToList();
            var route2waypoints = fdr2.ParsedRoute.ToList()
                .Where(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT).ToList();
            for (var wp1index = 1; wp1index < route1waypoints.Count; ++wp1index)
            {
                var route1Segment = CreatePolygon(route1waypoints[wp1index - 1].Intersection.LatLong,
                    route1waypoints[wp1index].Intersection.LatLong, value);
                for (var wp2index = 1; wp2index < route2waypoints.Count; wp2index++)
                {
                    var source = new List<Coordinate>();
                    var intersectionPoints = new List<Coordinate>();
                    source.AddRange(CalculatePolygonIntersections(route1Segment,
                        route2waypoints[wp2index - 1].Intersection.LatLong,
                        route2waypoints[wp2index].Intersection.LatLong));
                    var num1 = 0;
                    var num2 = 0;
                    foreach (var coordinate in source.ToList())
                        if (Conversions.IsLatLonOnGC(route2waypoints[wp2index - 1].Intersection.LatLong,
                                route2waypoints[wp2index].Intersection.LatLong, coordinate))
                        {
                            intersectionPoints.Add(coordinate);
                        }
                        else
                        {
                            var track = Conversions.CalculateTrack(route2waypoints[wp2index - 1].Intersection.LatLong,
                                route2waypoints[wp2index].Intersection.LatLong);
                            if (Math.Abs(track -
                                         Conversions.CalculateTrack(route2waypoints[wp2index - 1].Intersection.LatLong,
                                             coordinate)) > 90.0)
                                ++num1;
                            if (Math.Abs(track - Conversions.CalculateTrack(coordinate,
                                    route2waypoints[wp2index].Intersection.LatLong)) > 90.0)
                                ++num2;
                        }

                    if (num1 % 2 != 0 && num2 % 2 != 0)
                    {
                        intersectionPoints.Clear();
                        intersectionPoints.Add(route2waypoints[wp2index - 1].Intersection.LatLong);
                        intersectionPoints.Add(route2waypoints[wp2index].Intersection.LatLong);
                    }
                    else if (num2 % 2 != 0)
                    {
                        intersectionPoints.Add(route2waypoints[wp2index].Intersection.LatLong);
                    }
                    else if (num1 % 2 != 0)
                    {
                        intersectionPoints.Add(route2waypoints[wp2index - 1].Intersection.LatLong);
                    }

                    intersectionPoints.Sort((x, y) =>
                        Conversions.CalculateDistance(route2waypoints[wp2index - 1].Intersection.LatLong, x)
                            .CompareTo(Conversions.CalculateDistance(route2waypoints[wp2index - 1].Intersection.LatLong,
                                y)));
                    for (var ipIndex = 1; ipIndex < intersectionPoints.Count; ipIndex += 2)
                    {
                        var seg = new Segment();
                        seg.startLatlong = intersectionPoints[ipIndex - 1];
                        seg.endLatlong = intersectionPoints[ipIndex];
                        var conflictSegments = segs.Where(s => s.routeSegment == route2waypoints[wp2index]).Where(s =>
                            (Conversions.CalculateDistance(s.startLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) <
                             Conversions.CalculateDistance(seg.startLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) &&
                             Conversions.CalculateDistance(s.endLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) >
                             Conversions.CalculateDistance(seg.startLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong)) ||
                            (Conversions.CalculateDistance(s.endLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) >
                             Conversions.CalculateDistance(seg.endLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) &&
                             Conversions.CalculateDistance(s.startLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) <
                             Conversions.CalculateDistance(seg.endLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong)) ||
                            (Conversions.CalculateDistance(s.startLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) >
                             Conversions.CalculateDistance(seg.startLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) &&
                             Conversions.CalculateDistance(s.endLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong) <
                             Conversions.CalculateDistance(seg.endLatlong,
                                 route2waypoints[wp2index - 1].Intersection.LatLong)) ||
                            Conversions.CalculateDistance(s.startLatlong, seg.startLatlong) < 0.01 ||
                            Conversions.CalculateDistance(s.endLatlong, seg.endLatlong) < 0.01).ToList();
                        if (conflictSegments.Count > 0)
                        {
                            foreach (var segment in conflictSegments)
                            {
                                if (Conversions.CalculateDistance(segment.endLatlong,
                                        route2waypoints[wp2index - 1].Intersection.LatLong) <
                                    Conversions.CalculateDistance(seg.endLatlong,
                                        route2waypoints[wp2index - 1].Intersection.LatLong))
                                    segment.endLatlong = seg.endLatlong;
                                if (Conversions.CalculateDistance(seg.startLatlong,
                                        route2waypoints[wp2index - 1].Intersection.LatLong) <
                                    Conversions.CalculateDistance(segment.startLatlong,
                                        route2waypoints[wp2index - 1].Intersection.LatLong))
                                    segment.startLatlong = seg.startLatlong;
                            }
                        }
                        else
                        {
                            seg.callsign = fdr2.Callsign;
                            seg.routeSegment = route2waypoints[wp2index];
                            segs.Add(seg);
                        }
                    }
                }
            }

            for (var i = 0; i < segs.Count; i++)
            {
                if (!segs.Exists(s => Conversions.CalculateDistance(segs[i].startLatlong, s.endLatlong) < 0.01))
                    segs[i].startTime =
                        FDP2.GetSystemEstimateAtPosition(fdr2, segs[i].startLatlong, segs[i].routeSegment);
                if (!segs.Exists(s => Conversions.CalculateDistance(segs[i].endLatlong, s.startLatlong) < 0.01))
                    segs[i].endTime = FDP2.GetSystemEstimateAtPosition(fdr2, segs[i].endLatlong, segs[i].routeSegment);
            }

            return segs;
        }

        private static List<Coordinate> CreatePolygon(Coordinate point1, Coordinate point2, int value)
        {
            var polygon = new List<Coordinate>();
            var track = Conversions.CalculateTrack(point1, point2);
            var num1 = track - 90.0;
            for (var index = 0; index <= 180; index += 10)
            {
                var heading = num1 - index;
                var fromBearingRange = Conversions.CalculateLLFromBearingRange(point1, value, heading);
                polygon.Add(fromBearingRange);
            }

            var num2 = track + 90.0;
            for (var index = 0; index <= 180; index += 10)
            {
                var heading = num2 - index;
                var fromBearingRange = Conversions.CalculateLLFromBearingRange(point2, value, heading);
                polygon.Add(fromBearingRange);
            }

            polygon.Add(polygon[0]);
            return polygon;
        }

        private static List<Coordinate> CalculatePolygonIntersections(
            List<Coordinate> polygon,
            Coordinate point1,
            Coordinate point2)
        {
            var polygonIntersections = new List<Coordinate>();
            for (var index = 1; index < polygon.Count; ++index)
            {
                var gcIntersectionLl =
                    Conversions.CalculateAllGCIntersectionLL(polygon[index - 1], polygon[index], point1, point2);
                if (gcIntersectionLl != null)
                    polygonIntersections.AddRange(gcIntersectionLl);
            }

            for (var index = 0; index < polygonIntersections.Count; ++index)
            {
                var intsect = polygonIntersections[index];
                polygonIntersections.RemoveAll(c => c != intsect && Conversions.CalculateDistance(intsect, c) < 0.01);
            }

            return polygonIntersections;
        }

        public void ConflictProbe(FDP2.FDR fdr, int latSep)
        {
            var conDict = new List<ConflictData>();
            var fpap = new FPAP();

            if (fdr == null || !MMI.IsMySectorConcerned(fdr)) return;
            var cfl = fdr.CFLUpper;
            var rfl = fdr.RFL;
            var alt = cfl == -1 ? rfl : cfl;
            var isRvsm = fdr.RVSM;
            for (var i = 0; i < conDict.Count; i++)
                foreach (var fdr2 in FDP2.GetFDRs)
                {
                    if (fdr2 == null || fdr.Callsign == fdr2.Callsign || !MMI.IsMySectorConcerned(fdr2)) continue;
                    var data = new ConflictData();
                    var rte = fdr.ParsedRoute;
                    var rte2 = fdr2.ParsedRoute;
                    var trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong,
                        rte.Last().Intersection.LatLong);
                    var trk2 = Conversions.CalculateTrack(rte2.First().Intersection.LatLong,
                        rte2.Last().Intersection.LatLong);
                    data.trkAngle = Math.Abs(trk2 - trk);
                    var sameDir = data.trkAngle < 45;
                    var crossing = (data.trkAngle >= 45 && data.trkAngle <= 135) ||
                                   (data.trkAngle >= 315 && data.trkAngle <= 225);
                    var oppoDir = data.trkAngle > 135 && data.trkAngle < 225;
                    var cfl2 = fdr2.CFLUpper;
                    var rfl2 = fdr2.RFL;
                    var alt2 = cfl2 == -1 ? rfl2 : cfl2;
                    var isRvsm2 = fdr2.RVSM;
                    data.verticalAct = Math.Abs(alt - alt2);
                    data.verticalSep = alt > 45000 && alt < 60000
                        ? 4000
                        : alt > 60000
                            ? 5000
                            : alt2 > 45000 && alt2 < 60000
                                ? 4000
                                : alt2 > 60000
                                    ? 5000
                                    : (alt > FDP2.RVSM_BAND_LOWER && !isRvsm) ||
                                      (alt2 > FDP2.RVSM_BAND_LOWER && !isRvsm2) || alt > FDP2.RVSM_BAND_UPPER ||
                                      alt2 > FDP2.RVSM_BAND_UPPER
                                        ? 2000
                                        : 1000;

                    if (data.verticalAct < data.verticalSep)
                    {
                        //data.latSep = latSep1;

                        if (fpap.rnp4 && fpap.pbcs && fpap.adsc && fpap.cpdlc)
                            data.latSep = Math.Max(latSep, 23);


                        else if (fpap.rnp10 || fpap.rnp4)
                            data.latSep = Math.Max(latSep, 50);

                        else if (!fpap.rnp10 && !fpap.rnp4)
                            data.latSep = Math.Max(latSep, 100);

                        else if (data.latSep != 100 && data.latSep == 100) ;
                        {
                            data.latSep = (50 + 100) / 2;
                        }

                        var cpar = new CPAR(fdr2, fdr, data.latSep);
                        var segments1 = cpar.Segments1;
                        var segments2 = cpar.Segments2;

                        segments1.Sort((s, t) => s.startTime.CompareTo(t.startTime)); //sort by first conflict time
                        segments2.Sort((s, t) => s.startTime.CompareTo(t.startTime));

                        var firstConflictTime = segments1.FirstOrDefault();
                        var firstConflictTime2 = segments2.FirstOrDefault();
                        var failedLateral = segments1.Count > 0;
                        if (firstConflictTime == null || firstConflictTime2 == null) continue;

                        data.mnt = sameDir && fpap.jet;
                        data.longTimesep = data.mnt ? new TimeSpan(0, 0, 10, 0) : new TimeSpan(0, 0, 15, 0);
                        data.longTimeact = (firstConflictTime2.startTime - firstConflictTime.startTime).Duration();
                        data.longDistsep = fpap.jet && fpap.rnp4 && fpap.pbcs && fpap.cpdlc && fpap.adsc ? 30 :
                            fpap.jet && fpap.rnp10 && fpap.pbcs && fpap.cpdlc && fpap.adsc ? 50 : default;
                        data.longDistact = Conversions.CalculateDistance(firstConflictTime.startLatlong,
                            firstConflictTime2.startLatlong);
                        data.timeLongsame = sameDir && failedLateral && firstConflictTime.endTime > DateTime.UtcNow
                                            && data.longTimeact <
                                            data.longTimesep; //check time based longitudinal for same direction                    
                        data.timeLongcross = crossing && failedLateral && firstConflictTime.endTime > DateTime.UtcNow
                                             && (firstConflictTime2.startTime - firstConflictTime.startTime)
                                             .Duration() < new TimeSpan(0, 0, 15, 0);
                        data.distLongsame = sameDir && failedLateral && firstConflictTime.endTime > DateTime.UtcNow
                                            && data.longDistact < data.longDistsep;

                        data.timeLongopposite = false;
                        data.top = null;

                        if (failedLateral && oppoDir)
                        {
                            try
                            {
                                data.top = new TimeOfPassing(fdr, fdr2);
                            }

                            catch (Exception e)
                            {
                                return;
                            }

                            data.timeLongopposite = data.top.Time > DateTime.UtcNow
                                                    && data.top.Time.Add(new TimeSpan(0, 0, 10, 0)) > DateTime.UtcNow &&
                                                    data.top.Time.Subtract(new TimeSpan(0, 0, 10, 0)) < DateTime.UtcNow;
                        }

                        data.longType = fpap.adsc && fpap.cpdlc && (fpap.rnp4 || fpap.rnp10) && fpap.pbcs
                            ? data.distLongsame
                            : data.timeLongsame;

                        var lossOfSep = data.longType || data.timeLongcross || data.timeLongopposite;
                        data.conflictType = lossOfSep && sameDir ? "same" :
                            lossOfSep && crossing ? "crossing" :
                            lossOfSep && oppoDir ? "opposite" : null;
                        data.earliestLOS = failedLateral && oppoDir
                            ? data.top.Time.Subtract(new TimeSpan(0, 0, 10, 0))
                            : DateTime.Compare(firstConflictTime.startTime, firstConflictTime2.startTime) < 0
                                ? firstConflictTime.startTime
                                : firstConflictTime2.startTime;

                        data.actualConflicts = failedLateral && oppoDir && data.verticalAct < data.verticalSep
                            ? new TimeSpan(0, 0, 1, 0, 0) >= data.earliestLOS.Subtract(DateTime.UtcNow).Duration()
                            : (lossOfSep && new TimeSpan(0, 0, 1, 0, 0) >=
                                  data.earliestLOS.Subtract(DateTime.UtcNow).Duration()) ||
                              data.earliestLOS < DateTime.UtcNow;

                        data.imminentConflicts = failedLateral && oppoDir && data.verticalAct < data.verticalSep
                            ? new TimeSpan(0, 0, 30, 0, 0) >= data.earliestLOS.Subtract(DateTime.UtcNow).Duration()
                            : lossOfSep && new TimeSpan(0, 0, 30, 0, 0) >=
                            data.earliestLOS.Subtract(DateTime.UtcNow).Duration(); //check if timediff < 30 min

                        data.advisoryConflicts = failedLateral && oppoDir && data.verticalAct < data.verticalSep
                            ? new TimeSpan(0, 2, 0, 0, 0) > data.earliestLOS.Subtract(DateTime.UtcNow).Duration()
                              && data.earliestLOS.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0)
                            : lossOfSep && new TimeSpan(0, 2, 0, 0, 0) >
                                        data.earliestLOS.Subtract(DateTime.UtcNow).Duration()
                                        && data.earliestLOS.Subtract(DateTime.UtcNow).Duration() >=
                                        new TimeSpan(0, 0, 30, 0, 0); //check if  2 hours > timediff > 30 mins


                        if (data.actualConflicts || data.imminentConflicts)
                        {
                            label.imminentConflict.AddOrUpdate(fdr.Callsign,
                                new HashSet<string>(new[] { fdr2.Callsign }),
                                (k, v) =>
                                {
                                    v.Add(fdr2.Callsign);
                                    return v;
                                });

                            conDict.Add(data);
                        }
                        else
                        {
                            HashSet<string> maybeSet;
                            var exists = label.imminentConflict.TryGetValue(fdr.Callsign, out maybeSet);
                            if (exists) maybeSet.Clear();


                            var emptyImminentConflicts = label.imminentConflict.Where(pair => pair.Value.Count == 0)
                                .Select(pair => pair.Key)
                                .ToList();
                            foreach (var callsign in emptyImminentConflicts)
                                label.imminentConflict.TryRemove(callsign, out _);


                            if (data.advisoryConflicts)
                            {
                                label.advisoryConflict.AddOrUpdate(fdr.Callsign,
                                    new HashSet<string>(new[] { fdr2.Callsign }),
                                    (k, v) =>
                                    {
                                        v.Add(fdr2.Callsign);
                                        return v;
                                    });
                            }
                            else
                            {
                                HashSet<string> admaybeSet;
                                var adexists = label.advisoryConflict.TryGetValue(fdr.Callsign, out admaybeSet);
                                if (adexists) admaybeSet.Clear();

                                var emptyAdvisoryConflicts = label.advisoryConflict.Where(pair => pair.Value.Count == 0)
                                    .Select(pair => pair.Key)
                                    .ToList();
                                foreach (var callsign in emptyAdvisoryConflicts)
                                    label.advisoryConflict.TryRemove(callsign, out _);
                            }
                        }
                    }
                    else
                    {
                        HashSet<string>
                            maybeSet; // defines an uninitialized variable for the result of the below method to get placed in
                        HashSet<string> admaybeSet;
                        var adexists = label.advisoryConflict.TryGetValue(fdr.Callsign, out admaybeSet);
                        var exists =
                            label.imminentConflict.TryGetValue(fdr.Callsign,
                                out maybeSet); // tries to get the set of conflicts corresponding to a callsign, places it in the `maybeSet` variable if it exists. The bool `exists` represents whether there was a set of conflicts for that callsign
                        if (exists)
                            maybeSet.Remove(fdr2
                                .Callsign); // remove the fdr2 (which is the one we are currently evaluating for conflicts against fdr) from the set of conflicts
                        if (adexists)
                            admaybeSet.Remove(fdr2
                                .Callsign); // remove the fdr2 (which is the one we are currently evaluating for conflicts against fdr) from the set of conflicts

                        var emptyAdvisoryConflicts = label.advisoryConflict.Where(pair => pair.Value.Count == 0)
                            .Select(pair => pair.Key)
                            .ToList();
                        foreach (var callsign in emptyAdvisoryConflicts)
                            label.advisoryConflict.TryRemove(callsign, out _);
                        var emptyImminentConflicts = label.imminentConflict.Where(pair => pair.Value.Count == 0)
                            .Select(pair => pair.Key)
                            .ToList();
                        foreach (var callsign in emptyImminentConflicts)
                            label.imminentConflict.TryRemove(callsign, out _);
                    }
                }
        }


        public class Segment
        {
            public string callsign;
            public Coordinate endLatlong;
            public DateTime endTime = DateTime.MaxValue;
            public FDP2.FDR.ExtractedRoute.Segment routeSegment;
            public Coordinate startLatlong;
            public DateTime startTime = DateTime.MaxValue;
        }

        public class ConflictData
        {
            public bool actualConflicts;
            public bool advisoryConflicts;
            public string conflictType;
            public bool distLongsame;
            public DateTime earliestLOS;
            public Segment endLatlong;
            public Segment endTime;
            public FDP2.FDR fdr;
            public FDP2.FDR fdr2;
            public bool imminentConflicts;
            public int latAct;
            public int latSep;
            public double longDistact;
            public double longDistsep;
            public TimeSpan longTimeact;
            public TimeSpan longTimesep;
            public bool longType;
            public bool mnt;
            public Segment startLatlong;
            public Segment startTime;
            public bool timeLongcross;
            public bool timeLongopposite;
            public bool timeLongsame;
            public TimeOfPassing top;
            public double trkAngle;
            public int verticalAct;
            public int verticalSep;
        }
    }
}