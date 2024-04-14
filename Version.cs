namespace AtopPlugin;

public static class Version
{
    private const string VersionPrefix = "Version ";
    private const int AiracVersion = 2403;
    private const int MajorVersion = 1;
    private const int MinorVersion = 1;

    public static string GetVersionString()
    {
        return VersionPrefix + AiracVersion + "." + MajorVersion + "." + MinorVersion;
    }
}