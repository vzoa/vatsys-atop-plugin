using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using AtopPlugin.Display;
using AtopPlugin.State;
using AtopPlugin.UI;
using vatsys;
using vatsys.Plugin;

namespace AtopPlugin
{

    [Export(typeof(IPlugin))]
    public class AtopPlugin : IPlugin
    {
        private DateTime lastFdrUpdate = DateTime.MinValue;
        private TimeSpan cooldownDuration = TimeSpan.FromSeconds(10);

        public AtopPlugin()
        {
            RegisterEventHandlers();
            AtopMenu.Initialize();
            TempActivationMessagePopup.PopUpActivationMessageIfFirstTime();
        }

        public string Name => "ATOP Plugin";

        public CustomLabelItem? GetCustomLabelItem(string itemType, Track track, FDP2.FDR flightDataRecord,
            RDP.RadarTrack radarTrack)
        {
            return LabelItemRenderer.RenderLabelItem(itemType, track, flightDataRecord, radarTrack);
        }

        public CustomStripItem? GetCustomStripItem(string itemType, Track track, FDP2.FDR flightDataRecord,
            RDP.RadarTrack radarTrack)
        {
            return StripItemRenderer.RenderStripItem(itemType, track, flightDataRecord, radarTrack);
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            if (DateTime.Now - lastFdrUpdate < cooldownDuration)
            {
                return; // Ignore the update if cooldown duration hasn't passed
            }

            lastFdrUpdate = DateTime.Now;

            _ = AtopPluginStateManager.ProcessFdrUpdate(updated);
            _ = AtopPluginStateManager.ProcessDisplayUpdate(updated);
            _ = AtopPluginStateManager.RunConflictProbe(updated);
            FdrPropertyChangesListener.RegisterHandler(updated);

            // don't manage jurisdiction if not connected as ATC
            if (Network.Me.IsRealATC) _ = JurisdictionManager.HandleFdrUpdate(updated);
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            // don't manage jurisdiction if not connected as ATC
            if (Network.Me.IsRealATC) _ = JurisdictionManager.HandleRadarTrackUpdate(updated);
        }

        public CustomColour? SelectASDTrackColour(Track track)
        {
            return TrackColorRenderer.GetAsdColor(track);
        }

        public CustomColour? SelectGroundTrackColour(Track track)
        {
            return null;
        }

        private static void RegisterEventHandlers()
        {
            Network.PrivateMessagesChanged += PrivateMessagesChangedHandler.Handle;
            Network.Disconnected += DisconnectHandler.Handle;

            // changes to cleared flight level do not register an FDR update
            // we need to create custom handlers to be able to update the label/strip
            FdrPropertyChangesListener.RegisterAllHandlers();
        }
    }
}