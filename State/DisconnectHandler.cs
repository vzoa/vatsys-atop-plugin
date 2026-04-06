using System;
using vatsys;

namespace AtopPlugin.State;

public static class DisconnectHandler
{
    public static void Handle(object sender, EventArgs eventArgs)
    {
        try
        {
            AtopPluginStateManager.Reset();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"DisconnectHandler: {ex.Message}", ex));
        }
    }
}