using vatsys;
using AtopPlugin.Helpers;

namespace AtopPlugin.UI;

public partial class SettingsWindow : BaseForm
{
    public SettingsWindow()
    {
        InitializeComponent();
        MeartsUiFonts.Apply(this);
    }
}