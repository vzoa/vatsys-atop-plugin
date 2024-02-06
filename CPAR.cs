﻿// Decompiled with JetBrains decompiler
// Type: vatsys.LATC
// Assembly: vatSys, Version=0.4.8114.34539, Culture=neutral, PublicKeyToken=null
// MVID: E82FB2F8-DAB0-42FD-91AA-1C44F8E62564
// Assembly location: E:\vatsys\bin\vatSys.exe
// XML documentation location: E:\vatsys\bin\vatSys.xml

using AuroraLabelItemsPlugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace vatsys
{
    public class CPAR
    {
        public List<CPAR.Segment> Segments1 = new List<CPAR.Segment>();
        public List<CPAR.Segment> Segments2 = new List<CPAR.Segment>();
        AuroraLabelItemsPlugin.GEO label = new AuroraLabelItemsPlugin.GEO();
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

        public class ConflictData
        {
            public FDP2.FDR fdr;
            public FDP2.FDR fdr2;
            public double trkAngle;
            public int verticalAct;
            public int verticalSep;
            public int latAct;
            public int latSep;
            public bool longType;
            public TimeSpan longTimesep;
            public TimeSpan longTimeact;
            public bool mnt;
            public double longDistsep;
            public double longDistact;
            public TimeOfPassing top;
            public string conflictType;
            public Segment startLatlong;
            public Segment endLatlong;
            public Segment startTime;
            public Segment endTime;
            public DateTime earliestLOS;
            public bool timeLongsame;
            public bool distLongsame;
            public bool timeLongcross;
            public bool timeLongopposite;
            public bool actualConflicts;
            public bool imminentConflicts;
            public bool advisoryConflicts;
        }
        public void ConflictProbe(FDP2.FDR fdr, int latSep)
        {
            List<ConflictData> conDict = new List<ConflictData>();
            FPAP fpap = new FPAP();

            if ((fdr == null) || !MMI.IsMySectorConcerned(fdr)) return;
            int cfl = fdr.CFLUpper;
            int rfl = fdr.RFL;
            int alt = cfl == -1 ? rfl : cfl;
            var isRvsm = fdr.RVSM;
            for (int i = 0; i < conDict.Count; i++)
            foreach (var fdr2 in FDP2.GetFDRs)
            {
                if (fdr2 == null || fdr.Callsign == fdr2.Callsign || !MMI.IsMySectorConcerned(fdr2)) continue;
                var data = new ConflictData();
                var rte = fdr.ParsedRoute;
                var rte2 = fdr2.ParsedRoute;
                double trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong,
                    rte.Last().Intersection.LatLong);
                double trk2 = Conversions.CalculateTrack(rte2.First().Intersection.LatLong,
                    rte2.Last().Intersection.LatLong);
                data.trkAngle = Math.Abs(trk2 - trk);
                bool sameDir = data.trkAngle < 45;
                bool crossing = (data.trkAngle >= 45 && data.trkAngle <= 135) || (data.trkAngle >= 315 && data.trkAngle <= 225);
                bool oppoDir = data.trkAngle > 135 && data.trkAngle < 225;                
                int cfl2 = fdr2.CFLUpper;
                int rfl2 = fdr2.RFL;
                int alt2 = cfl2 == -1 ? rfl2 : cfl2;
                var isRvsm2 = fdr2.RVSM;
                data.verticalAct = Math.Abs(alt - alt2);
                data.verticalSep = (alt > 45000 && alt < 60000) ? 4000 : alt > 60000 ? 5000 : (alt2 > 45000 && alt2 < 60000) ? 4000 : alt2 > 60000 ? 5000 :
                                     (alt > FDP2.RVSM_BAND_LOWER && !isRvsm) ||
                                     (alt2 > FDP2.RVSM_BAND_LOWER && !isRvsm2) || (alt > FDP2.RVSM_BAND_UPPER || alt2 > FDP2.RVSM_BAND_UPPER)
                    ? 2000
                    : 1000;

                if (data.verticalAct < data.verticalSep)
                {

                    //data.latSep = latSep1;

                    if (fpap.rnp4 && fpap.pbcs && fpap.adsc && fpap.cpdlc)
                    {
                        data.latSep = Math.Max(latSep, 23);
                    }


                    else if (fpap.rnp10 || fpap.rnp4)
                    {
                        data.latSep = Math.Max(latSep, 50);
                    }

                    else if (!fpap.rnp10 && !fpap.rnp4)
                    {
                        data.latSep = Math.Max(latSep, 100);
                    }

                    else if (data.latSep != 100 && data.latSep == 100);
                    {
                        data.latSep = (50 + 100) / 2;
                    }

                    var cpar = new CPAR(fdr2, fdr, data.latSep);
                    var segments1 = cpar.Segments1;
                    var segments2 = cpar.Segments2;

                    segments1.Sort((Comparison<CPAR.Segment>)((s, t) => s.startTime.CompareTo(t.startTime))); //sort by first conflict time
                    segments2.Sort((Comparison<CPAR.Segment>)((s, t) => s.startTime.CompareTo(t.startTime)));

                    var firstConflictTime = segments1.FirstOrDefault();
                    var firstConflictTime2 = segments2.FirstOrDefault();
                    var failedLateral = segments1.Count > 0;
                    if (firstConflictTime == null || firstConflictTime2 == null) continue;
                        
                    data.mnt = sameDir && fpap.jet;
                    data.longTimesep = data.mnt ? (new TimeSpan(0, 0, 10, 0)) : (new TimeSpan(0, 0, 15, 0));
                    data.longTimeact = (firstConflictTime2.startTime - firstConflictTime.startTime).Duration();
                    data.longDistsep = fpap.jet && fpap.rnp4 && fpap.pbcs && fpap.cpdlc && fpap.adsc ? 30 : fpap.jet && fpap.rnp10 && fpap.pbcs && fpap.cpdlc && fpap.adsc ? 50 : default;
                    data.longDistact = Conversions.CalculateDistance(firstConflictTime.startLatlong, firstConflictTime2.startLatlong);
                    data.timeLongsame = sameDir && failedLateral && firstConflictTime.endTime > DateTime.UtcNow
                        && data.longTimeact < data.longTimesep;//check time based longitudinal for same direction                    
                    data.timeLongcross = crossing && failedLateral && firstConflictTime.endTime > DateTime.UtcNow
                        && (firstConflictTime2.startTime - firstConflictTime.startTime).Duration() < (new TimeSpan(0, 0, 15, 0));
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
                            && (data.top.Time.Add(new TimeSpan(0, 0, 10, 0)) > DateTime.UtcNow) && (data.top.Time.Subtract(new TimeSpan(0, 0, 10, 0)) < DateTime.UtcNow);
                        }

                    data.longType = (fpap.adsc && fpap.cpdlc && (fpap.rnp4 || fpap.rnp10) && fpap.pbcs) ? data.distLongsame : data.timeLongsame;

                    var lossOfSep = data.longType || data.timeLongcross || data.timeLongopposite;
                    data.conflictType = lossOfSep && sameDir ? "same" : lossOfSep && crossing ? "crossing" : lossOfSep && oppoDir ? "opposite" : null;
                    data.earliestLOS = (failedLateral && oppoDir ? data.top.Time.Subtract(new TimeSpan(0, 0, 10, 0))
                    : (DateTime.Compare(firstConflictTime.startTime, firstConflictTime2.startTime) < 0 ? firstConflictTime.startTime : firstConflictTime2.startTime));

                    data.actualConflicts = (failedLateral && oppoDir && (data.verticalAct < data.verticalSep)) ? (new TimeSpan(0, 0, 1, 0, 0) >= data.earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        : (lossOfSep && new TimeSpan(0, 0, 1, 0, 0) >= data.earliestLOS.Subtract(DateTime.UtcNow).Duration()) || data.earliestLOS < DateTime.UtcNow;

                    data.imminentConflicts = (failedLateral && oppoDir && (data.verticalAct < data.verticalSep)) ? (new TimeSpan(0, 0, 30, 0, 0) >= data.earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        : (lossOfSep && new TimeSpan(0, 0, 30, 0, 0) >= data.earliestLOS.Subtract(DateTime.UtcNow).Duration()); //check if timediff < 30 min

                    data.advisoryConflicts = (failedLateral && oppoDir && (data.verticalAct < data.verticalSep)) ? (new TimeSpan(0, 2, 0, 0, 0) > data.earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        && (data.earliestLOS.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0))
                        : (lossOfSep && new TimeSpan(0, 2, 0, 0, 0) > data.earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        && (data.earliestLOS.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0));  //check if  2 hours > timediff > 30 mins



                    if (data.actualConflicts || data.imminentConflicts)
                    {
                        label.imminentConflict.AddOrUpdate(fdr.Callsign, new HashSet<string>(new string[] { fdr2.Callsign }),
                        (k, v) => { v.Add(fdr2.Callsign); return v; });

                            conDict.Add(data);

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



                        if (data.advisoryConflicts)
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
