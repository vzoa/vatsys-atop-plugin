using AtopPlugin.Models;
using System;
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
        if (pointIndex < 0 || pointIndex >= fdr.ParsedRoute.Count)
            return new CustomStripItem { Text = "" };

        var segment = fdr.ParsedRoute[pointIndex];

        // ZPOINTs are computed sector boundary crossings — always show as lat/lon
        if (segment.Type == Segment.SegmentTypes.ZPOINT)
            return new CustomStripItem { Text = FormatLat(segment.Intersection.LatLong) };

        var text = segment.Intersection.Name;

        // Unknown intersection (no match in airspace data) — show as lat/lon
        if (Airspace2.GetIntersection(text, segment.Intersection.LatLong) == null)
            text = FormatLat(segment.Intersection.LatLong);
        else if (segment.Intersection.Type == Airspace2.Intersection.Types.Unknown &&
                 !string.IsNullOrEmpty(segment.Intersection.FullName))
            text = segment.Intersection.FullName;

        return new CustomStripItem { Text = text };
    }

    private static CustomStripItem RenderPointLonStripItem(FDP2.FDR fdr, int pointIndex)
    {
        if (pointIndex < 0 || pointIndex >= fdr.ParsedRoute.Count)
            return new CustomStripItem { Text = "" };

        var segment = fdr.ParsedRoute[pointIndex];

        // ZPOINTs always show lon
        if (segment.Type == Segment.SegmentTypes.ZPOINT)
            return new CustomStripItem { Text = FormatLon(segment.Intersection.LatLong) };

        var name = segment.Intersection.Name;

        // Unknown intersection — show lon
        if (Airspace2.GetIntersection(name, segment.Intersection.LatLong) == null)
            return new CustomStripItem { Text = FormatLon(segment.Intersection.LatLong) };

        // Named waypoint — no lon line needed
        return new CustomStripItem { Text = "" };
    }

    internal static string FormatLat(Coordinate latLong)
    {
        var lat = latLong.Latitude;
        var latDir = lat >= 0 ? "N" : "S";
        lat = Math.Abs(lat);
        var latDeg = (int)lat;
        var latMin = (int)Math.Round((lat - latDeg) * 60);
        return $"{latDeg:D2}{latMin:D2}{latDir}";
    }

    internal static string FormatLon(Coordinate latLong)
    {
        var lon = latLong.Longitude;
        var lonDir = lon >= 0 ? "E" : "W";
        lon = Math.Abs(lon);
        var lonDeg = (int)lon;
        var lonMin = (int)Math.Round((lon - lonDeg) * 60);
        return $"{lonDeg:D3}{lonMin:D2}{lonDir}";
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
        var text = fdr.GetDisplayState()!.CpdlcAdsbSymbol;
        if (string.IsNullOrEmpty(text)) return null;

        var stripItem = GetStripItemWithColorsForDirection(fdr.GetAtopState()?.DirectionOfFlight);
        stripItem.Text = text;
        return stripItem;
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