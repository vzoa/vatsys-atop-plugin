namespace AtopPlugin.Models;

/// <summary>
/// Conflict type based on track angle between aircraft.
/// Per ATOP NAS-MD-4714 Appendix A.3.82 DIR_TYPE:
/// - Same direction: |θ| &lt; 45°
/// - Reciprocal direction: |θ| &gt; 135°
/// - Crossing direction: 45° ≤ |θ| ≤ 135°
/// 
/// Note: "Same direction" for basic classification is &lt; 90°, and "Opposite" is ≥ 90°.
/// The above thresholds are used for separation standards determination.
/// </summary>
public enum ConflictType
{
    /// <summary>
    /// Same direction tracks (|θ| &lt; 45°)
    /// </summary>
    Same,
    
    /// <summary>
    /// Reciprocal/Opposite direction tracks (|θ| &gt; 135°)
    /// Per spec, this is called "Reciprocal" for separation purposes.
    /// </summary>
    Reciprocal,
    
    /// <summary>
    /// Crossing tracks (45° ≤ |θ| ≤ 135°)
    /// </summary>
    Crossing,
    
    // Aliases for backward compatibility
    SameDirection = Same,
    OppositeDirection = Reciprocal,
    Opposite = Reciprocal
}