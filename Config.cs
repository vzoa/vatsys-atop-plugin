using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using AtopPlugin.Conflict;

namespace AtopPlugin;

public static class Config
{
    private const string ConfigFileName = "AtopPluginConfig.xml";
    private static readonly XmlSerializer ConfigSerializer = new(typeof(AtopPluginConfiguration));

    private static readonly AtopPluginConfiguration Configuration = LoadFromFile();

    public static bool ConflictProbeEnabled
    {
        get => Configuration.EnableConflictProbe;
        set
        {
            Configuration.EnableConflictProbe = value;
            SaveConfigurationToFile(Configuration);
        }
    }

    public static MinimaRegion MinimaRegion => Configuration.MinimaRegion;

    private static AtopPluginConfiguration LoadFromFile()
    {
        if (!File.Exists(GetConfigFilePath())) SaveConfigurationToFile(new AtopPluginConfiguration());
        using var configFileStream = File.OpenRead(GetConfigFilePath());
        return (AtopPluginConfiguration)ConfigSerializer.Deserialize(configFileStream);
    }

    private static void SaveConfigurationToFile(AtopPluginConfiguration configuration)
    {
        using var configFileStream = File.Create(GetConfigFilePath());
        ConfigSerializer.Serialize(configFileStream, configuration);
    }

    private static string GetConfigFilePath()
    {
        return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            ConfigFileName);
    }

    // Public so XmlSerializer can access the class
    // ReSharper disable once MemberCanBePrivate.Global
    public record AtopPluginConfiguration
    {
        public bool EnableConflictProbe { get; set; } = true;
        public MinimaRegion MinimaRegion { get; set; } = MinimaRegion.Pacific;
    }
}