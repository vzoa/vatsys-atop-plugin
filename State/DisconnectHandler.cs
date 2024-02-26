using System;

namespace AtopPlugin.State;

public static class DisconnectHandler
{
    public static void Handle(object sender, EventArgs eventArgs)
    {
        AtopPluginStateManager.Reset();
    }
}