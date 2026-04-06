using System;
using vatsys;

namespace AtopPlugin.State;

public static class PrivateMessagesChangedHandler
{
    public static void Handle(object sender, Network.GenericMessageEventArgs eventArgs)
    {
        try
        {
            var extendedFdrState = AtopPluginStateManager.GetAircraftState(eventArgs.Message.Address);
            if (extendedFdrState != null) extendedFdrState.DownlinkIndicator = !eventArgs.Message.Sent;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"PrivateMessagesChangedHandler: {ex.Message}", ex));
        }
    }
}