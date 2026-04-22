using System;
using AtopPlugin.Display;
using vatsys;

namespace AtopPlugin.State;

public static class DisconnectHandler
{
    public static void Handle(object sender, EventArgs eventArgs)
    {
        try
        {
            ProbeRouteRenderer.ClearAll();
            DynamicSectorBoundaryRenderer.Reset();
            AtopPluginStateManager.Reset();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"DisconnectHandler: {ex.Message}", ex));
        }
    }
}