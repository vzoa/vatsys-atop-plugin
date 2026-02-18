namespace AtopPlugin.Models;

/// <summary>
/// Time of Passing (TOP) indicates the relative position of aircraft at a common waypoint.
/// Per ATOP NAS-MD-4714 Section 6.2.4.3 - Used for longitudinal separation on same/crossing tracks.
/// </summary>
public enum TimeOfPassing
{
    /// <summary>
    /// Aircraft 1 passes before Aircraft 2
    /// </summary>
    Before,
    
    /// <summary>
    /// Aircraft 2 passes before Aircraft 1
    /// </summary>
    After,
    
    /// <summary>
    /// Aircraft pass at the same time (within tolerance)
    /// </summary>
    Same,
    
    /// <summary>
    /// Unable to determine time of passing
    /// </summary>
    Unknown
}
