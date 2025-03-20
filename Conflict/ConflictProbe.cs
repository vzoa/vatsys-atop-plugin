using System;
using System.Collections.Generic;
using System.Linq;
using AtopPlugin.Models;
using vatsys;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static vatsys.FDP2;
using AtopPlugin.Display;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Drawing.Text;

namespace AtopPlugin.Conflict;

public static class ConflictProbe
{
    public static List<ConflictData> ConflictDatas { get; set; } = new();
    private static readonly ConcurrentDictionary<string, List<double>> PrecomputedTracks = new();

    public static event EventHandler ConflictsUpdated;

    // Thread-safe storage for active conflicts
    private static readonly ConcurrentDictionary<string, ConflictData> ActiveConflicts = new();

    public static Conflicts Probe(FDR fdr)
    {
        if (!MMI.IsMySectorConcerned(fdr) ||
            fdr.State is FDR.FDRStates.STATE_INACTIVE or FDR.FDRStates.STATE_PREACTIVE or FDR.FDRStates.STATE_FINISHED)
            return EmptyConflicts();

        // Additional filtering based on observable FDR attributes
        if (fdr.ControllingSector == null || fdr.HandoffSector == null)
        {
            Console.WriteLine($"Skipping conflict probe for {fdr.Callsign} due to missing sector information.");
            return EmptyConflicts();
        }

        var discoveredConflicts = new ConcurrentDictionary<string, ConflictData>(); // Stores active conflicts

        var block1 = AltitudeBlock.ExtractAltitudeBlock(fdr);

        Parallel.ForEach(GetFDRs, fdr2 =>
        {
            if (fdr.Callsign == fdr2.Callsign || !MMI.IsMySectorConcerned(fdr2)) return;

            var block2 = AltitudeBlock.ExtractAltitudeBlock(fdr2);

            var data = new ConflictData { Active = fdr2, Intruder = fdr };

            // Temporal Test
            if (!PassesTemporalTest(fdr.ATD, fdr.ParsedRoute.Last().ETO, fdr2.ATD, fdr2.ParsedRoute.Last().ETO)) return;

            // Vertical Test
            data.VerticalAct = AltitudeBlock.Difference(block1, block2);
            data.VerticalSep = MinimaCalculator.Instance.GetVerticalMinima(fdr, fdr2);
            if (data.VerticalAct >= data.VerticalSep) return;

            // Lateral Test
            if (!LateralConflictCalculator.CalculateRectangleOverlap(fdr, fdr2)) return;

            // Compute lateral and longitudinal separation
            data.LatSep = MinimaCalculator.Instance.GetLateralMinima(fdr, fdr2);
            var conflictSegments1 = LateralConflictCalculator.CalculateAreaOfConflict(fdr, fdr2, data.LatSep);
            conflictSegments1.Sort((s, t) => s.StartTime.CompareTo(t.StartTime));

            if (conflictSegments1.Count == 0) return;

            data.FirstConflictTime = conflictSegments1.First();
            data.LongTimesep = MinimaCalculator.Instance.GetLongitudinalTimeMinima(fdr, fdr2);
            data.LongTimeact = (data.FirstConflictTime.EndTime - data.FirstConflictTime.StartTime).Duration();
            data.LongDistsep = MinimaCalculator.Instance.GetLongitudinalDistanceMinima(fdr, fdr2);
            data.LongDistact = Conversions.CalculateDistance(data.FirstConflictTime.StartLatlong,
                                                             data.FirstConflictTime.EndLatlong);

            bool lossOfSep = data.LongTimeact < data.LongTimesep || data.LongDistact < data.LongDistsep;
            if (!lossOfSep)
            {
                // **Remove resolved conflict from ActiveConflicts**
                ActiveConflicts.TryRemove($"{fdr.Callsign}-{fdr2.Callsign}", out _);
                return;
            }

            // Determine Conflict Status Based on Time
            var now = DateTime.UtcNow;
            var timeUntilLOS = (data.FirstConflictTime.StartTime - now).Duration();

            if (timeUntilLOS < TimeSpan.FromMinutes(1)) // < 1 min
            {
                data.ConflictStatus = ConflictStatus.Actual;
            }
            else if (timeUntilLOS <= TimeSpan.FromMinutes(30)) // ≤ 30 min
            {
                data.ConflictStatus = ConflictStatus.Imminent;
            }
            else if (timeUntilLOS <= TimeSpan.FromHours(2)) // ≤ 2 hours
            {
                data.ConflictStatus = ConflictStatus.Advisory;
            }
            else
            {
                return; // No conflict if greater than 2 hours
            }

            // **Update or add active conflict**
            ActiveConflicts.AddOrUpdate($"{fdr.Callsign}-{fdr2.Callsign}", data, (_, _) => data);
        });

        // Return only currently active conflicts
        ConflictsUpdated?.Invoke(null, new EventArgs());
        return GroupConflicts(ActiveConflicts.Values.ToList());
    }


    public static bool PassesTemporalTest(DateTime fdr1StartTime, DateTime fdr1EndTime, DateTime fdr2StartTime,
        DateTime fdr2EndTime)
    {
        if (fdr1StartTime - fdr2EndTime > TimeSpan.Zero) return false;

        if (fdr2StartTime - fdr1EndTime > TimeSpan.Zero) return false;

        return true;
    }

    private static List<double> GetOrComputeTrackAngles(FDR fdr)
    {
        return PrecomputedTracks.GetOrAdd(fdr.Callsign, _ =>
        {
            var angles = new List<double>();
            var route = fdr.ParsedRoute;
            for (int p = 0; p < route.Count - 1; p++)
            {
                angles.Add(Conversions.CalculateTrack(route[p].Intersection.LatLong,
                                                      route[p + 1].Intersection.LatLong));
            }
            return angles;
        });
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
            //FDP2.SetCFL(fdr3, fdr2.CFLString);
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