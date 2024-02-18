using System.Collections.Concurrent;
using vatsys;

namespace AuroraLabelItemsPlugin.State;

public static class AtopPluginStateManager
{
    
    private static readonly ConcurrentDictionary<string, AtopAircraftState> ExtendedFdrStates = new();

    public static AtopAircraftState GetState(string callsign)
    {
        var found = ExtendedFdrStates.TryGetValue(callsign, out var state);
        return found ? state : null;
    }

    public static void UpdateState(FDP2.FDR updated)
    {
        var callsign = updated.Callsign;
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