using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using vatsys;

namespace AtopPlugin.Display;

public static class DynamicSectorBoundaryRenderer
{
    private static readonly object Sync = new();
    private static bool _initialized;

    private static readonly string[] TargetMapNames = { "OSec", "OSEC" };

    public static void Initialize()
    {
        if (_initialized) return;

        _initialized = true;
        MMI.SectorsControlledChanged += MMI_SectorsControlledChanged;
        Refresh();
    }

    public static void Reset()
    {
        lock (Sync)
        {
            var map = GetOrCreateTargetMap();
            map.Lines.Clear();
            map.Infills.Clear();
            map.Symbols.Clear();
            map.Labels.Clear();
            map.Runways.Clear();
        }

        MMI.RequestRedraw(false, true);
    }

    private static void MMI_SectorsControlledChanged(object sender, EventArgs e)
    {
        Refresh();
    }

    private static void Refresh()
    {
        try
        {
            lock (Sync)
            {
                var map = GetOrCreateTargetMap();
                var lines = BuildBoundaryLines();

                map.Lines.Clear();
                map.Lines.AddRange(lines);

                map.Type = DisplayMaps.MapTypes.System2;
                map.Category = DisplayMaps.MapCategories.ASD;
                map.Pattern = DisplayMaps.Map.Patterns.Solid;
                map.Priority = 0;

                // OSEC is rendered as boundaries only.
                map.Infills.Clear();
                map.Symbols.Clear();
                map.Labels.Clear();
                map.Runways.Clear();

                DisplayMaps.Maps.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }

            MMI.RequestRedraw(false, true);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"DynamicSectorBoundaryRenderer refresh error: {ex.Message}", ex));
        }
    }

    private static DisplayMaps.Map GetOrCreateTargetMap()
    {
        var map = DisplayMaps.Maps.FirstOrDefault(m =>
            TargetMapNames.Any(n => string.Equals(m.Name, n, StringComparison.OrdinalIgnoreCase)));

        if (map != null) return map;

        map = new DisplayMaps.Map
        {
            Name = "OSec",
            Type = DisplayMaps.MapTypes.System2,
            Category = DisplayMaps.MapCategories.ASD,
            Pattern = DisplayMaps.Map.Patterns.Solid,
            Priority = 0
        };

        DisplayMaps.Maps.Add(map);
        return map;
    }

    private static List<DisplayMaps.Map.Line> BuildBoundaryLines()
    {
        var distinctBoundaries = new Dictionary<string, List<Coordinate>>();

        foreach (var sector in MMI.SectorsControlled.OrderBy(s => s.Name))
        {
            foreach (var volume in sector.Volumes
                         .Where(v => v != null)
                         .OrderBy(v => v.LowerLevel)
                         .ThenBy(v => v.UpperLevel))
            {
                var boundary = NormalizeBoundary(volume.Boundary);
                if (boundary.Count < 3) continue;

                var key = GetBoundaryKey(boundary);
                if (!distinctBoundaries.ContainsKey(key))
                    distinctBoundaries.Add(key, boundary);
            }
        }

        var edgeCounts = new Dictionary<string, EdgeData>(StringComparer.Ordinal);
        foreach (var boundary in distinctBoundaries.Values)
        {
            for (var index = 0; index < boundary.Count; index++)
            {
                var start = boundary[index];
                var end = boundary[(index + 1) % boundary.Count];
                var startKey = GetPointKey(start);
                var endKey = GetPointKey(end);

                if (startKey == endKey) continue;

                var edgeKey = GetEdgeKey(startKey, endKey);
                if (edgeCounts.TryGetValue(edgeKey, out var edge))
                {
                    edge.Count++;
                }
                else
                {
                    edgeCounts.Add(edgeKey, new EdgeData(startKey, endKey, start, end));
                }
            }
        }

        var remainingEdges = edgeCounts.Values.Where(edge => edge.Count == 1).ToList();
        if (remainingEdges.Count == 0) return new List<DisplayMaps.Map.Line>();

        var adjacency = new Dictionary<string, List<EdgeData>>(StringComparer.Ordinal);
        foreach (var edge in remainingEdges)
        {
            AddAdjacency(adjacency, edge.StartKey, edge);
            AddAdjacency(adjacency, edge.EndKey, edge);
        }

        var lines = new List<DisplayMaps.Map.Line>();
        foreach (var loop in BuildLoops(remainingEdges, adjacency))
        {
            var line = new DisplayMaps.Map.Line
            {
                Name = "OSEC",
                Pattern = DisplayMaps.Map.Patterns.Solid,
                Width = 1f
            };

            AddBoundary(line, loop);
            if (line.Points.Count > 1)
                lines.Add(line);
        }

        return lines;
    }

    private static List<List<Coordinate>> BuildLoops(
        List<EdgeData> edges,
        Dictionary<string, List<EdgeData>> adjacency)
    {
        var loops = new List<List<Coordinate>>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var edge in edges)
        {
            if (!visited.Add(edge.Key)) continue;

            var loop = new List<Coordinate> { edge.Start, edge.End };
            var startKey = edge.StartKey;
            var currentKey = edge.EndKey;
            var previousEdgeKey = edge.Key;

            while (!string.Equals(currentKey, startKey, StringComparison.Ordinal))
            {
                if (!adjacency.TryGetValue(currentKey, out var connectedEdges))
                {
                    break;
                }

                var nextEdge = connectedEdges.FirstOrDefault(candidate => candidate.Key != previousEdgeKey && !visited.Contains(candidate.Key));
                if (nextEdge == null)
                {
                    break;
                }

                visited.Add(nextEdge.Key);

                var nextKey = nextEdge.StartKey == currentKey ? nextEdge.EndKey : nextEdge.StartKey;
                var nextPoint = nextEdge.StartKey == currentKey ? nextEdge.End : nextEdge.Start;

                if (nextKey != startKey)
                    loop.Add(nextPoint);

                previousEdgeKey = nextEdge.Key;
                currentKey = nextKey;
            }

            if (loop.Count >= 3)
                loops.Add(loop);
        }

        return loops;
    }

    private static void AddAdjacency(Dictionary<string, List<EdgeData>> adjacency, string key, EdgeData edge)
    {
        if (!adjacency.TryGetValue(key, out var list))
        {
            list = new List<EdgeData>();
            adjacency.Add(key, list);
        }

        list.Add(edge);
    }

    private static List<Coordinate> NormalizeBoundary(IList<Coordinate> boundary)
    {
        var normalized = new List<Coordinate>();
        if (boundary == null) return normalized;

        foreach (var point in boundary)
        {
            if (point == null) continue;

            if (normalized.Count == 0 || GetPointKey(normalized[normalized.Count - 1]) != GetPointKey(point))
                normalized.Add(point);
        }

        if (normalized.Count > 1 && GetPointKey(normalized[0]) == GetPointKey(normalized[normalized.Count - 1]))
            normalized.RemoveAt(normalized.Count - 1);

        return normalized;
    }

    private static string GetBoundaryKey(IReadOnlyList<Coordinate> boundary)
    {
        var forward = boundary.Select(GetPointKey).ToList();
        var reverse = forward.AsEnumerable().Reverse().ToList();

        var bestForward = GetBestRotation(forward);
        var bestReverse = GetBestRotation(reverse);

        return string.CompareOrdinal(bestForward, bestReverse) <= 0 ? bestForward : bestReverse;
    }

    private static string GetBestRotation(IReadOnlyList<string> points)
    {
        var best = string.Join("|", points);
        for (var index = 1; index < points.Count; index++)
        {
            var rotated = points.Skip(index).Concat(points.Take(index));
            var candidate = string.Join("|", rotated);
            if (string.CompareOrdinal(candidate, best) < 0)
                best = candidate;
        }

        return best;
    }

    private static string GetEdgeKey(string pointA, string pointB)
    {
        return string.CompareOrdinal(pointA, pointB) <= 0
            ? pointA + "->" + pointB
            : pointB + "->" + pointA;
    }

    private static string GetPointKey(Coordinate point)
    {
        return Math.Round(point.Latitude, 8).ToString("F8", CultureInfo.InvariantCulture) + "," +
               Math.Round(point.Longitude, 8).ToString("F8", CultureInfo.InvariantCulture);
    }

    private static void AddBoundary(DisplayMaps.Map.Line line, IList<Coordinate> boundary)
    {
        if (boundary == null || boundary.Count < 2) return;

        foreach (var point in boundary)
        {
            if (line.Points.Any())
            {
                var greatCircleSegments = Conversions.CreateGreatCircleSegments(line.Points.Last(), point);
                if (greatCircleSegments.Any())
                {
                    line.Points.AddRange(greatCircleSegments);
                }
            }

            line.Points.Add(point);
        }

        var first = boundary[0];
        if (line.Points.Any())
        {
            var greatCircleSegments = Conversions.CreateGreatCircleSegments(line.Points.Last(), first);
            if (greatCircleSegments.Any())
            {
                line.Points.AddRange(greatCircleSegments);
            }
        }

        line.Points.Add(first);
    }

    private sealed class EdgeData
    {
        public EdgeData(string startKey, string endKey, Coordinate start, Coordinate end)
        {
            StartKey = startKey;
            EndKey = endKey;
            Start = start;
            End = end;
            Key = GetEdgeKey(startKey, endKey);
        }

        public string Key { get; }
        public string StartKey { get; }
        public string EndKey { get; }
        public Coordinate Start { get; }
        public Coordinate End { get; }
        public int Count { get; set; } = 1;
    }
}