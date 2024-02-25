namespace AtopPlugin.Models;

public enum ConflictStatus
{
    Actual,
    Imminent,
    Advisory,
    None
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