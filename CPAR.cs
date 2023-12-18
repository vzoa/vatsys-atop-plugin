// Decompiled with JetBrains decompiler
// Type: vatsys.LATC
// Assembly: vatSys, Version=0.4.8114.34539, Culture=neutral, PublicKeyToken=null
// MVID: E82FB2F8-DAB0-42FD-91AA-1C44F8E62564
// Assembly location: E:\vatsys\bin\vatSys.exe
// XML documentation location: E:\vatsys\bin\vatSys.xml

using System;
using System.Collections.Generic;
using System.Linq;

namespace vatsys
{
    public class CPAR
    {
        public List<CPAR.Segment> Segments1 = new List<CPAR.Segment>();
        public List<CPAR.Segment> Segments2 = new List<CPAR.Segment>();
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
    }
}
