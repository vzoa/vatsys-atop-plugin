using System;
using System.Collections.Concurrent;
using AtopPlugin.Models;
using vatsys;

namespace AtopPlugin.State;

/// <summary>
/// Tracks active lateral deviation clearances per callsign so the conflict probe can widen the
/// affected aircraft's protected-airspace buffer per NAS-MD-4714 6.2.8.3.2.3 "Leg Deviation Buffer".
/// </summary>
public static class DeviationStateManager
{
    private static readonly ConcurrentDictionary<string, DeviationClearanceState> States =
        new(StringComparer.OrdinalIgnoreCase);

    public static event Action<string>? DeviationStateChanged;

    public static void RecordDeviation(string callsign, double doffNm, string direction)
    {
        if (string.IsNullOrWhiteSpace(callsign) || doffNm <= 0) return;

        States[callsign] = new DeviationClearanceState
        {
            Callsign = callsign,
            DeviationNm = doffNm,
            Direction = NormalizeDirection(direction),
            IssuedAtUtc = DateTimeOffset.UtcNow,
            IsActive = true,
            PendingRejoin = DeviationRejoinType.None
        };
        RaiseChanged(callsign);
    }

    public static void RecordRejoin(string callsign, DeviationRejoinType type, string? position = null, DateTime? timeUtc = null)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;
        if (!States.TryGetValue(callsign, out var state) || !state.IsActive) return;

        if (type == DeviationRejoinType.Immediate)
        {
            state.IsActive = false;
        }
        else
        {
            // Buffer stays active until the rejoin condition is actually met — see EvaluateAutoRejoin.
            state.PendingRejoin = type;
            state.RejoinPosition = position;
            state.RejoinTimeUtc = timeUtc;
        }

        RaiseChanged(callsign);
    }

    public static void ClearDeviation(string callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;
        if (States.TryRemove(callsign, out _))
            RaiseChanged(callsign);
    }

    public static DeviationClearanceState? GetActiveDeviation(string callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return null;
        return States.TryGetValue(callsign, out var state) && state.IsActive ? state : null;
    }

    /// <summary>
    /// Auto-clears deviations whose REJOIN ROUTE BY [time] has elapsed. There's no live pilot
    /// "back on route" report to key off, so ByPosition rejoins are left active until the
    /// controller explicitly sends PROCEED BACK ON ROUTE.
    /// </summary>
    public static void EvaluateAutoRejoin(string callsign, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;
        if (!States.TryGetValue(callsign, out var state) || !state.IsActive) return;

        if (state.PendingRejoin == DeviationRejoinType.ByTime &&
            state.RejoinTimeUtc.HasValue && nowUtc >= state.RejoinTimeUtc.Value)
        {
            state.IsActive = false;
            RaiseChanged(callsign);
        }
    }

    private static string NormalizeDirection(string direction)
    {
        if (string.IsNullOrWhiteSpace(direction)) return "";
        var d = direction.Trim().ToUpperInvariant();
        if (d.StartsWith("L")) return "L";
        if (d.StartsWith("R")) return "R";
        return d;
    }

    private static void RaiseChanged(string callsign)
    {
        try
        {
            DeviationStateChanged?.Invoke(callsign);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"DeviationStateManager.RaiseChanged: {ex.Message}", ex));
        }
    }
}
