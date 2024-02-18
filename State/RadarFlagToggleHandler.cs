using vatsys.Plugin;

namespace AuroraLabelItemsPlugin.State;

public static class RadarFlagToggleHandler
{
    public static void Handle(CustomLabelItemMouseClickEventArgs eventArgs)
    {
        var atopState = eventArgs.Track.GetFDR().GetAtopState();
        atopState.RadarToggleIndicator = !atopState.RadarToggleIndicator;
        eventArgs.Handled = true;
    }
}