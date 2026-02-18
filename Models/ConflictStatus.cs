namespace AtopPlugin.Models;

/// <summary>
/// Conflict status indicating severity and time until loss of separation.
/// Per ATOP NAS-MD-4714 Section 6.2.5:
/// - Actual: Loss of separation occurring now or within 1 minute
/// - Imminent: Loss of separation within 30 minutes
/// - Advisory: Loss of separation within 2 hours
/// </summary>
public enum ConflictStatus
{
    Actual,
    Imminent,
    Advisory,
    None,
    
    // Alias for backward compatibility
    NoConflict = None
}

public static class ConflictStatusUtils
{
    public static ConflictStatus From(bool actual, bool imminent, bool advisory)
    {
        return (actual, imminent, advisory) switch
        {
            { actual: true } => ConflictStatus.Actual,
            { imminent: true } => ConflictStatus.Imminent,
            { advisory: true } => ConflictStatus.Advisory,
            _ => ConflictStatus.None
        };
    }
}