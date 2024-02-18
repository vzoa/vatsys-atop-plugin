#nullable enable
using AuroraLabelItemsPlugin.State;
using vatsys;
using vatsys.Plugin;

namespace AuroraLabelItemsPlugin.Display;

public static class LabelItemRenderer
{
    public static CustomLabelItem? RenderLabelItem(string itemType, Track track, FDP2.FDR? fdr,
        RDP.RadarTrack _)
    {
        if (fdr == null) return null;

        return itemType switch
        {
            LabelConstants.LabelItemSelectHori => track.IsSelected()
                ? new CustomLabelItem() { Text = Symbols.Empty, Border = BorderFlags.Bottom }
                : null,

            LabelConstants.LabelItemSelectVert => track.IsSelected()
                ? new CustomLabelItem() { Text = Symbols.Empty, Border = BorderFlags.Left }
                : null,

            LabelConstants.LabelItemCommIcon => fdr.GetAtopState().DownlinkIndicator
                ? new CustomLabelItem() { Text = Symbols.CommDownlink, Border = BorderFlags.All }
                : new CustomLabelItem() { Text = Symbols.CommEmpty },

            LabelConstants.LabelItemAdsbCpdlc => null,

            LabelConstants.LabelItemAdsFlags => null,

            LabelConstants.LabelItemMntFlags => null,

            LabelConstants.LabelItemScc => fdr.GetAtopState().HighestSccFlag != null
                ? new CustomLabelItem()
                {
                    Text = fdr.GetAtopState().HighestSccFlag!.Value, ForeColourIdentity = Colours.Identities.Custom,
                    CustomForeColour = CustomColors.SpecialConditionCode
                }
                : null,

            LabelConstants.LabelItemAnnotInd => null,

            LabelConstants.LabelItemRestr => null,

            LabelConstants.LabelItemLevel => null,

            LabelConstants.LabelItemVmi => null,

            LabelConstants.LabelItemClearedLevel => null,

            LabelConstants.LabelItemRadarInd => fdr.GetAtopState().RadarToggleIndicator
                ? new CustomLabelItem() { Text = Symbols.RadarFlag, OnMouseClick = RadarFlagToggleHandler.Handle }
                : new CustomLabelItem() { Text = Symbols.UntoggledFlag, OnMouseClick = RadarFlagToggleHandler.Handle },

            LabelConstants.LabelItemInhibitInd => fdr.State == FDP2.FDR.FDRStates.STATE_INHIBITED
                ? new CustomLabelItem() { Text = Symbols.Inhibited }
                : null,

            LabelConstants.LabelItemFiledSpeed => null,

            LabelConstants.LabelItem3DigitGroundspeed => null,

            LabelConstants.LabelItemDestination => new CustomLabelItem() { Text = fdr.DesAirport },

            _ => null
        };
    }
}