using vatsys;
using vatsys.Plugin;

namespace AuroraLabelItemsPlugin.Display;

public static class StripItemRenderer
{
    public static CustomStripItem RenderStripItem(string itemType, Track track, FDP2.FDR fdr, RDP.RadarTrack radarTrack)
    {
        return itemType switch
        {
            StripConstants.StripItemCallsign => null,
            StripConstants.StripItemCtlsector => null,
            StripConstants.StripItemNxtsector => null,
            StripConstants.LabelItemAdsbCpdlc => null,
            StripConstants.StripItemT10Flag => null,
            StripConstants.StripItemMntFlag => null,
            StripConstants.StripItemDistFlag => null,
            StripConstants.StripItemRvsmFlag => null,
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
}