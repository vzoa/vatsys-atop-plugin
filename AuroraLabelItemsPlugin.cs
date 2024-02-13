//<--Need to add a reference to System.ComponentModel.Composition
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using vatsys;
using vatsys.Plugin;
using static vatsys.FDP2;
using static vatsys.SectorsVolumes;


//Note the reference to vatsys (set Copy Local to false) ----->

namespace AuroraLabelItemsPlugin
{
    [Export(typeof(IPlugin))]
    public class AuroraLabelItemsPlugin : ILabelPlugin
    {
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


        public AuroraLabelItemsPlugin()
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
            var pbn = Regex.Match(updated.Remarks, @"PBN\/\w+\s");
            var rnp10 = pbn.Value.Contains("A1");
            var rnp4 = pbn.Value.Contains("L1");
            var cpdlc = updated.AircraftEquip.Contains("J5") || updated.AircraftEquip.Contains("J7");
            var adsc = updated.AircraftSurvEquip.Contains("D1");
            var jet = updated.PerformanceData?.IsJet ?? false;
            var vs = updated.PredictedPosition.VerticalSpeed;
            var prl = updated.PRL / 100;
            var cfl = updated.CFLUpper;
            var rfl = updated.RFL;
            var alt = cfl == -1 ? rfl : cfl;


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

                if (!updated.ADSB && cpdlc)

                    c1 = '⧆';

                else if (!updated.ADSB)

                    c1 = '⎕';

                else if (cpdlc)

                    c1 = '✱';


                adsbcpdlcValues.AddOrUpdate(updated.Callsign, c1, (k, v) => c1);

                char c2 = default;

                if (adsc & cpdlc & rnp4)
                    c2 = '3';

                else if (adsc & cpdlc & rnp10)
                    c2 = 'D';

                adsflagValues.AddOrUpdate(updated.Callsign, c2, (k, v) => c2);

                char c3 = default;

                if (updated.PerformanceData?.IsJet ?? false)
                    c3 = 'M';

                mntflagValues.AddOrUpdate(updated.Callsign, c3, (k, v) => c3);

                NetworkPilot code;
                code = new NetworkPilot();

                var d1 = string.Empty;

                if (!rnp10 || !rnp4)
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

                if (Math.Abs(updated.PRL - alt) <= 299) //level flight
                    h1 = default;

                else if (alt / 100 > prl || vs > 300) //Issued or trending climb
                    h1 = '↑';

                else if ((alt / 100 > 0 && alt < prl) || vs < -300) //Issued or trending descent
                    h1 = '↓';

                else if (prl - alt / 100 >= 3) //deviating above
                    h1 = '+';

                else if (prl - alt / 100 <= -3) //deviating below
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

                TransferOfControl(updated);

                var cpar = new CPAR();

                if (jet & rnp4 & cpdlc & adsc) ///Using CalculateAreaofConflict method against intruder aircraft
                    cpar.ConflictProbe(updated, 23);
                else if (rnp4 || rnp10)
                    cpar.ConflictProbe(updated, 50);
                else if (rnp10)
                    cpar.ConflictProbe(updated, 75);
                else
                    cpar.ConflictProbe(updated, 100);
            }
        }

        //void SendNextDataAuthority(Network network, NetworkPilot pilot, VSCSFrequency freq)
        //{
        //
        //    if (network.ValidATC && network.HaveConnection && !((DateTime.UtcNow - pilot.LastContactMe).TotalMinutes < 1.0))
        //    {
        //        string text = "NEXT DATA AUTHORITY KZAK";
        //        if (freq.AliasFrequency != VSCSFrequency.None.Frequency)
        //        {
        //            text = text + " (" + Conversions.FrequencyToString(freq.AliasFrequency) + ")"; 
        //Network.Instance.SendRadioMessage(radioMessage.FSDFrequencies, radioMessage.Address, "Unable", ThisRequiresResponse: false, radioMessage, CloseRequest: true);
        //        }
        //
        //        network.SendTextMessage(pilot.Callsign, text);
        //        pilot.LastContactMe = DateTime.UtcNow;
        //    }
        //}


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
            var pbn = Regex.Match(flightDataRecord.Remarks, @"PBN\/\w+\s");
            var rnp10 = pbn.Value.Contains("A1");
            var rnp4 = pbn.Value.Contains("L1");
            var prl = flightDataRecord.PRL / 100;
            var cfl = flightDataRecord.CFLUpper / 100;
            var rfl = flightDataRecord.RFL / 100;
            var alt = flightDataRecord.CFLUpper == -1 ? rfl : cfl;
            var notProbe = flightDataRecord.State == (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED)
                ? BorderFlags.All
                : BorderFlags.None;
            var colour = !flightDataRecord.RVSM ? CustomColors.NonRVSM : ExcludeConflictColor(track, flightDataRecord);
            var excludeConflict = SelectASDTrackColour(track) == GetConflictColour(flightDataRecord.Callsign);

            switch (itemType)

            {
                case LabelConstants.LABEL_ITEM_SELECT_HORI:

                    if (MMI.SelectedTrack?.GetFDR()?.Callsign == track.GetFDR().Callsign)
                        return new CustomLabelItem
                        {
                            Text = "",
                            Border = BorderFlags.Bottom
                        };

                    return null;

                case LabelConstants.LABEL_ITEM_SELECT_VERT:

                    if (MMI.SelectedTrack?.GetFDR()?.Callsign == track.GetFDR().Callsign)
                        return new CustomLabelItem
                        {
                            Text = "",
                            Border = BorderFlags.Left
                        };

                    return null;

                case LabelConstants.LABEL_ITEM_COMM_ICON:

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
                case LabelConstants.LABEL_ITEM_ADSB_CPDLC:

                    var useCustomForeColour =
                        flightDataRecord.State == (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED);

                    if (useCustomForeColour)
                        return new CustomLabelItem
                        {
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = CustomColors.NotCDA,
                            Text = c1.ToString()
                        };
                    return new CustomLabelItem
                    {
                        Text = c1.ToString(),
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };

                case LabelConstants.LABEL_ITEM_ADS_FLAGS:

                    return new CustomLabelItem
                    {
                        Text = c2.ToString(),
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };


                case LabelConstants.LABEL_ITEM_MNT_FLAGS:

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


                case LabelConstants.LABEL_ITEM_SCC:

                {
                    return new CustomLabelItem
                    {
                        Text = d1,
                        ForeColourIdentity = Colours.Identities.Custom,
                        CustomForeColour = CustomColors.SpecialConditionCode
                    };
                }


                case LabelConstants.LABEL_ITEM_ANNOT_IND:
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

                case LabelConstants.LABEL_ITEM_RESTR:

                    if (flightDataRecord.LabelOpData.Contains("AT ") || flightDataRecord.LabelOpData.Contains(" BY ") ||
                        flightDataRecord.LabelOpData.Contains("CLEARED TO "))

                        return new CustomLabelItem
                        {
                            Text = "x",
                            ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                            CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                        };

                    return null;

                case LabelConstants.LABEL_ITEM_LEVEL:

                    var level = prl == -1 ? null : (radarTrack.CorrectedAltitude / 100).ToString();


                    return new CustomLabelItem
                    {
                        Text = level,
                        Border = notProbe,
                        BorderColourIdentity = Colours.Identities.Custom,
                        CustomBorderColour = CustomColors.NotCDA,
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = colour
                    };

                case LabelConstants.LABEL_ITEM_VMI:

                    return new CustomLabelItem
                    {
                        Text = h1.ToString(),
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = colour
                    };


                case LabelConstants.LABEL_ITEM_CLEARED_LEVEL:


                    if (radarTrack.ReachedCFL || prl == alt || Math.Abs(flightDataRecord.PRL - alt) < 300)
                        return new CustomLabelItem
                        {
                            Text = ""
                        };

                    return new CustomLabelItem
                    {
                        Text = alt.ToString(),
                        Border = notProbe,
                        BorderColourIdentity = Colours.Identities.Custom,
                        CustomBorderColour = CustomColors.NotCDA,
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

                case LabelConstants.LABEL_ITEM_RADAR_IND:


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

                case LabelConstants.LABEL_ITEM_INHIBIT_IND:


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


                case LabelConstants.LABEL_ITEM_FILED_SPEED:
                    var mach = Conversions.CalculateMach(flightDataRecord.TAS,
                        GRIB.FindTemperature(flightDataRecord.PRL, track.GetLocation(), true));
                    return new CustomLabelItem
                    {
                        Text = "M" + Convert.ToDecimal(mach).ToString("F2").Replace(".", ""),
                        //Text = "N" + flightDataRecord.TAS
                        ForeColourIdentity = excludeConflict ? Colours.Identities.Custom : default,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)
                    };

                case LabelConstants.LABEL_ITEM_3DIGIT_GROUNDSPEED:
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

                case LabelConstants.LABEL_ITEM_DESTINATION:

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


        private static void TOC(RDP.RadarTrack rt)
        {
            var fdr = rt.CoupledFDR;
            if (fdr != null && rt != null) TransferOfControl(fdr);
        }


        private static void TransferOfControl(FDR fdr)
        {
            TOC toc;
            toc = new TOC(fdr);

            var rt = fdr.CoupledTrack;
            if (fdr != null)
                if (!fdr.ESTed && MMI.IsMySectorConcerned(fdr))
                    MMI.EstFDR(fdr);

            if (MMI.SectorsControlled.ToList()
                    .Exists(s => s.IsInSector(fdr.GetLocation(), fdr.PRL)) && !fdr.IsTrackedByMe &&
                !fdr.IsTracked) //MMI.SectorsControlled.Contains(fdr.ControllingSector) || fdr.ControllingSector == null
                MMI.AcceptJurisdiction(fdr);


            foreach (var volume in Volumes)
                if (rt != null && (fdr.PRL == -1 || (fdr.PRL <= volume.UpperLevel && fdr.PRL > volume.LowerLevel)) &&
                    Conversions.IsLatlonInPoly(fdr.GetLocation(), volume.Boundary))
                {
                    var whichVol = volume;
                    var whichFIR = FindSector(whichVol);
                    if (MMI.GetSectorEntryTime(fdr) == DateTime.MaxValue && MMI.SectorsControlled.ToList()
                            .TrueForAll(s =>
                                !s.IsInSector(fdr.GetLocation(), fdr.PRL))) // && whichFIR != fdr.ControllingSector 

                    {
                        //toc.HandoffNextSector();
                        MMI.HandoffJurisdiction(fdr, whichFIR);
                        MMI.Inhibit(fdr);
                        Thread.Sleep(300000);
                        RDP.DeCouple(rt);
                    }
                }
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
                    return CustomColors.EastboundColour;
                return CustomColors.WestboundColour;
            }

            return default;
        }

        private CustomColour GetConflictColour(string callsign)
        {
            if (imminentConflict.Count % 2 == 0 && imminentConflict.TryGetValue(callsign, out _))
                return CustomColors.Imminent;
            if (advisoryConflict.Count % 2 == 0 && advisoryConflict.TryGetValue(callsign, out _)) return CustomColors.Advisory;

            return default;
        }

        private CustomColour ExcludeConflictColor(Track track, FDR flightDataRecord)
        {
            if ((imminentConflict.Count > 0 || advisoryConflict.Count > 0) && track.State == MMI.HMIStates.Jurisdiction)
                return GetDirectionColour(flightDataRecord.Callsign, flightDataRecord);
            if (imminentConflict.Count > 0 || advisoryConflict.Count > 0) return CustomColors.ApsBlue;
            return default;
        }
    }
}