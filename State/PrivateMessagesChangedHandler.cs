using vatsys;

namespace AuroraLabelItemsPlugin.State;

public static class PrivateMessagesChangedHandler
{
    public static void Handle(object sender, Network.GenericMessageEventArgs eventArgs)
    {
        var extendedFdrState = AtopPluginStateManager.GetState(eventArgs.Message.Address);
        extendedFdrState.DownlinkIndicator = !eventArgs.Message.Sent;
    }
}