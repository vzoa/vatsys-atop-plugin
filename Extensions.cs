using AuroraLabelItemsPlugin.State;
using vatsys;

namespace AuroraLabelItemsPlugin;

public static class Extensions
{
    public static AtopAircraftState GetAtopState(this FDP2.FDR fdr)
    {
        return AtopPluginStateManager.GetState(fdr.Callsign);
    }

    public static int? GetTransponderCode(this FDP2.FDR fdr)
    {
        return fdr.CoupledTrack?.ActualAircraft.TransponderCode;
    }

    public static bool IsSelected(this Track track)
    {
        return MMI.SelectedTrack == track;
    }
}