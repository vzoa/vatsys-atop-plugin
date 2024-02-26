using System.ComponentModel.Composition;
using AtopPlugin.Display;
using AtopPlugin.State;
using vatsys;
using vatsys.Plugin;

namespace AtopPlugin;

[Export(typeof(IPlugin))]
public class AtopPlugin : ILabelPlugin, IStripPlugin
{
    public string Name => "ATOP Plugin";

    public AtopPlugin()
    {
        RegisterEventHandlers();
    }

    private static void RegisterEventHandlers()
    {
        Network.PrivateMessagesChanged += PrivateMessagesChangedHandler.Handle;
        FdrPropertyChangesListener.RegisterAllHandlers();
    }

    public void OnFDRUpdate(FDP2.FDR updated)
    {
        AtopPluginStateManager.ProcessFdrUpdate(updated);
        AtopPluginStateManager.ProcessDisplayUpdate(updated);
        JurisdictionManager.HandleFdrUpdate(updated);
        FdrPropertyChangesListener.RegisterHandler(updated);
    }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated)
    {
        JurisdictionManager.HandleRadarTrackUpdate(updated);
    }

    public CustomStripItem? GetCustomStripItem(string itemType, Track track, FDP2.FDR flightDataRecord,
        RDP.RadarTrack radarTrack)
    {
        return StripItemRenderer.RenderStripItem(itemType, track, flightDataRecord, radarTrack);
    }

    public CustomLabelItem? GetCustomLabelItem(string itemType, Track track, FDP2.FDR flightDataRecord,
        RDP.RadarTrack radarTrack)
    {
        return LabelItemRenderer.RenderLabelItem(itemType, track, flightDataRecord, radarTrack);
    }

    public CustomColour? SelectASDTrackColour(Track track)
    {
        return TrackColorRenderer.GetAsdColor(track);
    }

    public CustomColour? SelectGroundTrackColour(Track track)
    {
        return null;
    }
}