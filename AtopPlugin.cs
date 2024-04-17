﻿using System.ComponentModel.Composition;
using AtopPlugin.Display;
using AtopPlugin.State;
using AtopPlugin.UI;
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
        AtopMenu.Initialize();
        TempActivationMessagePopup.PopUpActivationMessageIfFirstTime();
    }

    private static void RegisterEventHandlers()
    {
        Network.PrivateMessagesChanged += PrivateMessagesChangedHandler.Handle;
        Network.Disconnected += DisconnectHandler.Handle;

        // changes to cleared flight level do not register an FDR update
        // we need to create custom handlers to be able to update the label/strip
        FdrPropertyChangesListener.RegisterAllHandlers();
    }

    public void OnFDRUpdate(FDP2.FDR updated)
    {
        FdrUpdateProcessor.ProcessFdrUpdate(updated);
    }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated)
    {
        RadarTrackUpdateProcessor.ProcessRadarTrackUpdate(updated);
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