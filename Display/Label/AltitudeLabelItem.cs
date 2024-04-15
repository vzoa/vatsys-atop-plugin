using vatsys;
using vatsys.Plugin;

namespace AtopPlugin.Display.Label;

public class AltitudeLabelItem : ILabelItem
{
    public string GetFieldKey()
    {
        return LabelConstants.LabelItemAltitude;
    }

    public CustomLabelItem? Render(FDP2.FDR fdr)
    {
        return null;
    }
}