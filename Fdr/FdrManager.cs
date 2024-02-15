using System.Collections.Concurrent;

namespace AuroraLabelItemsPlugin.Fdr;

public static class FdrManager
{
    private static readonly ConcurrentDictionary<string, ExtendedFdrState> ExtendedFdrStates = new();

    public static ExtendedFdrState GetExtendedFdrState(string callsign)
    {
        var found = ExtendedFdrStates.TryGetValue(callsign, out var state);
        return found ? state : null;
    }
}