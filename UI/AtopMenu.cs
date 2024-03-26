using System.Drawing;
using System.Windows.Forms;
using AtopPlugin.State;
using vatsys;
using vatsys.Plugin;

namespace AtopPlugin.UI;

public static class AtopMenu
{
    private const string CategoryName = "ATOP";

    private static readonly SettingsWindow SettingsWindow = new();
    private static readonly ToolStripMenuItem ActivationToggle = new("Activate");

    static AtopMenu()
    {
        InitializeActivationToggle();
        InitializeSettingsMenu();
        InitializeVersionItem();
    }

    // empty method to force static class initialization to happen
    public static void Initialize()
    {
    }

    private static void InitializeSettingsMenu()
    {
        var settingsMenuItem = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main,
            CustomToolStripMenuItemCategory.Custom, new ToolStripMenuItem("Settings"))
        {
            CustomCategoryName = CategoryName
        };
        settingsMenuItem.Item.Click += (_, _) => MMI.InvokeOnGUI(SettingsWindow.Show);
        MMI.AddCustomMenuItem(settingsMenuItem);
    }

    private static void InitializeActivationToggle()
    {
        var activationMenuItem = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main,
            CustomToolStripMenuItemCategory.Custom, ActivationToggle)
        {
            CustomCategoryName = CategoryName
        };
        activationMenuItem.Item.Click += (_, _) => MMI.InvokeOnGUI(AtopPluginStateManager.ToggleActivated);
        MMI.AddCustomMenuItem(activationMenuItem);
    }

    private static void InitializeVersionItem()
    {
        var stripMenuItem = new ToolStripMenuItem(Version.GetVersionString())
        {
            Enabled = false,
            RightToLeft = RightToLeft.Yes
        };
        var versionMenuItem = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main,
            CustomToolStripMenuItemCategory.Custom, stripMenuItem)
        {
            CustomCategoryName = CategoryName
        };
        MMI.AddCustomMenuItem(versionMenuItem);
    }

    public static void SetActivationState(bool state)
    {
        ActivationToggle.Checked = state;
    }
}