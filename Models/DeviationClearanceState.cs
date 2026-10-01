using System;

namespace AtopPlugin.Models;

/// <summary>
/// How an active lateral deviation clearance is expected to end.
/// </summary>
public enum DeviationRejoinType
{
    /// <summary>No rejoin condition has been issued yet — deviation remains open-ended.</summary>
    None,

    /// <summary>PROCEED BACK ON ROUTE (message 67) — clears the deviation immediately.</summary>
    Immediate,

    /// <summary>REJOIN ROUTE BY [pos] (message 68).</summary>
    ByPosition,

    /// <summary>REJOIN ROUTE BY [time] (message 69).</summary>
    ByTime
}

/// <summary>
/// Tracks an aircraft's active lateral deviation (offset) clearance — e.g. "OFFSET 20L OF ROUTE"
/// or "CLEARED TO DEVIATE UP TO 20 L OF ROUTE" — so the conflict probe can widen that aircraft's
/// protected-airspace buffer on the cleared side per NAS-MD-4714 6.2.8.3.2.3 "Leg Deviation Buffer"
/// (Figure 6-45): since the aircraft could be anywhere within the deviation, other traffic must be
/// kept further away on that side until the deviation is resolved.
/// </summary>
public class DeviationClearanceState
{
    public string Callsign { get; set; } = "";

    /// <summary>Deviation distance in nautical miles (the "doff" parameter).</summary>
    public double DeviationNm { get; set; }

    /// <summary>Side of the route centerline the deviation is cleared towards: "L" or "R".</summary>
    public string Direction { get; set; } = "";

    public DateTimeOffset IssuedAtUtc { get; set; }

    /// <summary>True while the widened buffer should still be applied.</summary>
    public bool IsActive { get; set; } = true;

    public DeviationRejoinType PendingRejoin { get; set; } = DeviationRejoinType.None;

    /// <summary>Set when PendingRejoin == ByPosition.</summary>
    public string? RejoinPosition { get; set; }

    /// <summary>Set when PendingRejoin == ByTime.</summary>
    public DateTime? RejoinTimeUtc { get; set; }
}
