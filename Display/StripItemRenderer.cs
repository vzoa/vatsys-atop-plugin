using AtopPlugin.Models;
using System.Collections.Generic;
using vatsys;
using vatsys.Plugin;
using static System.Net.Mime.MediaTypeNames;
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
                    Text = displayState.AdsFlag, BackColourIdentity = Colours.Identities.Custom,
                    CustomBackColour = CustomColors.SepFlags
                }
                : null,

            StripConstants.StripItemRvsmFlag => fdr.RVSM
                ? new CustomStripItem
                {
                    Text = Symbols.Rvsm, BackColourIdentity = Colours.Identities.Custom,
                    CustomBackColour = CustomColors.SepFlags
                }
                : null,

            StripConstants.StripItemVmi => new CustomStripItem { Text = displayState.AltitudeFlag?.Value ?? "" },

            StripConstants.StripItemComplex => displayState.IsRestrictionsIndicatorToggled
                ? new CustomStripItem() { Text = Symbols.ComplexFlag }
                : null,

            StripConstants.StripItemClearedLevel => new CustomStripItem { Text = displayState.ClearedLevel },

            StripConstants.StripItemRequestedLevel => new CustomStripItem { Text = displayState.RequestedLevel },

            StripConstants.StripItemPoint => RenderPointStripItem(fdr),

            StripConstants.StripItemRoute => new CustomStripItem { Text = Symbols.StripRouteItem },

            StripConstants.StripItemRadarInd => new CustomStripItem { Text = Symbols.StripRadarIndicator },

            StripConstants.StripItemAnnotInd => displayState.HasAnnotations
                ? new CustomStripItem { Text = Symbols.ScratchpadFlag }
                : new CustomStripItem { Text = Symbols.EmptyAnnotations },

            StripConstants.StripItemLateralFlag => !string.IsNullOrEmpty(displayState.LateralFlag)
                ? new CustomStripItem
                {
                    Text = displayState.LateralFlag, BackColourIdentity = Colours.Identities.Custom,
                    CustomBackColour = CustomColors.SepFlags
                }
                : null,

            StripConstants.StripItemRestr => displayState.IsRestrictionsIndicatorToggled
                ? new CustomStripItem { Text = Symbols.RestrictionsFlag }
                : null,

            _ => null
        };
    }

    private static CustomStripItem RenderPointStripItem(FDP2.FDR fdr)
    {

        StripItem stripItem = new StripItem();
        var customItem = new CustomStripItem { Text = "TEST" };
        for (var segment = 0; segment <= fdr.ParsedRoute.Count; ++segment)
        {
            string text = fdr.ParsedRoute[segment].Intersection.Name;
            //FDP2.FDR.ExtractedRoute.Segment segment = (FDP2.FDR.ExtractedRoute.Segment)null;

            if (Airspace2.GetIntersection(text, fdr.ParsedRoute[segment].Intersection.LatLong) == null)
            {
                text = Conversions.ConvertToReadableLatLongDDDMM(fdr.ParsedRoute[segment].Intersection.LatLong);
            }
            else if (fdr.ParsedRoute[segment].Intersection.Type == Airspace2.Intersection.Types.Unknown && fdr.ParsedRoute[segment].Intersection.FullName != "")
            {
                text = fdr.ParsedRoute[segment].Intersection.FullName;
            }

            if (item.PointIndexSpecified && rte.Count > item.PointIndex)
                segment = rte[item.PointIndex];

            customItem = new CustomStripItem { Text = text };

        }
        return customItem;

        //if (stripItem.PointIndexSpecified && fdr.ParsedRoute.Count > stripItem.PointIndex)
        //segment = fdr.ParsedRoute[stripItem.PointIndex];





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