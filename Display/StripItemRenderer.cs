using AtopPlugin.Helpers;
using AtopPlugin.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using vatsys;
using vatsys.Plugin;
using static vatsys.FDP2.FDR.ExtractedRoute;

namespace AtopPlugin.Display;

public static class StripItemRenderer
{
    public static CustomStripItem? RenderStripItem(string itemType, Track track, FDP2.FDR? fdr,
        RDP.RadarTrack radarTrack)
    {
        if (fdr?.GetAtopState() == null || fdr.GetDisplayState() == null) return null;

        var atopState = fdr.GetAtopState()!;
        var displayState = fdr.GetDisplayState()!;

        // Handle indexed point items (AURORA_POINT_0 through AURORA_POINT_N)
        if (itemType.StartsWith(StripConstants.StripItemPointLonPrefix))
        {
            var indexStr = itemType.Substring(StripConstants.StripItemPointLonPrefix.Length);
            if (int.TryParse(indexStr, out var pointIndex))
                return RenderPointLonStripItem(fdr, pointIndex);
            return null;
        }
        if (itemType.StartsWith(StripConstants.StripItemEtopPrefix))
        {
            var indexStr = itemType.Substring(StripConstants.StripItemEtopPrefix.Length);
            if (int.TryParse(indexStr, out var pointIndex))
                return RenderEtopStripItem(fdr, pointIndex);
            return null;
        }
        if (itemType.StartsWith(StripConstants.StripItemPointPrefix))
        {
            var indexStr = itemType.Substring(StripConstants.StripItemPointPrefix.Length);
            if (int.TryParse(indexStr, out var pointIndex))
                return RenderPointStripItem(fdr, pointIndex);
            return null;
        }

        return itemType switch
        {
            StripConstants.StripItemCallsign => RenderCallsignStripItem(fdr),

            StripConstants.StripItemCtlsector => RenderCtlSectorStripItem(fdr),

            StripConstants.StripItemNxtsector => new CustomStripItem { Text = atopState.NextSector?.Name ?? "" },

            StripConstants.LabelItemAdsbCpdlc => RenderAdsbCpdlcStripItem(fdr),

            StripConstants.StripItemT10Flag => fdr.IsJet()
                ? new CustomStripItem { Text = Symbols.T10 }
                : null,

            StripConstants.StripItemMntFlag => fdr.IsJet()
                ? new CustomStripItem { Text = Symbols.Mnt }
                : null,

            StripConstants.StripItemDistFlag => !string.IsNullOrEmpty(displayState.AdsFlag)
                ? new CustomStripItem
                {
                    Text = displayState.AdsFlag,
                    BackColourIdentity = Colours.Identities.Custom,
                    CustomBackColour = CustomColors.SepFlags
                }
                : null,

            StripConstants.StripItemRvsmFlag => fdr.RVSM
                ? new CustomStripItem
                {
                    Text = Symbols.Rvsm,
                    BackColourIdentity = Colours.Identities.Custom,
                    CustomBackColour = CustomColors.SepFlags
                }
                : null,

            StripConstants.StripItemVmi => new CustomStripItem { Text = displayState.AltitudeFlag?.Value ?? "" },

            StripConstants.StripItemComplex => displayState.IsRestrictionsIndicatorToggled
                ? new CustomStripItem() { Text = Symbols.ComplexFlag }
                : null,

            StripConstants.StripItemClearedLevel => new CustomStripItem { Text = displayState.ClearedLevel },

            StripConstants.StripItemRequestedLevel => new CustomStripItem { Text = displayState.RequestedLevel },

            StripConstants.StripItemPoint => RenderPointStripItem(fdr, 0),

            StripConstants.StripItemRoute => new CustomStripItem { Text = Symbols.StripRouteItem },

            StripConstants.StripItemRadarInd => new CustomStripItem { Text = Symbols.StripRadarIndicator },

            StripConstants.StripItemAnnotInd => displayState.HasAnnotations
                ? new CustomStripItem { Text = Symbols.ScratchpadFlag }
                : new CustomStripItem { Text = Symbols.EmptyAnnotations },

            StripConstants.StripItemLateralFlag => !string.IsNullOrEmpty(displayState.LateralFlag)
                ? new CustomStripItem
                {
                    Text = displayState.LateralFlag,
                    BackColourIdentity = Colours.Identities.Custom,
                    CustomBackColour = CustomColors.SepFlags
                }
                : null,

            StripConstants.StripItemRestr => displayState.IsRestrictionsIndicatorToggled
                ? new CustomStripItem { Text = Symbols.RestrictionsFlag }
                : null,

            _ => null
        };
    }

    private static CustomStripItem RenderPointStripItem(FDP2.FDR fdr, int pointIndex)
    {
        var filtered = GetSectorFilteredRoute(fdr);

        if (pointIndex < 0 || pointIndex >= filtered.Count)
            return new CustomStripItem { Text = "" };

        var segment = filtered[pointIndex];
        CustomStripItem item;

        // ZPOINTs are computed sector boundary crossings — always show as lat/lon
        if (segment.Type == Segment.SegmentTypes.ZPOINT)
        {
            item = new CustomStripItem { Text = FormatLat(segment.Intersection.LatLong) };
        }
        else
        {
            var text = segment.Intersection.Name;

            // Unknown intersection (no match in airspace data) — show as lat
            if (Airspace2.GetIntersection(text, segment.Intersection.LatLong) == null)
                text = FormatLat(segment.Intersection.LatLong);
            else if (segment.Intersection.Type == Airspace2.Intersection.Types.Unknown &&
                     !string.IsNullOrEmpty(segment.Intersection.FullName))
                text = segment.Intersection.FullName;

            item = new CustomStripItem { Text = text };
        }

        ApplyPointColors(item, fdr, segment);
        return item;
    }

    private static CustomStripItem RenderPointLonStripItem(FDP2.FDR fdr, int pointIndex)
    {
        var filtered = GetSectorFilteredRoute(fdr);

        if (pointIndex < 0 || pointIndex >= filtered.Count)
            return new CustomStripItem { Text = "" };

        var segment = filtered[pointIndex];
        CustomStripItem item;

        if (segment.Type == Segment.SegmentTypes.ZPOINT)
            item = new CustomStripItem { Text = FormatLon(segment.Intersection.LatLong) };
        else if (Airspace2.GetIntersection(segment.Intersection.Name, segment.Intersection.LatLong) == null)
            item = new CustomStripItem { Text = FormatLon(segment.Intersection.LatLong) };
        else
            item = new CustomStripItem { Text = "" };

        ApplyPointColors(item, fdr, segment);
        return item;
    }

    private static CustomStripItem RenderEtopStripItem(FDP2.FDR fdr, int pointIndex)
    {
        var filtered = GetSectorFilteredRoute(fdr);

        if (pointIndex < 0 || pointIndex >= filtered.Count)
            return new CustomStripItem { Text = "" };

        var segment = filtered[pointIndex];
        var eto = segment.ETO;

        CustomStripItem item;
        if (eto == DateTime.MaxValue || eto == default)
            item = new CustomStripItem { Text = "" };
        else
            item = new CustomStripItem { Text = eto.ToString("HHmm") };

        ApplyPointColors(item, fdr, segment);
        return item;
    }

    /// <summary>
    /// Returns the subset of ParsedRoute relevant to the controller's sector,
    /// scrolled so point 0 is the last passed fix and point 1+ are upcoming.
    /// ZPOINTs that duplicate an adjacent filed fix (same rendered text) are removed
    /// per ATOP spec 10.9.3.3 Boundary Fixes.
    /// </summary>
    private static List<Segment> GetSectorFilteredRoute(FDP2.FDR fdr)
    {
        var route = fdr.ParsedRoute.ToList();
        if (route.Count == 0) return route;

        var mySectors = MMI.SectorsControlled;
        if (mySectors == null || mySectors.Count == 0) return route;

        // Walk route tracking sector transitions via ZPOINT tags.
        var inSector = new bool[route.Count];
        bool currentlyInSector = mySectors.Contains(fdr.ControllingSector);

        for (int i = 0; i < route.Count; i++)
        {
            var seg = route[i];
            if (seg.Type == Segment.SegmentTypes.ZPOINT && seg.Tag != null)
            {
                currentlyInSector = mySectors.Any(s =>
                    ((IEnumerable<object>)s.Volumes).Contains(seg.Tag));
            }
            inSector[i] = currentlyInSector;
        }

        // Find first and last in-sector indices
        int firstIn = -1, lastIn = -1;
        for (int i = 0; i < route.Count; i++)
        {
            if (inSector[i])
            {
                if (firstIn == -1) firstIn = i;
                lastIn = i;
            }
        }

        if (firstIn == -1) return route;

        // Extend 1 waypoint outside on each side
        int startIdx = firstIn > 0 ? firstIn - 1 : 0;
        int endIdx = lastIn < route.Count - 1 ? lastIn + 1 : route.Count - 1;

        var subset = route.GetRange(startIdx, endIdx - startIdx + 1);

        // Remove redundant ZPOINTs per ATOP 10.9.3.3:
        // If a ZPOINT renders the same lat/lon text as an adjacent filed fix, remove it.
        var deduped = subset.Where((seg, idx) =>
        {
            if (seg.Type != Segment.SegmentTypes.ZPOINT) return true;

            var zText = FormatLat(seg.Intersection.LatLong) + FormatLon(seg.Intersection.LatLong);

            // Check previous neighbor
            if (idx > 0)
            {
                var prev = subset[idx - 1];
                var prevText = FormatLat(prev.Intersection.LatLong) + FormatLon(prev.Intersection.LatLong);
                if (zText == prevText) return false;
            }

            // Check next neighbor
            if (idx < subset.Count - 1)
            {
                var next = subset[idx + 1];
                var nextText = FormatLat(next.Intersection.LatLong) + FormatLon(next.Intersection.LatLong);
                if (zText == nextText) return false;
            }

            return true;
        }).ToList();

        // Scroll using OverflownIndex, same as vatSys's PaintStrip:
        // Find the last overflown segment within our filtered list, start from there.
        int overflownIndex = fdr.ParsedRoute.OverflownIndex;
        if (overflownIndex > 0)
        {
            // Map the global overflown index to our deduped list.
            // Find the last segment in deduped whose original route index <= overflownIndex.
            int lastOverflown = -1;
            for (int i = 0; i < deduped.Count; i++)
            {
                int origIdx = route.IndexOf(deduped[i]);
                if (origIdx <= overflownIndex)
                    lastOverflown = i;
            }

            if (lastOverflown > 0)
                deduped = deduped.Skip(lastOverflown).ToList();
        }

        return deduped;
    }

    internal static string FormatLat(Coordinate latLong)
    {
        var lat = Math.Abs(latLong.Latitude);
        var latDir = latLong.Latitude >= 0 ? "N" : "S";
        var latDeg = (int)lat;
        var latMin = (int)Math.Round((lat - latDeg) * 60);
        return latMin == 0
            ? $"{latDeg:D2}{latDir}"
            : $"{latDeg:D2}{latMin:D2}{latDir}";
    }

    internal static string FormatLon(Coordinate latLong)
    {
        var lon = Math.Abs(latLong.Longitude);
        var lonDir = latLong.Longitude >= 0 ? "E" : "W";
        var lonDeg = (int)lon;
        var lonMin = (int)Math.Round((lon - lonDeg) * 60);
        return lonMin == 0
            ? $"{lonDeg:D3}{lonDir}"
            : $"{lonDeg:D3}{lonMin:D2}{lonDir}";
    }

    private static CustomStripItem RenderCallsignStripItem(FDP2.FDR fdr)
    {
        var stripItem = GetStripItemWithColorsForDirection(fdr.GetAtopState()?.DirectionOfFlight);
        stripItem.Text = fdr.Callsign;
        return stripItem;
    }

    private static CustomStripItem RenderCtlSectorStripItem(FDP2.FDR fdr)
    {
        var pendingCoordination =
            fdr.State is FDP2.FDR.FDRStates.STATE_PREACTIVE or FDP2.FDR.FDRStates.STATE_COORDINATED;
        var sectorName = fdr.ControllingSector?.Name ?? Symbols.Empty;

        var stripItem = new CustomStripItem { Text = sectorName };

        if (pendingCoordination)
        {
            stripItem.ForeColourIdentity = Colours.Identities.Custom;
            stripItem.CustomForeColour = CustomColors.Pending;
        }

        return stripItem;
    }

    private static CustomStripItem? RenderAdsbCpdlcStripItem(FDP2.FDR fdr)
    {
        if (!CpdlcPluginBridge.IsAvailable) return null;

        var text = fdr.GetDisplayState()!.CpdlcAdsbSymbol;
        if (string.IsNullOrEmpty(text)) return null;

        var connState = CpdlcPluginBridge.GetConnectionState(fdr.Callsign);
        if (connState == CpdlcPluginBridge.CpdlcConnectionState.CurrentDataAuthority)
        {
            var stripItem = GetStripItemWithColorsForDirection(fdr.GetAtopState()?.DirectionOfFlight);
            stripItem.Text = text;
            return stripItem;
        }

        return new CustomStripItem
        {
            Text = text,
            ForeColourIdentity = Colours.Identities.Custom,
            CustomForeColour = CustomColors.NotCda
        };
    }

    private static readonly ConcurrentDictionary<string, byte> _acknowledgedOverdue = new();

    private static bool IsSegmentOverflown(FDP2.FDR fdr, Segment segment)
    {
        int overflownIndex = fdr.ParsedRoute.OverflownIndex;
        if (overflownIndex <= 0) return false;

        int origIdx = fdr.ParsedRoute.ToList().IndexOf(segment);
        return origIdx >= 0 && origIdx <= overflownIndex;
    }

    private static bool IsSegmentOverdue(Segment segment)
    {
        if (!segment.IsPETO || !segment.MPRArmed || segment.ATO != DateTime.MaxValue)
            return false;
        if (segment.ETO == DateTime.MaxValue || segment.ETO == default)
            return false;
        return (DateTime.UtcNow - segment.ETO).TotalSeconds > 180.0;
    }

    private static string GetOverdueKey(FDP2.FDR fdr, Segment segment)
    {
        return $"{fdr.Callsign}_{segment.Intersection.Name}_{segment.ETO:HHmmss}";
    }

    private static void ApplyPointColors(CustomStripItem item, FDP2.FDR fdr, Segment segment)
    {
        bool overflown = IsSegmentOverflown(fdr, segment);
        bool overdue = IsSegmentOverdue(segment);
        string key = GetOverdueKey(fdr, segment);

        item.ForeColourIdentity = Colours.Identities.Custom;

        if (overdue)
        {
            if (_acknowledgedOverdue.ContainsKey(key))
            {
                item.CustomForeColour = CustomColors.OverdueGreen;
            }
            else
            {
                bool flash = DateTime.UtcNow.Second % 2 == 0;
                item.CustomForeColour = flash ? CustomColors.OverdueYellow : CustomColors.OverdueGreen;
                item.OnMouseClick = _ => _acknowledgedOverdue.TryAdd(key, 0);
            }
        }
        else
        {
            _acknowledgedOverdue.TryRemove(key, out _);
            item.CustomForeColour = overflown ? CustomColors.OverflownGrey : CustomColors.Black;
        }
    }

    private static CustomStripItem GetStripItemWithColorsForDirection(DirectionOfFlight? directionOfFlight)
    {
        var foreColorIdentity = directionOfFlight == DirectionOfFlight.Eastbound
            ? Colours.Identities.StripBackground
            : Colours.Identities.StripText;
        var backColorIdentity = directionOfFlight == DirectionOfFlight.Eastbound
            ? Colours.Identities.StripText
            : Colours.Identities.StripBackground;
        return new CustomStripItem
        {
            ForeColourIdentity = foreColorIdentity,
            BackColourIdentity = backColorIdentity
        };
    }
}