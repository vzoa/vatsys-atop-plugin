#nullable enable
using System.Collections.Concurrent;
using vatsys;

namespace AuroraLabelItemsPlugin.State;

public static class AtopPluginStateManager
{
    private const int MissingFromFdpState = -1;

    private static readonly ConcurrentDictionary<string, AtopAircraftState> AircraftStates = new();
    private static readonly ConcurrentDictionary<string, AtopAircraftDisplayState> DisplayStates = new();

    public static AtopAircraftState? GetAircraftState(string callsign)
    {
        var found = AircraftStates.TryGetValue(callsign, out var state);
        return found ? state : null;
    }

    public static AtopAircraftDisplayState? GetDisplayState(string callsign)
    {
        var found = DisplayStates.TryGetValue(callsign, out var state);
        return found ? state : null;
    }

    public static void ProcessStateUpdate(FDP2.FDR updated)
    {
        var callsign = updated.Callsign;

        if (FDP2.GetFDRIndex(callsign) == MissingFromFdpState)
        {
            AircraftStates.TryRemove(callsign, out _);
            DisplayStates.TryRemove(callsign, out _);
            return;
        }

        var aircraftState = GetAircraftState(callsign);
        if (aircraftState == null)
        {
            aircraftState = new AtopAircraftState(updated);
            AircraftStates.TryAdd(callsign, aircraftState);
        }
        else
        {
            aircraftState.UpdateFromFdr(updated);
        }

        var displayState = GetDisplayState(callsign);
        if (displayState == null)
            DisplayStates.TryAdd(callsign, new AtopAircraftDisplayState(aircraftState));
        else
            displayState.UpdateFromAtopState(aircraftState);
    }
}