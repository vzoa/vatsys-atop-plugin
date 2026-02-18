using System;
using AtopPlugin.Models;
using vatsys;
using System.Linq;

namespace AtopPlugin.Conflict;

/// <summary>
/// Pacific minima per ATOP NAS-MD-4714 and ICAO Doc 7030 Pacific Region
/// </summary>
public class PacificMinimaDelegate : IMinimaDelegate
{
    // Lateral separation minima (nautical miles)
    private const int Rnp4Lateral = 23;          // RNP4 certified
    private const int Rnp10Lateral = 50;         // RNP10/RNAV10 certified
    private const int HalfRnpLateral = 75;       // One RNP10 and one non-RNP
    private const int StandardLateral = 100;     // Non-RNP equipped (Pacific uses 100nm vs NAT 120nm)

    // Vertical separation minima (feet)
    private const int StandardVertical = 1000;   // RVSM airspace (FL290-FL410)
    private const int NonRvsmVertical = 2000;    // Non-RVSM approved or above FL410
    private const int SupersonicVertical = 4000; // Supersonic/subsonic mix
    public const int Above600Vertical = 5000;    // Above FL600

    // Longitudinal time separation minima (minutes)
    private const int TimeLongitudinalSame = 10;       // Same track, jet aircraft with MNT
    private const int TimeLongitudinalSameNonJet = 15; // Same track, non-jet or no MNT
    private const int TimeLongitudinalCross = 15;      // Crossing tracks
    private const int TimeLongitudinalOpposite = 10;   // Opposite direction
    
    // Longitudinal distance separation minima (nautical miles)
    private const int DistanceLongitudinal = 50;  // Standard with ADS/CPDLC
    private const int Rnp4Longitudinal = 30;      // RNP4 with ADS-C
    private const int DmeLongitudinal = 20;       // DME-based separation

    public MinimaRegion GetRegion()
    {
        return MinimaRegion.Pacific;
    }

    /// <summary>
    /// Get lateral separation minima per NAS-MD-4714 Section 6.2.4.2
    /// Pacific region specific rules
    /// </summary>
    public int GetLateralMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        // RNP4 both aircraft - 23nm
        if (CanApplyRnp4(fdr1) && CanApplyRnp4(fdr2)) 
            return Rnp4Lateral;

        // RNP10 both aircraft - 50nm
        if (CanApplyRnp10(fdr1) && CanApplyRnp10(fdr2)) 
            return Rnp10Lateral;

        // Mixed RNP4/RNP10 - use 50nm (most restrictive aircraft dictates)
        if ((CanApplyRnp4(fdr1) && CanApplyRnp10(fdr2)) || (CanApplyRnp10(fdr1) && CanApplyRnp4(fdr2)))
            return Rnp10Lateral;

        // One RNP capable, one not - halfway between
        if ((CanApplyRnp10(fdr1) || CanApplyRnp4(fdr1)) && !CanApplyRnp10(fdr2) && !CanApplyRnp4(fdr2))
            return HalfRnpLateral;
        if ((CanApplyRnp10(fdr2) || CanApplyRnp4(fdr2)) && !CanApplyRnp10(fdr1) && !CanApplyRnp4(fdr1))
            return HalfRnpLateral;

        return StandardLateral;
    }

    /// <summary>
    /// Get vertical separation minima per NAS-MD-4714 Section 6.2.4.1
    /// </summary>
    public int GetVerticalMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        var block1 = AltitudeBlock.ExtractAltitudeBlock(fdr1);
        var block2 = AltitudeBlock.ExtractAltitudeBlock(fdr2);

        // Above FL600 - 5000ft
        if (block1.IsAbove600() || block2.IsAbove600())
            return Above600Vertical;

        // Above FL450 with supersonic aircraft - 4000ft
        if (block1.IsAbove450() || block2.IsAbove450())
        {
            if (IsSupersonic(fdr1) || IsSupersonic(fdr2))
                return SupersonicVertical;
        }

        // Above RVSM or non-RVSM approved - 2000ft
        if (block1.IsAboveRvsm() || block2.IsAboveRvsm())
            return NonRvsmVertical;

        // Either below RVSM floor and not RVSM approved
        if ((block1.IsBelowRvsm() || block2.IsBelowRvsm()) && 
            (!IsRvsmApproved(fdr1) || !IsRvsmApproved(fdr2)))
            return NonRvsmVertical;

        // RVSM airspace (FL290-FL410) - 1000ft
        return StandardVertical;
    }

    /// <summary>
    /// Get longitudinal time separation minima per NAS-MD-4714 Section 6.2.4.3
    /// </summary>
    public TimeSpan GetLongitudinalTimeMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        var trackType = DetermineTrackType(fdr1, fdr2);

        switch (trackType)
        {
            case ConflictType.Same:
                // Same direction - apply MNT if both are jets
                if (fdr1.IsJet() && fdr2.IsJet() && CanApplyMnt(fdr1, fdr2))
                    return TimeSpan.FromMinutes(TimeLongitudinalSame);
                return TimeSpan.FromMinutes(TimeLongitudinalSameNonJet);

            case ConflictType.Reciprocal:
                return TimeSpan.FromMinutes(TimeLongitudinalOpposite);

            case ConflictType.Crossing:
            default:
                return TimeSpan.FromMinutes(TimeLongitudinalCross);
        }
    }

    /// <summary>
    /// Get longitudinal distance separation minima per NAS-MD-4714 Section 6.2.4.4/6.2.4.5
    /// </summary>
    public int? GetLongitudinalDistanceMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        // RNP4 both aircraft with ADS-C - 30nm
        if (CanApplyRnp4(fdr1) && CanApplyRnp4(fdr2)) 
            return Rnp4Longitudinal;

        // ADS-C/CPDLC equipped with RNP10 - 50nm
        if (HasDatalink(fdr1) && HasDatalink(fdr2) && CanApplyRnp10(fdr1) && CanApplyRnp10(fdr2))
            return DistanceLongitudinal;

        // DME-based separation available
        if (HasDme(fdr1) && HasDme(fdr2))
            return DmeLongitudinal;

        return null;
    }

    /// <summary>
    /// Determine track type (same, reciprocal, crossing) per NAS-MD-4714 Appendix A.3.82 DIR_TYPE
    /// </summary>
    private static ConflictType DetermineTrackType(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        if (fdr1.ParsedRoute.Count < 2 || fdr2.ParsedRoute.Count < 2)
            return ConflictType.Crossing;

        var track1 = Conversions.CalculateTrack(
            fdr1.ParsedRoute.First().Intersection.LatLong,
            fdr1.ParsedRoute.Last().Intersection.LatLong);
        var track2 = Conversions.CalculateTrack(
            fdr2.ParsedRoute.First().Intersection.LatLong,
            fdr2.ParsedRoute.Last().Intersection.LatLong);

        var angle = Math.Abs(track1 - track2);
        if (angle > 180) angle = 360 - angle;

        // Per NAS-MD-4714 Appendix A.3.82 DIR_TYPE:
        // if |θ| < 45°, then return same direction
        // if |θ| > 135°, then return reciprocal direction
        // else return crossing direction
        if (angle < 45)
            return ConflictType.Same;
        if (angle > 135)
            return ConflictType.Reciprocal;
        return ConflictType.Crossing;
    }

    /// <summary>
    /// Check if Mach Number Technique (MNT) can be applied
    /// </summary>
    private static bool CanApplyMnt(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        return fdr1.IsJet() && fdr2.IsJet() && (HasDatalink(fdr1) || HasDatalink(fdr2));
    }

    private static bool HasDatalink(FDP2.FDR fdr)
    {
        return fdr.GetAtopState()?.CalculatedFlightData is { Pbcs: true, Adsc: true, Cpdlc: true };
    }

    private static bool CanApplyRnp4(FDP2.FDR fdr)
    {
        return HasDatalink(fdr) && fdr.GetAtopState()?.CalculatedFlightData is { Rnp4: true };
    }

    private static bool CanApplyRnp10(FDP2.FDR fdr)
    {
        return fdr.GetAtopState()?.CalculatedFlightData is { Rnp10: true };
    }

    /// <summary>
    /// Check if aircraft is RVSM approved
    /// </summary>
    private static bool IsRvsmApproved(FDP2.FDR fdr)
    {
        return fdr.AircraftEquip?.Contains("W") == true;
    }

    /// <summary>
    /// Check if aircraft is supersonic capable
    /// </summary>
    private static bool IsSupersonic(FDP2.FDR fdr)
    {
        return false;
    }

    /// <summary>
    /// Check if aircraft has DME capability
    /// </summary>
    private static bool HasDme(FDP2.FDR fdr)
    {
        return fdr.AircraftEquip?.Contains("D") == true;
    }
}