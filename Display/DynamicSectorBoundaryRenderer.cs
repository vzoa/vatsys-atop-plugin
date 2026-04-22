using System;
using System.Collections.Generic;
using System.Linq;
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
        var lines = new List<DisplayMaps.Map.Line>();
        var seenVolumes = new HashSet<SectorsVolumes.Volume>();

        foreach (var sector in MMI.SectorsControlled.OrderBy(s => s.Name).ToList())
        {
            foreach (var volume in sector.Volumes
                         .Where(v => v != null)
                         .OrderBy(v => v.LowerLevel)
                         .ThenBy(v => v.UpperLevel)
                         .ToList())
            {
                if (!seenVolumes.Add(volume)) continue;

                var line = new DisplayMaps.Map.Line
                {
                    Name = $"{sector.Name} FL{volume.LowerLevel:D3}-{volume.UpperLevel:D3}",
                    Pattern = DisplayMaps.Map.Patterns.Solid,
                    Width = 1f
                };

                AddBoundary(line, volume.Boundary);

                if (line.Points.Count > 1)
                {
                    lines.Add(line);
                }
            }
        }

        return lines;
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
}