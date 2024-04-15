using vatsys;
using vatsys.Plugin;

namespace AtopPlugin.Display.Label;

public interface ILabelItem
{
    public string GetFieldKey();
    public CustomLabelItem? Render(FDP2.FDR fdr);
}