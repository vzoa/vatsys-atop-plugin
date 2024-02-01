// Decompiled with JetBrains decompiler
// Type: vatsys.LATC
// Assembly: vatSys, Version=0.4.8114.34539, Culture=neutral, PublicKeyToken=null
// MVID: E82FB2F8-DAB0-42FD-91AA-1C44F8E62564
// Assembly location: E:\vatsys\bin\vatSys.exe
// XML documentation location: E:\vatsys\bin\vatSys.xml

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace vatsys
{
    public class CPAR
    {
        public List<CPAR.Segment> Segments1 = new List<CPAR.Segment>();
        public List<CPAR.Segment> Segments2 = new List<CPAR.Segment>();
        AuroraLabelItemsPlugin.AuroraLabelItemsPlugin label = new AuroraLabelItemsPlugin.AuroraLabelItemsPlugin();
        public DateTime Timeout = DateTime.MaxValue;

        public CPAR(FDP2.FDR fdr1, FDP2.FDR fdr2, int value) => this.CalculateLATC(fdr1, fdr2, value);

        public CPAR()
        {
        }

        public void CalculateLATC(FDP2.FDR fdr1, FDP2.FDR fdr2, int value)
        {
            if (fdr1 == null || fdr2 == null)
                return;
            this.Segments1.AddRange((IEnumerable<CPAR.Segment>)this.CalculateAreaOfConflict(fdr1, fdr2, value));
            this.Segments2.AddRange((IEnumerable<CPAR.Segment>)this.CalculateAreaOfConflict(fdr2, fdr1, value));
        }

        public List<Segment> CalculateAreaOfConflict(FDP2.FDR fdr1, FDP2.FDR fdr2, int value)
        {
            List<Segment> segs = new List<Segment>();
            List<FDP2.FDR.ExtractedRoute.Segment> route1waypoints = fdr1.ParsedRoute.ToList().Where(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT).ToList();
            List<FDP2.FDR.ExtractedRoute.Segment> route2waypoints = fdr2.ParsedRoute.ToList().Where(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT).ToList();
            for (int wp1index = 1; wp1index < route1waypoints.Count; ++wp1index)
            {
                List<Coordinate> route1Segment = CreatePolygon(route1waypoints[wp1index - 1].Intersection.LatLong, route1waypoints[wp1index].Intersection.LatLong, value);
                for (int wp2index = 1; wp2index < route2waypoints.Count; wp2index++)
                {
                    List<Coordinate> source = new List<Coordinate>();
                    List<Coordinate> intersectionPoints = new List<Coordinate>();
                    source.AddRange((IEnumerable<Coordinate>)CalculatePolygonIntersections(route1Segment, route2waypoints[wp2index - 1].Intersection.LatLong, route2waypoints[wp2index].Intersection.LatLong));
                    int num1 = 0;
                    int num2 = 0;
                    foreach (Coordinate coordinate in source.ToList<Coordinate>())
                    {
                        if (Conversions.IsLatLonOnGC(route2waypoints[wp2index - 1].Intersection.LatLong, route2waypoints[wp2index].Intersection.LatLong, coordinate))
                        {
                            intersectionPoints.Add(coordinate);
                        }
                        else
                        {
                            double track = Conversions.CalculateTrack(route2waypoints[wp2index - 1].Intersection.LatLong, route2waypoints[wp2index].Intersection.LatLong);
                            if (Math.Abs(track - Conversions.CalculateTrack(route2waypoints[wp2index - 1].Intersection.LatLong, coordinate)) > 90.0)
                                ++num1;
                            if (Math.Abs(track - Conversions.CalculateTrack(coordinate, route2waypoints[wp2index].Intersection.LatLong)) > 90.0)
                                ++num2;
                        }
                    }
                    if (num1 % 2 != 0 && num2 % 2 != 0)
                    {
                        intersectionPoints.Clear();
                        intersectionPoints.Add(route2waypoints[wp2index - 1].Intersection.LatLong);
                        intersectionPoints.Add(route2waypoints[wp2index].Intersection.LatLong);
                    }
                    else if (num2 % 2 != 0)
                        intersectionPoints.Add(route2waypoints[wp2index].Intersection.LatLong);
                    else if (num1 % 2 != 0)
                        intersectionPoints.Add(route2waypoints[wp2index - 1].Intersection.LatLong);
                    intersectionPoints.Sort((x, y) => Conversions.CalculateDistance(route2waypoints[wp2index - 1].Intersection.LatLong, x).CompareTo(Conversions.CalculateDistance(route2waypoints[wp2index - 1].Intersection.LatLong, y)));
                    for (int ipIndex = 1; ipIndex < intersectionPoints.Count; ipIndex += 2)
                    {
                        Segment seg = new Segment();
                        seg.startLatlong = intersectionPoints[ipIndex - 1];
                        seg.endLatlong = intersectionPoints[ipIndex];
                        List<Segment> conflictSegments = segs.Where<Segment>((Func<Segment, bool>)(s => s.routeSegment == route2waypoints[wp2index])).Where<Segment>((Func<Segment, bool>)(s => Conversions.CalculateDistance(s.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(seg.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) && Conversions.CalculateDistance(s.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) > Conversions.CalculateDistance(seg.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) || Conversions.CalculateDistance(s.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) > Conversions.CalculateDistance(seg.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) && Conversions.CalculateDistance(s.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(seg.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) || Conversions.CalculateDistance(s.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) > Conversions.CalculateDistance(seg.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) && Conversions.CalculateDistance(s.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(seg.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) || Conversions.CalculateDistance(s.startLatlong, seg.startLatlong) < 0.01 || Conversions.CalculateDistance(s.endLatlong, seg.endLatlong) < 0.01)).ToList<Segment>();
                        if (conflictSegments.Count > 0)
                        {
                            foreach (Segment segment in conflictSegments)
                            {
                                if (Conversions.CalculateDistance(segment.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(seg.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong))
                                    segment.endLatlong = seg.endLatlong;
                                if (Conversions.CalculateDistance(seg.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(segment.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong))
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
            for (int i = 0; i < segs.Count; i++)
            {
                if (!segs.Exists((Predicate<Segment>)(s => Conversions.CalculateDistance(segs[i].startLatlong, s.endLatlong) < 0.01)))
                    segs[i].startTime = FDP2.GetSystemEstimateAtPosition(fdr2, segs[i].startLatlong, segs[i].routeSegment);
                if (!segs.Exists((Predicate<Segment>)(s => Conversions.CalculateDistance(segs[i].endLatlong, s.startLatlong) < 0.01)))
                    segs[i].endTime = FDP2.GetSystemEstimateAtPosition(fdr2, segs[i].endLatlong, segs[i].routeSegment);
            }
            return segs;
        }

        public static List<Coordinate> CreatePolygon(Coordinate point1, Coordinate point2, int value)
        {
            List<Coordinate> polygon = new List<Coordinate>();
            double track = Conversions.CalculateTrack(point1, point2);
            double num1 = track - 90.0;
            for (int index = 0; index <= 180; index += 10)
            {
                double heading = num1 - (double)index;
                Coordinate fromBearingRange = Conversions.CalculateLLFromBearingRange(point1, (double)value, heading);
                polygon.Add(fromBearingRange);
            }
            double num2 = track + 90.0;
            for (int index = 0; index <= 180; index += 10)
            {
                double heading = num2 - (double)index;
                Coordinate fromBearingRange = Conversions.CalculateLLFromBearingRange(point2, (double)value, heading);
                polygon.Add(fromBearingRange);
            }
            polygon.Add(polygon[0]);
            return polygon;
        }

        public static List<Coordinate> CalculatePolygonIntersections(
            List<Coordinate> polygon,
            Coordinate point1,
            Coordinate point2)
        {
            List<Coordinate> polygonIntersections = new List<Coordinate>();
            for (int index = 1; index < polygon.Count; ++index)
            {
                List<Coordinate> gcIntersectionLl = Conversions.CalculateAllGCIntersectionLL(polygon[index - 1], polygon[index], point1, point2);
                if (gcIntersectionLl != null)
                    polygonIntersections.AddRange(gcIntersectionLl);
            }
            for (int index = 0; index < polygonIntersections.Count; ++index)
            {
                Coordinate intsect = polygonIntersections[index];
                polygonIntersections.RemoveAll(c => c != intsect && Conversions.CalculateDistance(intsect, c) < 0.01);
            }
            return polygonIntersections;
        }


        public class Segment
        {
            public string callsign;
            public Coordinate startLatlong;
            public Coordinate endLatlong;
            public DateTime startTime = DateTime.MaxValue;
            public DateTime endTime = DateTime.MaxValue;
            public FDP2.FDR.ExtractedRoute.Segment routeSegment;
        }
        public void ConflictProbe(FDP2.FDR fdr, int latSep1)
        {
            if ((fdr == null) || !MMI.IsMySectorConcerned(fdr)) return;
            int cfl = fdr.CFLUpper;
            int rfl = fdr.RFL;
            int alt = cfl == -1 ? rfl : cfl;
            var isRvsm = fdr.RVSM;

            foreach (var fdr2 in FDP2.GetFDRs)
            {
                if (fdr2 == null || fdr.Callsign == fdr2.Callsign || !MMI.IsMySectorConcerned(fdr2)) continue;
                var rte = fdr.ParsedRoute;
                var rte2 = fdr2.ParsedRoute;
                double trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong,
                    rte.Last().Intersection.LatLong);
                double trk2 = Conversions.CalculateTrack(rte2.First().Intersection.LatLong,
                    rte2.Last().Intersection.LatLong);
                var trkdelta = Math.Abs(trk2 - trk);
                bool sameDir = trkdelta < 45;
                bool crossing = (trkdelta >= 45 && trkdelta <= 135) || (trkdelta >= 315 && trkdelta <= 225);
                bool oppoDir = trkdelta > 135 && trkdelta < 225;
                int cfl2 = fdr2.CFLUpper;
                int rfl2 = fdr2.RFL;
                int alt2 = cfl2 == -1 ? rfl2 : cfl2;
                var isRvsm2 = fdr2.RVSM;
                var delta = Math.Abs(alt - alt2);
                int verticalSep = (alt > 45000 && alt < 60000) ? 4000 : alt > 60000 ? 5000 : (alt2 > 45000 && alt2 < 60000) ? 4000 : alt2 > 60000 ? 5000 :
                                     (alt > FDP2.RVSM_BAND_LOWER && !isRvsm) ||
                                     (alt2 > FDP2.RVSM_BAND_LOWER && !isRvsm2) || (alt > FDP2.RVSM_BAND_UPPER || alt2 > FDP2.RVSM_BAND_UPPER)
                    ? 2000
                    : 1000;

                if (delta < verticalSep)
                {
                    Match pbn2 = Regex.Match(fdr2.Remarks, @"PBN\/\w+\s");
                    bool rnp10 = pbn2.Value.Contains("A1");
                    bool rnp4 = pbn2.Value.Contains("L1");
                    bool cpdlc = (Regex.IsMatch(fdr2.AircraftEquip, @"J5") || Regex.IsMatch(fdr2.AircraftEquip, @"J7"));
                    bool adsc = Regex.IsMatch(fdr2.AircraftSurvEquip, @"D1");
                    bool jet = fdr2.PerformanceData?.IsJet ?? false;

                    var newValue = latSep1;

                    if (rnp4 && adsc && cpdlc)
                    {
                        newValue = Math.Max(latSep1, 23);
                    }


                    else if (rnp10 || rnp4)
                    {
                        newValue = Math.Max(latSep1, 50);
                    }

                    else if (!rnp10 && !rnp4)
                    {
                        newValue = Math.Max(latSep1, 100);
                    }

                    else if (newValue != latSep1 && newValue == 100);
                    {
                        newValue = (50 + 100) / 2;
                    }

                    var cpar = new CPAR(fdr2, fdr, newValue);
                    var segments1 = cpar.Segments1;
                    var segments2 = cpar.Segments2;

                    segments1.Sort((Comparison<CPAR.Segment>)((s, t) => s.startTime.CompareTo(t.startTime))); //sort by first conflict time
                    segments2.Sort((Comparison<CPAR.Segment>)((s, t) => s.startTime.CompareTo(t.startTime)));

                    var firstConflictTime = segments1.FirstOrDefault();
                    var firstConflictTime2 = segments2.FirstOrDefault();
                    var isConflictPair = segments1.Count > 0;
                    if (firstConflictTime == null || firstConflictTime2 == null) continue;


                    var timeLongSame = sameDir && isConflictPair && firstConflictTime.endTime > DateTime.UtcNow
                        && (firstConflictTime2.startTime - firstConflictTime.startTime).Duration() < (jet ? (new TimeSpan(0, 0, 10, 0)) : (new TimeSpan(0, 0, 15, 0)));//check time based longitudinal for same direction                    
                    var timeLongCross = crossing && isConflictPair && firstConflictTime.endTime > DateTime.UtcNow
                        && (firstConflictTime2.startTime - firstConflictTime.startTime).Duration() < (new TimeSpan(0, 0, 15, 0));
                    var distLongSame = sameDir && isConflictPair && firstConflictTime.endTime > DateTime.UtcNow
                        && Conversions.CalculateDistance(firstConflictTime.startLatlong, firstConflictTime2.startLatlong)
                        < (jet && rnp4 && cpdlc && adsc ? 30 : (rnp4 || rnp10) ? 50 : 50);

                    var timeLongOpposite = false;
                    TimeOfPassing top = null;

                    if (isConflictPair && oppoDir)
                    {
                        try
                        {
                            top = new TimeOfPassing(fdr, fdr2);
                        }

                        catch (Exception e)
                        {
                            return;
                        }
                        timeLongOpposite = top.Time > DateTime.UtcNow
                            && (top.Time.Add(new TimeSpan(0, 0, 10, 0)) > DateTime.UtcNow) && (top.Time.Subtract(new TimeSpan(0, 0, 10, 0)) < DateTime.UtcNow);
                    }


                    var lossOfSep = timeLongSame || timeLongCross || distLongSame || timeLongOpposite;

                    var earliestLOS = (isConflictPair && oppoDir ? top.Time.Subtract(new TimeSpan(0, 0, 10, 0))
                        : (DateTime.Compare(firstConflictTime.startTime, firstConflictTime2.startTime) < 0 ? firstConflictTime.startTime : firstConflictTime2.startTime));

                    var actualConflicts = (isConflictPair && oppoDir && (delta < verticalSep)) ? (new TimeSpan(0, 0, 1, 0, 0) >= earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        : (lossOfSep && new TimeSpan(0, 0, 1, 0, 0) >= earliestLOS.Subtract(DateTime.UtcNow).Duration()) || earliestLOS < DateTime.UtcNow;

                    var imminentConflicts = (isConflictPair && oppoDir && (delta < verticalSep)) ? (new TimeSpan(0, 0, 30, 0, 0) >= earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        : (lossOfSep && new TimeSpan(0, 0, 30, 0, 0) >= earliestLOS.Subtract(DateTime.UtcNow).Duration()); //check if timediff < 30 min

                    var advisoryConflicts = (isConflictPair && oppoDir && (delta < verticalSep)) ? (new TimeSpan(0, 2, 0, 0, 0) > earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        && (earliestLOS.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0))
                        : (lossOfSep && new TimeSpan(0, 2, 0, 0, 0) > earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        && (earliestLOS.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0));  //check if  2 hours > timediff > 30 mins



                    if (actualConflicts || imminentConflicts)
                    {
                        label.imminentConflict.AddOrUpdate(fdr.Callsign, new HashSet<string>(new string[] { fdr2.Callsign }),
                        (k, v) => { v.Add(fdr2.Callsign); return v; });


                    }
                    else
                    {
                        HashSet<string> maybeSet;
                        bool exists = label.imminentConflict.TryGetValue(fdr.Callsign, out maybeSet);
                        if (exists)
                        {
                            maybeSet.Clear();
                        }


                        var emptyImminentConflicts = label.imminentConflict.Where(pair => pair.Value.Count == 0)
                            .Select(pair => pair.Key)
                            .ToList();
                        foreach (var callsign in emptyImminentConflicts)
                        {
                            label.imminentConflict.TryRemove(callsign, out _);
                        }



                        if (advisoryConflicts)
                        {
                            label.advisoryConflict.AddOrUpdate(fdr.Callsign, new HashSet<string>(new string[] { fdr2.Callsign }),
                            (k, v) => { v.Add(fdr2.Callsign); return v; });

                        }
                        else
                        {
                            HashSet<string> admaybeSet;
                            bool adexists = label.advisoryConflict.TryGetValue(fdr.Callsign, out admaybeSet);
                            if (adexists)
                            {
                                admaybeSet.Clear();
                            }

                            var emptyAdvisoryConflicts = label.advisoryConflict.Where(pair => pair.Value.Count == 0)
                                .Select(pair => pair.Key)
                                .ToList();
                            foreach (var callsign in emptyAdvisoryConflicts)
                            {
                                label.advisoryConflict.TryRemove(callsign, out _);
                            }

                        }

                    }
                }
                else
                {
                    HashSet<string> maybeSet; // defines an uninitialized variable for the result of the below method to get placed in
                    HashSet<string> admaybeSet;
                    bool adexists = label.advisoryConflict.TryGetValue(fdr.Callsign, out admaybeSet);
                    bool exists = label.imminentConflict.TryGetValue(fdr.Callsign, out maybeSet); // tries to get the set of conflicts corresponding to a callsign, places it in the `maybeSet` variable if it exists. The bool `exists` represents whether there was a set of conflicts for that callsign
                    if (exists)
                    {
                        maybeSet.Remove(fdr2.Callsign); // remove the fdr2 (which is the one we are currently evaluating for conflicts against fdr) from the set of conflicts
                    }
                    if (adexists)
                    {
                        admaybeSet.Remove(fdr2.Callsign); // remove the fdr2 (which is the one we are currently evaluating for conflicts against fdr) from the set of conflicts
                    }

                    var emptyAdvisoryConflicts = label.advisoryConflict.Where(pair => pair.Value.Count == 0)
                        .Select(pair => pair.Key)
                        .ToList();
                    foreach (var callsign in emptyAdvisoryConflicts)
                    {
                        label.advisoryConflict.TryRemove(callsign, out _);
                    }
                    var emptyImminentConflicts = label.imminentConflict.Where(pair => pair.Value.Count == 0)
                        .Select(pair => pair.Key)
                        .ToList();
                    foreach (var callsign in emptyImminentConflicts)
                    {
                        label.imminentConflict.TryRemove(callsign, out _);
                    }
                }
            }
        }
    }
}
