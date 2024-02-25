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
        Network.PrivateMessagesChanged += PrivateMessagesChangedHandler.Handle;
    }

    public void OnFDRUpdate(FDP2.FDR updated)
    {
        AtopPluginStateManager.ProcessStateUpdate(updated);
        JurisdictionManager.HandleFdrUpdate(updated);
    }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated)
    {
        JurisdictionManager.HandleRadarTrackUpdate(updated);
    }

    public CustomStripItem GetCustomStripItem(string itemType, Track track, FDP2.FDR flightDataRecord,
        RDP.RadarTrack radarTrack)
    {
        return StripItemRenderer.RenderStripItem(itemType, track, flightDataRecord, radarTrack);
    }

    public CustomLabelItem GetCustomLabelItem(string itemType, Track track, FDP2.FDR flightDataRecord,
        RDP.RadarTrack radarTrack)
    {
        return LabelItemRenderer.RenderLabelItem(itemType, track, flightDataRecord, radarTrack);
    }

    public CustomColour SelectASDTrackColour(Track track)
    {
        return TrackColorRenderer.GetAsdColor(track);
    }

    public CustomColour SelectGroundTrackColour(Track track)
    {
        return null;
    }
}