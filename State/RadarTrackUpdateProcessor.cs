using vatsys;

namespace AtopPlugin.State;

public static class RadarTrackUpdateProcessor
{
    public static void ProcessRadarTrackUpdate(RDP.RadarTrack updated)
    {
        // don't manage jurisdiction if not connected as ATC
        if (Network.Me.IsRealATC) JurisdictionManager.HandleRadarTrackUpdate(updated);
    }
}