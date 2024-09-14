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
    private static bool _probeEnabled = Config.ConflictProbeEnabled;
    private static bool _activated = false;
    private static readonly object _lock = new object();

    private static bool ProbeEnabled
    {
        get
        {
            lock (_lock)
            {
                return _probeEnabled;
            }
        }
        set
        {
            lock (_lock)
            {
                _probeEnabled = value;
                Config.ConflictProbeEnabled = value;
            }
        }
    }

    public static bool Activated
    {
        get
        {
            lock (_lock)
            {
                return _activated;
            }
        }
        set
        {
            lock (_lock)
            {
                _activated = value;
                AtopMenu.SetActivationState(value);
            }
        }
    }

    public static AtopAircraftState? GetAircraftState(string callsign)
    {
        lock (_lock)
        {
            var found = AircraftStates.TryGetValue(callsign, out var state);
            return found ? state : null;
        }
    }

    public static AtopAircraftDisplayState? GetDisplayState(string callsign)
    {
        lock (_lock)
        {
            var found = DisplayStates.TryGetValue(callsign, out var state);
            return found ? state : null;
        }
    }

    public static ConflictProbe.Conflicts? GetConflicts(string callsign)
    {
        lock (_lock)
        {
            var found = Conflicts.TryGetValue(callsign, out var state);
            return found ? state : null;
        }
    }

    public static async Task ProcessFdrUpdate(FDP2.FDR updated)
    {
        var callsign = updated.Callsign;

        if (FDP2.GetFDRIndex(callsign) == MissingFromFdpState)
        {
            lock (_lock)
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
            lock (_lock)
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
            lock (_lock)
            {
                DisplayStates.TryAdd(callsign, displayState);
            }
        }
        else
        {
            await Task.Run(() => displayState.UpdateFromAtopState(atopState));
        }
    }

    public static async Task RunConflictProbe(FDP2.FDR fdr)
    {
        if (ProbeEnabled)
        {
            var newConflicts = await Task.Run(() => ConflictProbe.Probe(fdr));
            lock (_lock)
            {
                Conflicts.AddOrUpdate(fdr.Callsign, newConflicts, (_, _) => newConflicts);
            }
        }
    }

    public static bool IsConflictProbeEnabled()
    {
        lock (_lock)
        {
            return ProbeEnabled;
        }
    }

    public static void SetConflictProbe(bool conflictProbeEnabled)
    {
        lock (_lock)
        {
            ProbeEnabled = conflictProbeEnabled;
            if (!ProbeEnabled) Conflicts.Clear();
        }
    }

    public static void ToggleActivated()
    {
        lock (_lock)
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
        lock (_lock)
        {
            AircraftStates.Clear();
            DisplayStates.Clear();
            Conflicts.Clear();
            Activated = false;
        }
    }
}
