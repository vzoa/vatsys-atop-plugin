#nullable enable
using AuroraLabelItemsPlugin.Models;
using vatsys;
using vatsys.Plugin;

namespace AuroraLabelItemsPlugin.Display;

public static class StripItemRenderer
{
    public static CustomStripItem? RenderStripItem(string itemType, Track track, FDP2.FDR? fdr,
        RDP.RadarTrack radarTrack)
    {
        if (fdr == null) return null;

        return itemType switch
        {
            StripConstants.StripItemCallsign => RenderCallsignStripItem(fdr),
            StripConstants.StripItemCtlsector => RenderCtlSectorStripItem(fdr),
            StripConstants.StripItemNxtsector => null,

            StripConstants.LabelItemAdsbCpdlc => null,

            StripConstants.StripItemT10Flag => fdr.PerformanceData.IsJet
                ? new CustomStripItem { Text = Symbols.T10 }
                : null,

            StripConstants.StripItemMntFlag => fdr.PerformanceData.IsJet
                ? new CustomStripItem { Text = Symbols.Mnt }
                : null,

            StripConstants.StripItemDistFlag => null,

            StripConstants.StripItemRvsmFlag => fdr.RVSM
                ? new CustomStripItem
                {
                    Text = Symbols.Rvsm, BackColourIdentity = Colours.Identities.Custom,
                    CustomBackColour = CustomColors.SepFlags,
                }
                : null,

            StripConstants.StripItemVmi => null,

            StripConstants.StripItemComplex => null,

            StripConstants.StripItemClearedLevel => null,

            StripConstants.StripItemRequestedLevel => null,

            StripConstants.StripItemRoute => null,

            StripConstants.StripItemRadarInd => null,

            StripConstants.StripItemAnnotInd => null,

            StripConstants.StripItemLateralFlag => null,

            StripConstants.StripItemRestr => null,

            _ => null
        };
    }

    private static CustomStripItem RenderCallsignStripItem(FDP2.FDR fdr)
    {
        var stripItem = GetStripItemWithColorsForDirection(fdr.GetAtopState().DirectionOfFlight);
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

    private static CustomStripItem GetStripItemWithColorsForDirection(DirectionOfFlight directionOfFlight)
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