using System.Threading;
using System.Windows.Forms;
using AtopPlugin.State;
using vatsys;
using vatsys.Plugin;
using vatsys_atop_plugin.UI;

namespace AtopPlugin.UI;

public static class AtopMenu
{
    private const string CategoryName = "ATOP";

    private static readonly SettingsWindow SettingsWindow = new();
    private static readonly ToolStripMenuItem ActivationToggle = new("Activate");
    private static readonly ConflictSummaryWindow ConflictSummaryWindow = new();
    //private static readonly AltitudeWindow AltitudeWindow = new();

    static AtopMenu()
    {
        InitializeActivationToggle();
        InitializeSettingsMenu();
        InitializeVersionItem();
        //InitializeConflictSummaryWindow();
        //InitializeAltitudeWindow();
    }

    // empty method to force static class initialization to happen
    public static void Initialize()
    {
    }

    //private static void InitializeAltitudeWindow()
    //{
    //    var altitudeWindowItem = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main,
    //        CustomToolStripMenuItemCategory.Custom, new ToolStripMenuItem("Altitude Menu"))
    //    {
    //        CustomCategoryName = CategoryName
    //    };
    //    altitudeWindowItem.Item.Click += (_, _) => MMI.InvokeOnGUI(AltitudeWindow.Show);
    //    MMI.AddCustomMenuItem(altitudeWindowItem);
    //}
    private static void InitializeConflictSummaryWindow()
    {
        var conflictWindowItem = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main,
            CustomToolStripMenuItemCategory.Custom, new ToolStripMenuItem("Conflict Summary") { Enabled = false })
        {
            CustomCategoryName = CategoryName
        };
        conflictWindowItem.Item.Click += (_, _) => MMI.InvokeOnGUI(ConflictSummaryWindow.Show);
        MMI.AddCustomMenuItem(conflictWindowItem);
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