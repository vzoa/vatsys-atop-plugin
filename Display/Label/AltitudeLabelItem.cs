using AtopPlugin.State;
using vatsys;
using vatsys.Plugin;
using vatsys_atop_plugin.UI;

namespace AtopPlugin.Display.Label;

public class AltitudeLabelItem : ILabelItem
{
    public string GetFieldKey()
    {
        return LabelConstants.LabelItemAltitude;
    }

    public CustomLabelItem Render(FDP2.FDR fdr, AtopAircraftDisplayState displayState, AtopAircraftState atopState)
    {
        var text = displayState.CurrentLevel.PadLeft(3) +
                   (displayState.AltitudeFlag?.Value ?? Symbols.Empty).PadLeft(1) +
                   displayState.ClearedLevel.PadLeft(3);

        var labelItem = new CustomLabelItem
        {
            Text = text,
            Border = displayState.AltitudeBorderFlags,
            BorderColourIdentity = Colours.Identities.Custom,
            CustomBorderColour = CustomColors.NotCda,
            OnMouseClick = AltitudeWindow.Handle
        };

        if (displayState.AltitudeColor == null) return labelItem;

        labelItem.ForeColourIdentity = Colours.Identities.Custom;
        labelItem.CustomForeColour = displayState.AltitudeColor;

        return labelItem;
    }
}