using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AtopPlugin.Helpers;

public enum StripProfileVisualState
{
    None,
    Probing,
    SentPendingReadback
}

public sealed class ProposedRouteWaypoint
{
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime? EtoUtc { get; set; }
}

public static class ProposedProfileBridge
{
    private sealed class ProposedProfileState
    {
        public bool IsProbing;
        public bool IsSentPendingReadback;
        public DateTimeOffset? SentAtUtc;
        public List<ProposedRouteWaypoint> Waypoints = new();
        public readonly object Sync = new();
    }

    private static readonly ConcurrentDictionary<string, ProposedProfileState> States =
        new(StringComparer.OrdinalIgnoreCase);

    public static event Action<string>? ProbeStateChanged;

    private static void RaiseProbeStateChanged(string callsign)
    {
        ProbeStateChanged?.Invoke(callsign);
    }

    public static bool TryBeginProbe(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return false;
        var key = callsign.Trim();

        var state = States.GetOrAdd(key, _ => new ProposedProfileState());
        lock (state.Sync)
        {
            if (state.IsProbing || state.IsSentPendingReadback)
                return false;

            state.IsProbing = true;
            state.IsSentPendingReadback = false;
            state.SentAtUtc = null;
            RaiseProbeStateChanged(key);
            return true;
        }
    }

    public static void BeginProbe(string? callsign)
    {
        TryBeginProbe(callsign);
    }

    public static void CompleteProbe(string? callsign, IEnumerable<ProposedRouteWaypoint>? waypoints)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;
        var key = callsign.Trim();
        if (!States.TryGetValue(key, out var state)) return;

        lock (state.Sync)
        {
            state.IsProbing = false;

            if (waypoints != null)
            {
                state.Waypoints = waypoints
                    .Where(w => w != null)
                    .Select(w => new ProposedRouteWaypoint
                    {
                        Name = w.Name ?? "",
                        Latitude = w.Latitude,
                        Longitude = w.Longitude,
                        EtoUtc = w.EtoUtc
                    })
                    .ToList();
            }

            RaiseProbeStateChanged(key);
        }
    }

    public static void MarkSentPendingReadback(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;
        var key = callsign.Trim();

        var state = States.GetOrAdd(key, _ => new ProposedProfileState());
        lock (state.Sync)
        {
            state.IsProbing = false;
            state.IsSentPendingReadback = true;
            state.SentAtUtc = DateTimeOffset.UtcNow;
            RaiseProbeStateChanged(key);
        }
    }

    public static void EvaluateCpdlcReadback(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;
        var key = callsign.Trim();
        if (!States.TryGetValue(key, out var state)) return;

        bool shouldClear = false;

        lock (state.Sync)
        {
            if (!state.IsSentPendingReadback || !state.SentAtUtc.HasValue)
                return;

            if (CpdlcPluginBridge.HasWilcoReadbackSince(key, state.SentAtUtc.Value))
                shouldClear = true;
        }

        if (shouldClear)
            Clear(key);
    }

    public static StripProfileVisualState GetVisualState(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return StripProfileVisualState.None;
        var key = callsign.Trim();
        if (!States.TryGetValue(key, out var state)) return StripProfileVisualState.None;

        lock (state.Sync)
        {
            if (state.IsProbing) return StripProfileVisualState.Probing;
            if (state.IsSentPendingReadback) return StripProfileVisualState.SentPendingReadback;
            return StripProfileVisualState.None;
        }
    }

    public static bool TryGetProposedRoute(string? callsign, out IReadOnlyList<ProposedRouteWaypoint> waypoints)
    {
        waypoints = Array.Empty<ProposedRouteWaypoint>();

        if (string.IsNullOrWhiteSpace(callsign)) return false;
        var key = callsign.Trim();
        if (!States.TryGetValue(key, out var state)) return false;

        lock (state.Sync)
        {
            if (state.Waypoints.Count == 0)
                return false;

            if (!state.IsProbing && !state.IsSentPendingReadback)
                return false;

            waypoints = state.Waypoints
                .Select(w => new ProposedRouteWaypoint
                {
                    Name = w.Name,
                    Latitude = w.Latitude,
                    Longitude = w.Longitude,
                    EtoUtc = w.EtoUtc
                })
                .ToList();

            return waypoints.Count > 0;
        }
    }

    public static void Clear(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;
        var key = callsign.Trim();
        if (States.TryRemove(key, out _))
            RaiseProbeStateChanged(key);
    }
}
