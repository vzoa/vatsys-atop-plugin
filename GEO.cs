//<--Need to add a reference to System.ComponentModel.Composition
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using vatsys;
using vatsys.Plugin;
using static vatsys.FDP2;


//Note the reference to vatsys (set Copy Local to false) ----->

namespace AuroraLabelItemsPlugin
{
    [Export(typeof(IPlugin))]
    public class GEO : ILabelPlugin
    {
        /// The name of the custom label item we've added to Labels
        /// in the Profile
        private const string LABEL_ITEM_SELECT_HORI = "SELECT_HORI";

        private const string LABEL_ITEM_SELECT_VERT = "SELECT_VERT";
        private const string LABEL_ITEM_COMM_ICON = "AURORA_COMM_ICON"; //field a(2)
        private const string LABEL_ITEM_ADSB_CPDLC = "AURORA_ADSB_CPDLC"; //field c(4)
        private const string LABEL_ITEM_ADS_FLAGS = "AURORA_ADS_FLAGS"; //field c(4)
        private const string LABEL_ITEM_MNT_FLAGS = "AURORA_MNT_FLAGS"; //field c(4)
        private const string LABEL_ITEM_SCC = "AURORA_SCC"; //field d(5)
        private const string LABEL_ITEM_ANNOT_IND = "AURORA_ANNOT_IND"; //field e(1)
        private const string LABEL_ITEM_RESTR = "AURORA_RESTR"; //field f(1)
        private const string LABEL_ITEM_LEVEL = "AURORA_LEVEL"; //field g(3)
        private const string LABEL_ITEM_VMI = "AURORA_VMI"; //field h(1)
        private const string LABEL_ITEM_CLEARED_LEVEL = "AURORA_CLEARED_LEVEL"; //field i(7)
        private const string LABEL_ITEM_HANDOFF_IND = "AURORA_HO_IND"; //field j(4)
        private const string LABEL_ITEM_RADAR_IND = "AURORA_RADAR_IND"; //field k(1)
        private const string LABEL_ITEM_INHIBIT_IND = "AURORA_INHIBIT_IND"; //field l(1)
        private const string LABEL_ITEM_FILED_SPEED = "AURORA_FILEDSPEED"; //field m(4)
        private const string LABEL_ITEM_3DIGIT_GROUNDSPEED = "AURORA_GROUNDSPEED"; //field n(5)
        private const string LABEL_ITEM_DESTINATION = "AURORA_DESTINATION"; //field o(4)
        private static readonly CustomColour EastboundColour = new CustomColour(240, 255, 255);
        private static readonly CustomColour WestboundColour = new CustomColour(240, 231, 140);
        private static readonly CustomColour NonRVSM = new CustomColour(242, 133, 0);
        private static readonly CustomColour Probe = new CustomColour(0, 255, 0);
        private static readonly CustomColour NotCDA = new CustomColour(100, 0, 100);
        private static readonly CustomColour Advisory = new CustomColour(255, 165, 0);
        private static readonly CustomColour Imminent = new CustomColour(255, 0, 0);
        private static readonly CustomColour SpecialConditionCode = new CustomColour(255, 255, 0);
        private static readonly CustomColour ApsBlue = new CustomColour(141, 182, 205);
        private readonly ConcurrentDictionary<string, char> adsbcpdlcValues = new ConcurrentDictionary<string, char>();
        private readonly ConcurrentDictionary<string, char> adsflagValues = new ConcurrentDictionary<string, char>();

        public readonly ConcurrentDictionary<string, HashSet<string>> advisoryConflict =
            new ConcurrentDictionary<string, HashSet<string>>();

        private readonly ConcurrentDictionary<string, char> altValues = new ConcurrentDictionary<string, char>();
        private readonly ConcurrentDictionary<string, byte> downlink = new ConcurrentDictionary<string, byte>();

        private readonly ConcurrentDictionary<string, bool> eastboundCallsigns =
            new ConcurrentDictionary<string, bool>();

        public readonly ConcurrentDictionary<string, HashSet<string>> imminentConflict =
            new ConcurrentDictionary<string, HashSet<string>>();

        private readonly ConcurrentDictionary<string, byte> mntflagtoggle = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentDictionary<string, char> mntflagValues = new ConcurrentDictionary<string, char>();
        private readonly ConcurrentDictionary<string, byte> radartoggle = new ConcurrentDictionary<string, byte>();

        private readonly ConcurrentDictionary<string, string>
            sccflagValues = new ConcurrentDictionary<string, string>();


        public GEO()
        {
            Network.PrivateMessagesChanged += Network_PrivateMessagesChanged;
            Network.RadioMessageAcknowledged += Network_RadioMessageAcknowledged;
        }


        /// Plugin Name
        public string Name => "Aurora Label Items";

        /// This is called each time a flight data record is updated
        /// Here we are updating the eastbound callsigns dictionary with each flight data record
        /// When the FDR is updated we check if it still exists in the Flight Data Processor and remove from our dictionary if not. Otherwise we do some simple regex matching to find 
        /// the flight planned PBN category and store the character we want to display in the label in the dictionary.
        public void OnFDRUpdate(FDR updated)
        {
            var fpap = new FPAP();


            if (GetFDRIndex(updated.Callsign) == -1)
            {
                eastboundCallsigns.TryRemove(updated.Callsign, out _);
                adsbcpdlcValues.TryRemove(updated.Callsign, out _);
                adsflagValues.TryRemove(updated.Callsign, out _);
                mntflagValues.TryRemove(updated.Callsign, out _);
                sccflagValues.TryRemove(updated.Callsign, out _);
                altValues.TryRemove(updated.Callsign, out _);
                radartoggle.TryRemove(updated.Callsign, out _);
                mntflagtoggle.TryRemove(updated.Callsign, out _);
                downlink.TryRemove(updated.Callsign, out _);
                advisoryConflict.TryRemove(updated.Callsign, out _);
                imminentConflict.TryRemove(updated.Callsign, out _);
            }
            else
            {
                char c1 = default;

                if (!updated.ADSB && fpap.cpdlc)

                    c1 = '⧆';

                else if (!updated.ADSB)

                    c1 = '⎕';

                else if (fpap.cpdlc)

                    c1 = '✱';


                adsbcpdlcValues.AddOrUpdate(updated.Callsign, c1, (k, v) => c1);

                char c2 = default;

                if (fpap.adsc & fpap.cpdlc & fpap.rnp4)
                    c2 = '3';

                else if (fpap.adsc & fpap.cpdlc & fpap.rnp10)
                    c2 = 'D';

                adsflagValues.AddOrUpdate(updated.Callsign, c2, (k, v) => c2);

                char c3 = default;

                if (updated.PerformanceData?.IsJet ?? false)
                    c3 = 'M';

                mntflagValues.AddOrUpdate(updated.Callsign, c3, (k, v) => c3);

                NetworkPilot code;
                code = new NetworkPilot();

                var d1 = string.Empty;

                if (!fpap.rnp10 && !fpap.rnp4)
                    d1 = "RNP";

                else if (code.TransponderCode == 7700)
                    d1 = "EMG";

                else if (code.TransponderCode == 7600)
                    d1 = "RCF";

                else if (code.TransponderCode == 7777)
                    d1 = "MTI";

                else if (code.TransponderCode == 7700)
                    d1 = "EMG";

                sccflagValues.AddOrUpdate(updated.Callsign, d1, (k, v) => d1);

                char h1 = default;

                if (Math.Abs(updated.PRL - fpap.alt) <= 299) //level flight
                    h1 = default;

                else if (fpap.alt / 100 > fpap.prl || fpap.vs > 300) //Issued or trending climb
                    h1 = '↑';

                else if ((fpap.alt / 100 > 0 && fpap.alt < fpap.prl) || fpap.vs < -300) //Issued or trending descent
                    h1 = '↓';

                else if (fpap.prl - fpap.alt / 100 >= 3) //deviating above
                    h1 = '+';

                else if (fpap.prl - fpap.alt / 100 <= -3) //deviating below
                    h1 = '-';


                altValues.AddOrUpdate(updated.Callsign, h1, (k, v) => h1);

                if (updated.ParsedRoute.Count > 1)
                {
                    //calculate track from first route point to last (Departure point to destination point)
                    var rte = updated.ParsedRoute;
                    var trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong,
                        rte.Last().Intersection.LatLong);
                    var east = trk >= 0 && trk < 180;
                    eastboundCallsigns.AddOrUpdate(updated.Callsign, east, (c, e) => east);
                }

                FPAP.TransferOfControl(updated);

                var cpar = new CPAR();
                var reqLat = new CPAR.ConflictData();

                cpar.ConflictProbe(updated, reqLat.latSep);
            }
        }

        /// Could use the new position of the radar track or its change in state (cancelled, etc.) to do some processing.
        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
        }


        /// vatSys calls this function when it encounters a custom label item (defined in Labels.xml) during the label rendering.
        /// itemType is the value of the Type attribute in Labels.xml
        /// If it's not our item being called (another plugins, for example), return null.
        /// As a general rule, don't do processing in here as you'll slow down the ASD refresh. In the case of parsing a level to a string though, that's fine.
        public CustomLabelItem GetCustomLabelItem(string itemType, Track track, FDR flightDataRecord,
            RDP.RadarTrack radarTrack)
        {
            var fpap = new FPAP();

            if (flightDataRecord == null || radarTrack == null)
                return null;

            char c1;
            adsbcpdlcValues.TryGetValue(flightDataRecord.Callsign, out c1);
            char c2;
            adsflagValues.TryGetValue(flightDataRecord.Callsign, out c2);
            char c3;
            mntflagValues.TryGetValue(flightDataRecord.Callsign, out c3);
            string d1;
            sccflagValues.TryGetValue(flightDataRecord.Callsign, out d1);
            char h1;
            altValues.TryGetValue(flightDataRecord.Callsign, out h1);


            var radarToggled = radartoggle.TryGetValue(flightDataRecord.Callsign, out _);
            var mntflagToggled = mntflagtoggle.TryGetValue(flightDataRecord.Callsign, out _);
            var downLink = downlink.TryGetValue(flightDataRecord.Callsign, out _);
            var isNonRVSM = !flightDataRecord.RVSM;
            var notProbe = flightDataRecord.State == (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED)
                ? BorderFlags.All
                : BorderFlags.None;
            var colour = !flightDataRecord.RVSM ? NonRVSM : ExcludeConflictColor(track, flightDataRecord);
            var excludeConflict = SelectASDTrackColour(track) == GetConflictColour(flightDataRecord.Callsign);

            switch (itemType)

            {
                case LABEL_ITEM_SELECT_HORI:

                    if (MMI.SelectedTrack?.GetFDR()?.Callsign == track.GetFDR().Callsign)
                        return new CustomLabelItem
                        {
                            Text = "",
                            Border = BorderFlags.Bottom
                        };

                    return null;

                case LABEL_ITEM_SELECT_VERT:

                    if (MMI.SelectedTrack?.GetFDR()?.Callsign == track.GetFDR().Callsign)
                        return new CustomLabelItem
                        {
                            Text = "",
                            Border = BorderFlags.Left
                        };

                    return null;

                case LABEL_ITEM_COMM_ICON:

                    if (downLink)
                        return new CustomLabelItem
                        {
                            Text = "▼",
                            Border = BorderFlags.All,
                            ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                            CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                        };
                    return new CustomLabelItem
                    {
                        Text = "⬜",
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };
                case LABEL_ITEM_ADSB_CPDLC:

                    var useCustomForeColour =
                        flightDataRecord.State == (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED);

                    if (useCustomForeColour)
                        return new CustomLabelItem
                        {
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = NotCDA,
                            Text = c1.ToString()
                        };
                    return new CustomLabelItem
                    {
                        Text = c1.ToString(),
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };

                case LABEL_ITEM_ADS_FLAGS:

                    return new CustomLabelItem
                    {
                        Text = c2.ToString(),
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };


                case LABEL_ITEM_MNT_FLAGS:

                    if (mntflagToggled)
                        return new CustomLabelItem
                        {
                            Text = c3.ToString(),
                            ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                            CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                        };

                    return new CustomLabelItem
                    {
                        Text = ""
                    };


                case LABEL_ITEM_SCC:

                {
                    return new CustomLabelItem
                    {
                        Text = d1,
                        ForeColourIdentity = Colours.Identities.Custom,
                        CustomForeColour = SpecialConditionCode
                    };
                }


                case LABEL_ITEM_ANNOT_IND:
                    var scratch = string.IsNullOrEmpty(flightDataRecord.LabelOpData);

                    if (scratch)
                        return new CustomLabelItem
                        {
                            Text = "◦",
                            ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                            CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                        };

                    return new CustomLabelItem
                    {
                        Text = "&",
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };

                case LABEL_ITEM_RESTR:

                    if (flightDataRecord.LabelOpData.Contains("AT ") || flightDataRecord.LabelOpData.Contains(" BY ") ||
                        flightDataRecord.LabelOpData.Contains("CLEARED TO "))

                        return new CustomLabelItem
                        {
                            Text = "x",
                            ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                            CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                        };

                    return null;

                case LABEL_ITEM_LEVEL:

                    var level = fpap.prl == -1 ? null : (radarTrack.CorrectedAltitude / 100).ToString();


                    return new CustomLabelItem
                    {
                        Text = level,
                        Border = notProbe,
                        BorderColourIdentity = Colours.Identities.Custom,
                        CustomBorderColour = NotCDA,
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = colour
                    };

                case LABEL_ITEM_VMI:

                    return new CustomLabelItem
                    {
                        Text = h1.ToString(),
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = colour
                    };


                case LABEL_ITEM_CLEARED_LEVEL:


                    if (radarTrack.ReachedCFL || fpap.prl == fpap.alt ||
                        Math.Abs(flightDataRecord.PRL - fpap.alt) < 300)
                        return new CustomLabelItem
                        {
                            Text = ""
                        };

                    return new CustomLabelItem
                    {
                        Text = fpap.alt.ToString(),
                        Border = notProbe,
                        BorderColourIdentity = Colours.Identities.Custom,
                        CustomBorderColour = NotCDA,
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = colour
                    };

                //                case LABEL_ITEM_HANDOFF_IND:
//                
//                
//                    if (flightDataRecord.IsHandoff)
//                    {
//                        return new CustomLabelItem()
//                        {
//                            Text = "H" + flightDataRecord.HandoffSector.Name,
//                        };
//                    }
//                
//                    else if (flightDataRecord.State == FDR.FDRStates.STATE_HANDOVER_FIRST)
//                    {
//                        return new CustomLabelItem()
//                        {
//                            Text = "O" + flightDataRecord.HandoffSector.Name,
//                        };
//                    }
//                
//                    else
//                    {
//                        return new CustomLabelItem()
//                        {
//                            Text = ""
//                        };
//                    }

                case LABEL_ITEM_RADAR_IND:


                    if (radarToggled)
                        return new CustomLabelItem
                        {
                            Text = "★",
                            OnMouseClick = HandleRadarFlagClick,
                            ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                            CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                        };

                    return new CustomLabelItem
                    {
                        Text = "◦",
                        OnMouseClick = HandleRadarFlagClick,
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };

                case LABEL_ITEM_INHIBIT_IND:


                    if (flightDataRecord.State == FDR.FDRStates.STATE_INHIBITED)
                        return new CustomLabelItem
                        {
                            Text = "^",
                            ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                            CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                        };

                    return new CustomLabelItem
                    {
                        Text = ""
                    };


                case LABEL_ITEM_FILED_SPEED:
                    var mach = Conversions.CalculateMach(flightDataRecord.TAS,
                        GRIB.FindTemperature(flightDataRecord.PRL, track.GetLocation(), true));
                    return new CustomLabelItem
                    {
                        Text = "M" + Convert.ToDecimal(mach).ToString("F2").Replace(".", ""),
                        //Text = "N" + flightDataRecord.TAS
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };

                case LABEL_ITEM_3DIGIT_GROUNDSPEED:
                    //get groundspeed value from either FDR or radarTrack if coupled
                    var gs = radarTrack == null
                        ? flightDataRecord.PredictedPosition.Groundspeed
                        : radarTrack.GroundSpeed;
                    return new CustomLabelItem
                    {
                        Text = "N" + gs.ToString("000"), //format as 3 digits (with leading zeros)
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };

                case LABEL_ITEM_DESTINATION:

                    return new CustomLabelItem
                    {
                        Text = flightDataRecord.DesAirport,
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };

                default:
                    return null;
            }
        }

        public CustomColour SelectASDTrackColour(Track track)
        {
            var fdr = track.GetFDR();
            var fdr2 = track.GetFDR();
            //if track doesn't have an FDR coupled do nothing
            if (fdr == null)
                return null;


            //only apply East/West colour to jurisdiction state
            if (track.State ==
                MMI.HMIStates
                    .Jurisdiction) //read our dictionary of stored bools (true means is easterly) and return the correct colour
                return GetConflictColour(fdr.Callsign) ?? GetDirectionColour(fdr.Callsign, fdr);

            if (fdr.State !=
                (FDR.FDRStates.STATE_PREACTIVE |
                 FDR.FDRStates.STATE_COORDINATED)) //only apply conflict colours if planes are of concern
                return GetConflictColour(fdr.Callsign) ?? null;

            return default;
        }

        public CustomColour SelectGroundTrackColour(Track track)
        {
            return null;
        }


        private void Network_RadioMessageAcknowledged(object sender, RadioMessageEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Network_PrivateMessagesChanged(object sender, Network.GenericMessageEventArgs e)
        {
            var downLink = e.Message.Sent;

            if (downLink)
                downlink.TryRemove(e.Message.Address, out _);
            else
                downlink.TryAdd(e.Message.Address, 0);
        }

        private void HandleRadarFlagClick(CustomLabelItemMouseClickEventArgs e)
        {
            var radarToggled = radartoggle.TryGetValue(e.Track.GetFDR().Callsign, out _);

            if (radarToggled)
                radartoggle.TryRemove(e.Track.GetFDR().Callsign, out _);
            else
                radartoggle.TryAdd(e.Track.GetFDR().Callsign, 0);

            e.Handled = true;
        }

        private CustomColour GetDirectionColour(string callsign, FDR fdr)
        {
            if (eastboundCallsigns.TryGetValue(callsign, out var east))
            {
                if (east)
                    return EastboundColour;
                return WestboundColour;
            }

            return default;
        }

        private CustomColour GetConflictColour(string callsign)
        {
            if (imminentConflict.Count % 2 == 0 && imminentConflict.TryGetValue(callsign, out _))
                return Imminent;
            if (advisoryConflict.Count % 2 == 0 && advisoryConflict.TryGetValue(callsign, out _)) return Advisory;

            return default;
        }

        private CustomColour ExcludeConflictColor(Track track, FDR flightDataRecord)
        {
            if ((imminentConflict.Count > 0 || advisoryConflict.Count > 0) && track.State == MMI.HMIStates.Jurisdiction)
                return GetDirectionColour(flightDataRecord.Callsign, flightDataRecord);
            if (imminentConflict.Count > 0 || advisoryConflict.Count > 0) return ApsBlue;
            return default;
        }
    }
}