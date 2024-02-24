using System.Collections.Concurrent;
using vatsys;

namespace AuroraLabelItemsPlugin.State;

public static class AtopPluginStateManager
{

    private const int MissingFromFdpState = -1;
    
    private static readonly ConcurrentDictionary<string, AtopAircraftState> ExtendedFdrStates = new();

    public static AtopAircraftState GetState(string callsign)
    {
        var found = ExtendedFdrStates.TryGetValue(callsign, out var state);
        return found ? state : null;
    }

    public static void ProcessStateUpdate(FDP2.FDR updated)
    {
        var callsign = updated.Callsign;

        if (FDP2.GetFDRIndex(callsign) == MissingFromFdpState)
        {
            ExtendedFdrStates.TryRemove(callsign, out _);
            return;
        }
        
        var extendedFdrState = GetState(callsign);

        if (extendedFdrState == null)
        {
            ExtendedFdrStates.TryAdd(callsign, new AtopAircraftState(updated));
        }
        else
        {
            extendedFdrState.UpdateFromFdr(updated);
        }
    }
}