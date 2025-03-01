using AtopPlugin.Models;
using AtopPlugin.State;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using vatsys;
using vatsys.Plugin;
using static System.Net.Mime.MediaTypeNames;
using static vatsys.FDP2;
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

            StripConstants.StripItemLevel => new CustomStripItem { Text = displayState.CurrentLevel },

            StripConstants.StripItemVmi => new CustomStripItem { Text = displayState.AltitudeFlag?.Value ?? "" },

            StripConstants.StripItemComplex => displayState.IsRestrictionsIndicatorToggled
                ? new CustomStripItem() { Text = Symbols.ComplexFlag }
                : null,

            StripConstants.StripItemClearedLevel => new CustomStripItem { Text = displayState.ClearedLevel },

            StripConstants.StripItemRequestedLevel => new CustomStripItem { Text = displayState.RequestedLevel },

            StripConstants.StripItemPoint0 => RenderPointStripItem(fdr,0),
            StripConstants.StripItemPoint1 => RenderPointStripItem(fdr,1),
            StripConstants.StripItemPoint2 => RenderPointStripItem(fdr,2),
            StripConstants.StripItemPoint3 => RenderPointStripItem(fdr,3),
            StripConstants.StripItemPoint4 => RenderPointStripItem(fdr,4),
            StripConstants.StripItemPoint5 => RenderPointStripItem(fdr,5),
            StripConstants.StripItemPoint6 => RenderPointStripItem(fdr,6),
            StripConstants.StripItemPoint7 => RenderPointStripItem(fdr,7),
            StripConstants.StripItemPoint8 => RenderPointStripItem(fdr,8),
            StripConstants.StripItemPoint9 => RenderPointStripItem(fdr,9),
            StripConstants.StripItemPoint10=> RenderPointStripItem(fdr,10),
            StripConstants.StripItemPoint11=> RenderPointStripItem(fdr,11),
            StripConstants.StripItemPoint12=> RenderPointStripItem(fdr,12),
            StripConstants.StripItemPoint13 => RenderPointStripItem(fdr, 13),

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
    private static CustomStripItem RenderPointStripItem(FDP2.FDR fdr, int index = -1)
    {
        IList<FDP2.FDR.ExtractedRoute.Segment> rte = fdr.ParsedRoute;
        FDP2.FDR.ExtractedRoute.Segment segment = null;

        if (index >= 0 && index < rte.Count)
        {
            segment = rte[index];
        }
        else if (index == -1 && rte.Count > 0)
        {
            segment = rte.FirstOrDefault();
        }

        if (segment == null)
        {
            return new CustomStripItem { Text = string.Empty };
        }

        var text = segment.Intersection.Name ?? string.Empty;

       //// Check if the point is within the controlled sector
       //if (!JurisdictionManager.IsInControlledSector(segment.Intersection.LatLong, fdr.CFLUpper).Result)
       //{
       //    return new CustomStripItem { Text = string.Empty };
       //}

        // Handle Z-points by rendering their latitude and longitude
        if (segment.Type == Segment.SegmentTypes.ZPOINT)
        {
            text = Conversions.ConvertToReadableLatLongDDDMM(segment.Intersection.LatLong).ToString();
        }
        else if (Airspace2.GetIntersection(text, segment.Intersection.LatLong) == null)
        {
            text = Conversions.ConvertToReadableLatLongDDDMM(segment.Intersection.LatLong).ToString();
        }
        else if (segment.Intersection.Type == Airspace2.Intersection.Types.Unknown && segment.Intersection.FullName != "")
        {
            text = segment.Intersection.FullName;
        }

        return new CustomStripItem { Text = text };
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