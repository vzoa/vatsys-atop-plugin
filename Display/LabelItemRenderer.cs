#nullable enable
using vatsys;
using vatsys.Plugin;

namespace AuroraLabelItemsPlugin.Display;

public static class LabelItemRenderer
{
    public static CustomLabelItem? RenderLabelItem(string itemType, Track track, FDP2.FDR fdr, RDP.RadarTrack radarTrack)
    {
        return itemType switch
        {
            LabelConstants.LabelItemSelectHori => null,
            LabelConstants.LabelItemSelectVert => null,
            LabelConstants.LabelItemCommIcon => null,
            LabelConstants.LabelItemAdsbCpdlc => null,
            LabelConstants.LabelItemAdsFlags => null,
            LabelConstants.LabelItemMntFlags => null,
            LabelConstants.LabelItemScc => null,
            LabelConstants.LabelItemAnnotInd => null,
            LabelConstants.LabelItemRestr => null,
            LabelConstants.LabelItemLevel => null,
            LabelConstants.LabelItemVmi => null,
            LabelConstants.LabelItemClearedLevel => null,
            LabelConstants.LabelItemRadarInd => null,
            LabelConstants.LabelItemInhibitInd => null,
            LabelConstants.LabelItemFiledSpeed => null,
            LabelConstants.LabelItem3DigitGroundspeed => null,
            LabelConstants.LabelItemDestination => null,
            _ => null
        };
    }
}