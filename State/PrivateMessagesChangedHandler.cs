using vatsys;

namespace AtopPlugin.State;

public static class PrivateMessagesChangedHandler
{
    public static void Handle(object sender, Network.GenericMessageEventArgs eventArgs)
    {
        var extendedFdrState = AtopPluginStateManager.GetAircraftState(eventArgs.Message.Address);
        if (extendedFdrState != null) extendedFdrState.DownlinkIndicator = !eventArgs.Message.Sent;
    }
}