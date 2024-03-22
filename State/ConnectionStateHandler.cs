using System;

namespace AtopPlugin.State;

public static class ConnectionStateHandler
{
    public static void HandleConnect(object sender, EventArgs eventArgs)
    {
        FdrUpdateProcessor.StartProcessing();
    }
    
    public static void HandleDisconnect(object sender, EventArgs eventArgs)
    {
        FdrUpdateProcessor.EndProcessing();
        AtopPluginStateManager.Reset();
    }
}