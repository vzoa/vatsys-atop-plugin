using System;
using vatsys;

namespace AtopPlugin.Conflict;

/// <summary>
/// Interface for calculating separation minima between aircraft pairs.
/// Per ATOP NAS-MD-4714 Section 6.2.4 - Separation Standards
/// </summary>
public interface IMinimaCalculator
{
    /// <summary>
    /// Get lateral separation minima in nautical miles.
    /// Based on RNP capability of both aircraft.
    /// </summary>
    int GetLateralMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
    
    /// <summary>
    /// Get vertical separation minima in feet.
    /// Based on RVSM status and altitude.
    /// </summary>
    int GetVerticalMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
    
    /// <summary>
    /// Get longitudinal time separation minima.
    /// Based on track type (same, opposite, crossing) and MNT applicability.
    /// </summary>
    TimeSpan GetLongitudinalTimeMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
    
    /// <summary>
    /// Get longitudinal distance separation minima in nautical miles.
    /// Returns null if distance-based separation is not applicable.
    /// </summary>
    int? GetLongitudinalDistanceMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
}

/// <summary>
/// Region-specific minima delegate interface
/// </summary>
public interface IMinimaDelegate : IMinimaCalculator
{
    MinimaRegion GetRegion();
}

/// <summary>
/// Supported oceanic regions with specific separation standards
/// </summary>
public enum MinimaRegion
{
    Pacific,
    NorthAtlantic
}