using System;
using System.Collections.Generic;
using System.Linq;
using vatsys;

namespace AtopPlugin.Conflict;

public static class ConflictAreaCalculator
{
    public static List<ConflictSegment> CalculateAreaOfConflict(FDP2.FDR fdr1, FDP2.FDR fdr2, int value)
    {
        var fdr2SegmentsConflictingWithFdr1 = new List<ConflictSegment>();
        var route1Waypoints = fdr1.ParsedRoute.ToList()
            .Where(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT).ToList();
        var route2Waypoints = fdr2.ParsedRoute.ToList()
            .Where(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT).ToList();
        for (var wp1Index = 1; wp1Index < route1Waypoints.Count; ++wp1Index)
        {
            var route1ProtectedAirspace = CreatePolygon(route1Waypoints[wp1Index - 1].Intersection.LatLong,
                route1Waypoints[wp1Index].Intersection.LatLong, value);
            for (var wp2Index = 1; wp2Index < route2Waypoints.Count; wp2Index++)
            {
                var prevRoute2LatLong = route2Waypoints[wp2Index - 1].Intersection.LatLong;
                var currRoute2LatLong = route2Waypoints[wp2Index].Intersection.LatLong;
                var protectedAirspacePenetrations = CalculatePolygonIntersections(route1ProtectedAirspace,
                    prevRoute2LatLong,
                    currRoute2LatLong);
                var intersectionPoints = new List<Coordinate>();
                var num1 = 0;
                var num2 = 0;
                foreach (var penetration in protectedAirspacePenetrations)
                    if (Conversions.IsLatLonOnGC(prevRoute2LatLong, currRoute2LatLong, penetration))
                    {
                        intersectionPoints.Add(penetration);
                    }
                    else
                    {
                        var route2Track = Conversions.CalculateTrack(prevRoute2LatLong, currRoute2LatLong);
                        if (Math.Abs(route2Track - Conversions.CalculateTrack(prevRoute2LatLong, penetration)) > 90.0)
                            ++num1;
                        if (Math.Abs(route2Track - Conversions.CalculateTrack(penetration, currRoute2LatLong)) > 90.0)
                            ++num2;
                    }

                if (num1 % 2 != 0 && num2 % 2 != 0)
                {
                    intersectionPoints.Clear();
                    intersectionPoints.Add(prevRoute2LatLong);
                    intersectionPoints.Add(currRoute2LatLong);
                }
                else if (num2 % 2 != 0)
                {
                    intersectionPoints.Add(currRoute2LatLong);
                }
                else if (num1 % 2 != 0)
                {
                    intersectionPoints.Add(prevRoute2LatLong);
                }

                intersectionPoints.Sort((x, y) =>
                    Conversions.CalculateDistance(prevRoute2LatLong, x)
                        .CompareTo(Conversions.CalculateDistance(currRoute2LatLong, y)));
                for (var ipIndex = 1; ipIndex < intersectionPoints.Count; ipIndex += 2)
                {
                    var seg = new ConflictSegment();
                    seg.StartLatlong = intersectionPoints[ipIndex - 1];
                    seg.EndLatlong = intersectionPoints[ipIndex];
                    var conflictSegments = fdr2SegmentsConflictingWithFdr1
                        .Where(s => s.RouteSegment == route2Waypoints[wp2Index]).Where(s =>
                            (Conversions.CalculateDistance(s.StartLatlong,
                                 prevRoute2LatLong) <
                             Conversions.CalculateDistance(seg.StartLatlong,
                                 prevRoute2LatLong) &&
                             Conversions.CalculateDistance(s.EndLatlong,
                                 prevRoute2LatLong) >
                             Conversions.CalculateDistance(seg.StartLatlong,
                                 prevRoute2LatLong)) ||
                            (Conversions.CalculateDistance(s.EndLatlong,
                                 prevRoute2LatLong) >
                             Conversions.CalculateDistance(seg.EndLatlong,
                                 prevRoute2LatLong) &&
                             Conversions.CalculateDistance(s.StartLatlong,
                                 prevRoute2LatLong) <
                             Conversions.CalculateDistance(seg.EndLatlong,
                                 prevRoute2LatLong)) ||
                            (Conversions.CalculateDistance(s.StartLatlong,
                                 prevRoute2LatLong) >
                             Conversions.CalculateDistance(seg.StartLatlong,
                                 prevRoute2LatLong) &&
                             Conversions.CalculateDistance(s.EndLatlong,
                                 prevRoute2LatLong) <
                             Conversions.CalculateDistance(seg.EndLatlong,
                                 prevRoute2LatLong)) ||
                            Conversions.CalculateDistance(s.StartLatlong, seg.StartLatlong) < 0.01 ||
                            Conversions.CalculateDistance(s.EndLatlong, seg.EndLatlong) < 0.01).ToList();
                    if (conflictSegments.Count > 0)
                    {
                        foreach (var segment in conflictSegments)
                        {
                            if (Conversions.CalculateDistance(segment.EndLatlong,
                                    prevRoute2LatLong) <
                                Conversions.CalculateDistance(seg.EndLatlong,
                                    prevRoute2LatLong))
                                segment.EndLatlong = seg.EndLatlong;
                            if (Conversions.CalculateDistance(seg.StartLatlong,
                                    prevRoute2LatLong) <
                                Conversions.CalculateDistance(segment.StartLatlong,
                                    prevRoute2LatLong))
                                segment.StartLatlong = seg.StartLatlong;
                        }
                    }
                    else
                    {
                        seg.Callsign = fdr2.Callsign;
                        seg.RouteSegment = route2Waypoints[wp2Index];
                        fdr2SegmentsConflictingWithFdr1.Add(seg);
                    }
                }
            }
        }

        foreach (var segment in fdr2SegmentsConflictingWithFdr1)
        {
            if (!fdr2SegmentsConflictingWithFdr1.Exists(s =>
                    Conversions.CalculateDistance(segment.StartLatlong, s.EndLatlong) < 0.01))
                segment.StartTime =
                    FDP2.GetSystemEstimateAtPosition(fdr2, segment.StartLatlong, segment.RouteSegment);
            if (!fdr2SegmentsConflictingWithFdr1.Exists(s =>
                    Conversions.CalculateDistance(segment.EndLatlong, s.StartLatlong) < 0.01))
                segment.EndTime = FDP2.GetSystemEstimateAtPosition(fdr2, segment.EndLatlong, segment.RouteSegment);
        }

        return fdr2SegmentsConflictingWithFdr1;
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
            polygonIntersections.RemoveAll(c =>
                !Equals(c, intsect) && Conversions.CalculateDistance(intsect, c) < 0.01);
        }

        return polygonIntersections;
    }
}

public class ConflictSegment
{
    public string Callsign;
    public Coordinate EndLatlong;
    public DateTime EndTime = DateTime.MaxValue;
    public FDP2.FDR.ExtractedRoute.Segment RouteSegment;
    public Coordinate StartLatlong;
    public DateTime StartTime = DateTime.MaxValue;
}