using AtopPlugin.Models;
using vatsys;
using vatsys.Plugin;

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

            StripConstants.StripItemVmi => new CustomStripItem { Text = atopState.AltitudeFlag?.Value ?? "" },

            StripConstants.StripItemComplex => displayState.IsRestrictionsIndicatorToggled
                ? new CustomStripItem() { Text = Symbols.ComplexFlag }
                : null,

            StripConstants.StripItemClearedLevel => new CustomStripItem { Text = displayState.ClearedLevel },

            StripConstants.StripItemRequestedLevel => new CustomStripItem { Text = displayState.RequestedLevel },

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