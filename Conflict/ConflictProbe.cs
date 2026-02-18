using System;
using System.Collections.Generic;
using System.Linq;
using AtopPlugin.Models;
using AtopPlugin.Helpers;
using vatsys;
using static vatsys.FDP2;
using System.Collections.Concurrent;

namespace AtopPlugin.Conflict;

/// <summary>
/// Simplified ConflictProbe that receives conflict results from the webapp
/// instead of performing heavy calculations locally.
/// </summary>
public static class ConflictProbe
{
    public static List<ConflictData> ConflictDatas { get; set; } = new();
    
    public static event EventHandler ConflictsUpdated;
    
    // Event fired when conflicts for a specific callsign are updated
    public static event Action<string, Conflicts> CallsignConflictsUpdated;

    // Thread-safe storage for active conflicts received from webapp
    private static readonly ConcurrentDictionary<string, ConflictData> ActiveConflicts = new();

    // Flag to control whether to use webapp calculations or local (fallback)
    public static bool UseWebAppConflictService { get; set; } = true;

    static ConflictProbe()
    {
        // Subscribe to conflict results from WebSocket server
        AtopWebSocketServer.Instance.ConflictResultsReceived += OnWebAppConflictResultsReceived;
    }

    /// <summary>
    /// Handles conflict results received from the webapp via WebSocket
    /// </summary>
    private static void OnWebAppConflictResultsReceived(List<WebAppConflictResult> results)
    {
        if (results == null) return;

        // Track which callsigns are affected
        var affectedCallsigns = new HashSet<string>();

        // Clear existing conflicts and rebuild from webapp results
        var newConflicts = new Dictionary<string, ConflictData>();

        foreach (var result in results)
        {
            var intruderFdr = GetFDRs.FirstOrDefault(f => f.Callsign == result.IntruderCallsign);
            var activeFdr = GetFDRs.FirstOrDefault(f => f.Callsign == result.ActiveCallsign);

            if (intruderFdr == null || activeFdr == null) continue;

            var conflictData = new ConflictData
            {
                Intruder = intruderFdr,
                Active = activeFdr,
                ConflictStatus = ParseConflictStatus(result.Status),
                ConflictType = ParseConflictType(result.ConflictType),
                LatSep = (int)(result.LateralSep ?? 0),
                VerticalSep = (int)(result.VerticalSep ?? 0),
                VerticalAct = (int)(result.VerticalAct ?? 0),
                EarliestLos = ParseDateTime(result.EarliestLos),
                LatestLos = ParseDateTime(result.LatestLos)
            };

            var key = $"{result.IntruderCallsign}-{result.ActiveCallsign}";
            newConflicts[key] = conflictData;
            
            // Track affected callsigns
            affectedCallsigns.Add(result.IntruderCallsign);
            affectedCallsigns.Add(result.ActiveCallsign);
        }

        // Find callsigns that had conflicts but no longer do
        foreach (var existingKey in ActiveConflicts.Keys)
        {
            if (!newConflicts.ContainsKey(existingKey))
            {
                if (ActiveConflicts.TryGetValue(existingKey, out var oldConflict))
                {
                    if (oldConflict.Intruder != null) affectedCallsigns.Add(oldConflict.Intruder.Callsign);
                    if (oldConflict.Active != null) affectedCallsigns.Add(oldConflict.Active.Callsign);
                }
            }
        }

        // Update active conflicts atomically
        ActiveConflicts.Clear();
        foreach (var kvp in newConflicts)
        {
            ActiveConflicts.TryAdd(kvp.Key, kvp.Value);
        }

        // Update the public list and fire event
        ConflictDatas = ActiveConflicts.Values.ToList();
        ConflictsUpdated?.Invoke(null, EventArgs.Empty);
        
        // Notify state manager about each affected callsign's conflicts
        foreach (var callsign in affectedCallsigns)
        {
            var callsignConflicts = GetConflictsForCallsign(callsign);
            CallsignConflictsUpdated?.Invoke(callsign, callsignConflicts);
        }
    }

    private static ConflictStatus ParseConflictStatus(string status)
    {
        return status?.ToLower() switch
        {
            "actual" => ConflictStatus.Actual,
            "imminent" => ConflictStatus.Imminent,
            "advisory" => ConflictStatus.Advisory,
            _ => ConflictStatus.NoConflict
        };
    }

    private static ConflictType? ParseConflictType(string type)
    {
        return type?.ToLower() switch
        {
            "same" => ConflictType.Same,
            "samedirection" => ConflictType.Same,
            "opposite" => ConflictType.Reciprocal,
            "oppositedirection" => ConflictType.Reciprocal,
            "reciprocal" => ConflictType.Reciprocal,
            "crossing" => ConflictType.Crossing,
            _ => null
        };
    }

    private static DateTime ParseDateTime(string isoString)
    {
        if (string.IsNullOrEmpty(isoString)) return DateTime.UtcNow;
        return DateTime.TryParse(isoString, out var dt) ? dt : DateTime.UtcNow;
    }

    /// <summary>
    /// Returns conflicts for a specific FDR from the cached webapp results
    /// </summary>
    public static Conflicts Probe(FDR fdr)
    {
        if (!MMI.IsMySectorConcerned(fdr) ||
            fdr.State is FDR.FDRStates.STATE_INACTIVE or FDR.FDRStates.STATE_PREACTIVE or FDR.FDRStates.STATE_FINISHED)
            return EmptyConflicts();

        return GetConflictsForCallsign(fdr.Callsign);
    }
    
    /// <summary>
    /// Gets conflicts for a specific callsign from cached results
    /// </summary>
    private static Conflicts GetConflictsForCallsign(string callsign)
    {
        // Filter conflicts that involve this callsign
        var fdrConflicts = ActiveConflicts.Values
            .Where(c => c.Intruder?.Callsign == callsign || c.Active?.Callsign == callsign)
            .ToList();

        return GroupConflicts(fdrConflicts);
    }

    /// <summary>
    /// Gets all current conflicts from the webapp cache
    /// </summary>
    public static Conflicts GetAllConflicts()
    {
        return GroupConflicts(ActiveConflicts.Values.ToList());
    }

    public static bool PassesTemporalTest(DateTime fdr1StartTime, DateTime fdr1EndTime, DateTime fdr2StartTime,
        DateTime fdr2EndTime)
    {
        if (fdr1StartTime - fdr2EndTime > TimeSpan.Zero) return false;
        if (fdr2StartTime - fdr1EndTime > TimeSpan.Zero) return false;
        return true;
    }

    public static Conflicts ManualProbe(FDR fdr2)
    {
        // Create a new FDR based on the provided fdr2
        FDP2.TryCreateFDR("*" + fdr2.Callsign, fdr2.FlightRules, fdr2.DepAirport, fdr2.DesAirport, fdr2.Route,
            fdr2.Remarks, fdr2.AircraftCount.ToString(), fdr2.AircraftType, fdr2.AircraftWake, fdr2.AircraftEquip,
            fdr2.AircraftSurvEquip, fdr2.TAS.ToString(), fdr2.CFLString, fdr2.ETD.ToString("HHmm"), fdr2.EET.ToString("hhmm"), fdr2.ATD.ToString("HHmm"), fdr2.AltAirport);

        var fdr3 = GetFDRs.FirstOrDefault(s => s.Callsign == "*" + fdr2.Callsign);

        if (fdr3 != null)
        {
            FDP2.DepartFDR(fdr3, fdr2.ATD);
            return Probe(fdr3);
        }

        return EmptyConflicts();
    }

    private static Conflicts EmptyConflicts()
    {
        return new Conflicts(new List<ConflictData>(), new List<ConflictData>(), new List<ConflictData>());
    }

    private static Conflicts GroupConflicts(List<ConflictData> allConflicts)
    {
        var actual = new List<ConflictData>();
        var imminent = new List<ConflictData>();
        var advisory = new List<ConflictData>();

        foreach (var conflict in allConflicts)
        {
            if (conflict.ConflictStatus == ConflictStatus.Actual) actual.Add(conflict);
            if (conflict.ConflictStatus == ConflictStatus.Imminent) imminent.Add(conflict);
            if (conflict.ConflictStatus == ConflictStatus.Advisory) advisory.Add(conflict);
        }

        return new Conflicts(actual, imminent, advisory);
    }

    public record struct Conflicts(
        List<ConflictData> ActualConflicts,
        List<ConflictData> ImminentConflicts,
        List<ConflictData> AdvisoryConflicts);
}