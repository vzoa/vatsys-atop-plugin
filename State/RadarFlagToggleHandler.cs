using System;
using vatsys;
using vatsys.Plugin;

namespace AtopPlugin.State;

public static class RadarFlagToggleHandler
{
    public static void Handle(CustomLabelItemMouseClickEventArgs eventArgs)
    {
        try
        {
            var atopState = eventArgs.Track.GetFDR().GetAtopState();
            if (atopState != null) atopState.RadarToggleIndicator = !atopState.RadarToggleIndicator;
            eventArgs.Handled = true;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"RadarFlagToggleHandler: {ex.Message}", ex));
        }
    }
}