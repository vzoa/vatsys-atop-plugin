using AtopPlugin.Display;
using AtopPlugin.Helpers;
using AtopPlugin.State;
using AtopPlugin.UI;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using vatsys;
using vatsys.Plugin;

namespace AtopPlugin;

[Export(typeof(IPlugin))]
public class AtopPlugin : ILabelPlugin, IStripPlugin
{
    public AtopPlugin()
    {
        RegisterEventHandlers();
        AtopMenu.Initialize();
        SelectedLabelBoxBridge.Initialize();
        TempActivationMessagePopup.PopUpActivationMessageIfFirstTime();
        AtopPluginStateManager.Initialize(); // Initialize state manager to subscribe to webapp conflicts
        AtopWebSocketServer.Instance.Start();
        LocalWebServer.Start(); // Serve webapp and auto-open browser
        ConflictSegmentRenderer.Initialize(); // Initialize conflict segment rendering
        LabelDragOverrideHandler.Initialize(); // Allow unclamped label leader lengths
        DynamicSectorBoundaryRenderer.Initialize(); // Keep OSEC boundaries aligned to controlled sector volumes
    }

    public string Name => "ATOP Plugin";

    public CustomLabelItem? GetCustomLabelItem(string itemType, Track track, FDP2.FDR flightDataRecord,
        RDP.RadarTrack radarTrack)
    {
        try
        {
            return LabelItemRenderer.RenderLabelItem(itemType, track, flightDataRecord, radarTrack);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"GetCustomLabelItem: {ex.Message}", ex));
            return null;
        }
    }

    public CustomStripItem? GetCustomStripItem(string itemType, Track track, FDP2.FDR flightDataRecord,
        RDP.RadarTrack radarTrack)
    {
        try
        {
            return StripItemRenderer.RenderStripItem(itemType, track, flightDataRecord, radarTrack);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"GetCustomStripItem: {ex.Message}", ex));
            return null;
        }
    }

    public void OnFDRUpdate(FDP2.FDR updated)
    {
        try
        {
            ProbeRouteRenderer.EvaluateCpdlcReadback(updated.Callsign);
            ProposedProfileBridge.EvaluateCpdlcReadback(updated.Callsign);

            _ = AtopPluginStateManager.ProcessFdrUpdate(updated);
            _ = AtopPluginStateManager.ProcessDisplayUpdate(updated);

            FdrPropertyChangesListener.RegisterHandler(updated);

            // Don't manage jurisdiction if not connected as ATC
            if (Network.Me.IsRealATC)
            {
                _ = JurisdictionManager.HandleFdrUpdate(updated);
            }

            // Broadcast updated FDR and request conflict probe (event-driven per ATOP spec 12.1.1)
            Task.Run(async () =>
            {
                try
                {
                    await AtopWebSocketServer.Instance.BroadcastFlightPlanDataAsync(updated);
                    await AtopWebSocketServer.Instance.BroadcastFDRForConflictAsync(updated);
                    await AtopWebSocketServer.Instance.RequestProbeAsync(updated.Callsign);
                }
                catch (Exception ex)
                {
                    Errors.Add(new Exception($"OnFDRUpdate background task: {ex.Message}", ex));
                }
            });
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"OnFDRUpdate: {ex.Message}", ex));
        }
    }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated)
    {
        try
        {
            // don't manage jurisdiction if not connected as ATC
            if (Network.Me.IsRealATC) _ = JurisdictionManager.HandleRadarTrackUpdate(updated);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"OnRadarTrackUpdate: {ex.Message}", ex));
        }
    }

    public CustomColour? SelectASDTrackColour(Track track)
    {
        try
        {
            return TrackColorRenderer.GetAsdColor(track);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"SelectASDTrackColour: {ex.Message}", ex));
            return null;
        }
    }

    public CustomColour? SelectGroundTrackColour(Track track)
    {
        return null;
    }

    private static void RegisterEventHandlers()
    {
        Network.PrivateMessagesChanged += PrivateMessagesChangedHandler.Handle;
        Network.Disconnected += DisconnectHandler.Handle;
        Network.Disconnected += (_, _) => SelectedLabelBoxBridge.Shutdown();

        // changes to cleared flight level do not register an FDR update
        // we need to create custom handlers to be able to update the label/strip
        FdrPropertyChangesListener.RegisterAllHandlers();
    }
}