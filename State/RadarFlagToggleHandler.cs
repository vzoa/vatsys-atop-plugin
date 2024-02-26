using vatsys.Plugin;

namespace AtopPlugin.State;

public static class RadarFlagToggleHandler
{
    public static void Handle(CustomLabelItemMouseClickEventArgs eventArgs)
    {
        var atopState = eventArgs.Track.GetFDR().GetAtopState();
        if (atopState != null) atopState.RadarToggleIndicator = !atopState.RadarToggleIndicator;
        eventArgs.Handled = true;
    }
}