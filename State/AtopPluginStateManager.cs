using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows.Forms;
using AtopPlugin.Conflict;
using AtopPlugin.UI;
using vatsys;

namespace AtopPlugin.State;

public static class AtopPluginStateManager
{
    private const int MissingFromFdpState = -1;

    private static readonly ConcurrentDictionary<string, AtopAircraftState> AircraftStates = new();
    private static readonly ConcurrentDictionary<string, AtopAircraftDisplayState> DisplayStates = new();
    private static readonly ConcurrentDictionary<string, ConflictProbe.Conflicts> Conflicts = new();
    private static bool _activated;
    private static readonly object StatesLock = new();
    private static readonly object ConflictProbeLock = new();
    private static readonly object ActivationLock = new();
    private static bool _initialized = false;

    public static bool Activated
    {
        get => _activated;
        private set
        {
            lock (ActivationLock)
            {
                _activated = value;
                AtopMenu.SetActivationState(value);
            }
        }
    }

    /// <summary>
    /// Initialize the state manager and subscribe to conflict updates from webapp
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        
        // Subscribe to webapp conflict updates
        ConflictProbe.CallsignConflictsUpdated += OnCallsignConflictsUpdated;
    }

    private static void OnCallsignConflictsUpdated(string callsign, ConflictProbe.Conflicts conflicts)
    {
        lock (ConflictProbeLock)
        {
            Conflicts.AddOrUpdate(callsign, conflicts, (_, _) => conflicts);
        }
    }

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

    public static ConflictProbe.Conflicts? GetConflicts(string callsign)
    {
        var found = Conflicts.TryGetValue(callsign, out var state);
        return found ? state : null;
    }

    public static async Task ProcessFdrUpdate(FDP2.FDR updated)
    {
        var callsign = updated.Callsign;

        if (FDP2.GetFDRIndex(callsign) == MissingFromFdpState)
        {
            lock (StatesLock)
            {
                AircraftStates.TryRemove(callsign, out _);
                DisplayStates.TryRemove(callsign, out _);
            }

            return;
        }

        var aircraftState = GetAircraftState(callsign);
        if (aircraftState == null)
        {
            aircraftState = await Task.Run(() => new AtopAircraftState(updated));
            lock (StatesLock)
            {
                AircraftStates.TryAdd(callsign, aircraftState);
            }
        }
        else
        {
            await Task.Run(() => aircraftState.UpdateFromFdr(updated));
        }
    }

    public static async Task ProcessDisplayUpdate(FDP2.FDR fdr)
    {
        var callsign = fdr.Callsign;
        var atopState = fdr.GetAtopState();
        if (atopState == null) return;

        var displayState = GetDisplayState(callsign);
        if (displayState == null)
        {
            displayState = await Task.Run(() => new AtopAircraftDisplayState(atopState));
            lock (StatesLock)
            {
                DisplayStates.TryAdd(callsign, displayState);
            }
        }
        else
        {
            await Task.Run(() => displayState.UpdateFromAtopState(atopState));
        }
    }

    public static void ToggleActivated()
    {
        lock (ActivationLock)
        {
            var newActivationState = !Activated;

            switch (newActivationState)
            {
                case true when !Network.IsConnected:
                    MessageBox.Show(@"Please connect to the network before activating");
                    return;
                case true:
                    MessageBox.Show(@"Session activated");
                    break;
                case false:
                    MessageBox.Show(@"Session deactivated");
                    break;
            }

            Activated = newActivationState;
        }
    }

    public static void Reset()
    {
        AircraftStates.Clear();
        DisplayStates.Clear();
        Conflicts.Clear();
        Activated = false;
    }
}