using vatsys;

namespace AtopPlugin.State;

public static class FdrUpdateProcessor
{
    public static void ProcessFdrUpdate(FDP2.FDR updated)
    {
        AtopPluginStateManager.ProcessFdrUpdate(updated);
        AtopPluginStateManager.ProcessDisplayUpdate(updated);
        AtopPluginStateManager.RunConflictProbe(updated);
        FdrPropertyChangesListener.RegisterHandler(updated);

        // don't manage jurisdiction if not connected as ATC
        if (Network.Me.IsRealATC) JurisdictionManager.HandleFdrUpdate(updated);
    }
}