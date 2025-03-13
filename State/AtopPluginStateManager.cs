﻿using System;
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
    private static readonly ConcurrentDictionary<string, DateTime> lastProbeTimeDict = new();
    private static bool _probeEnabled = Config.ConflictProbeEnabled;
    private static bool _activated;
    private static readonly object StatesLock = new();
    private static readonly object ConflictProbeLock = new();
    private static readonly object ActivationLock = new();
    private static readonly Random _random = new Random();

    private static bool ProbeEnabled
    {
        get => _probeEnabled;
        set
        {
            _probeEnabled = value;
            Config.ConflictProbeEnabled = value;
        }
    }

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

    public static async Task RunConflictProbe(FDP2.FDR fdr)
    {
        // Try to get the last probe time for the given callsign
        if (!lastProbeTimeDict.TryGetValue(fdr.Callsign, out DateTime lastProbeTime))
        {
            // Initialize with a random offset between 0 and 5 minutes
            var randomOffset = TimeSpan.FromSeconds(_random.Next(0, 300));
            lastProbeTime = DateTime.Now - new TimeSpan(0, 5, 0) + randomOffset;
        }

        // Check if probing is enabled and if enough time has passed since the last probe
        if (ProbeEnabled && DateTime.Now - lastProbeTime > new TimeSpan(0, 5, 0))
        {
            var newConflicts = await Task.Run(() => ConflictProbe.Probe(fdr));

            lock (ConflictProbeLock)
            {
                // Re-check whether probe is still enabled after locking
                if (!ProbeEnabled) return;

                // Update conflicts and last probe time
                Conflicts.AddOrUpdate(fdr.Callsign, newConflicts, (_, _) => newConflicts);
                lastProbeTimeDict[fdr.Callsign] = DateTime.Now;
            }
        }
    }

    public static bool IsConflictProbeEnabled()
    {
        return ProbeEnabled;
    }

    public static void SetConflictProbe(bool conflictProbeEnabled)
    {
        lock (ConflictProbeLock)
        {
            ProbeEnabled = conflictProbeEnabled;
            if (!ProbeEnabled) Conflicts.Clear();
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