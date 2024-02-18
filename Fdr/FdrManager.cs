using System.Collections.Concurrent;
using vatsys;

namespace AuroraLabelItemsPlugin.Fdr;

public static class FdrManager
{
    
    private static readonly ConcurrentDictionary<string, ExtendedFdrState> ExtendedFdrStates = new();

    public static ExtendedFdrState GetExtendedFdrState(string callsign)
    {
        var found = ExtendedFdrStates.TryGetValue(callsign, out var state);
        return found ? state : null;
    }

    public static void UpdateFdrState(FDP2.FDR updated)
    {
        var callsign = updated.Callsign;
        var extendedFdrState = GetExtendedFdrState(callsign);

        if (extendedFdrState == null)
        {
            ExtendedFdrStates.TryAdd(callsign, new ExtendedFdrState(updated));
        }
        else
        {
            extendedFdrState.UpdateFromFdr(updated);
        }
    }
}