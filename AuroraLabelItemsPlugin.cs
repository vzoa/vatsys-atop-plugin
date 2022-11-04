using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using vatsys;
using vatsys.Plugin;
using System.Collections.Concurrent;
using System.ComponentModel.Composition; //<--Need to add a reference to System.ComponentModel.Composition
using static vatsys.Performance;
using static vatsys.Network;


//Note the reference to vatsys (set Copy Local to false) ----->

namespace AuroraLabelItemsPlugin
{
    [Export(typeof(IPlugin))]

    public class AuroraLabelItemsPlugin : ILabelPlugin, IStripPlugin
    {

        /// The name of the custom label item we've added to Labels
        /// in the Profile
        const string LABEL_ITEM_COMM_ICON = "AURORA_COMM_ICON"; //field a(2)
        const string LABEL_ITEM_ADSB_CPDLC = "AURORA_ADSB_CPDLC"; //field c(4)
        const string LABEL_ITEM_SCC = "AURORA_SCC";  //field c(5)
        const string LABEL_ITEM_ANNOT_IND = "AURORA_ANNOT_IND"; //field e(1)
        const string LABEL_ITEM_RESTR = "AURORA_RESTR"; //field f(1)
        const string LABEL_ITEM_VMI = "AURORA_VMI"; //field h(1)
        const string LABEL_ITEM_CLEARED_LEVEL = "AURORA_CLEARED_LEVEL"; //field i(7)
        const string LABEL_ITEM_RADAR_IND = "AURORA_RADAR_IND"; //field k(1)
        const string LABEL_ITEM_3DIGIT_GROUNDSPEED = "AURORA_GROUNDSPEED";
        const string STRIP_ITEM_CALLSIGN = "AURORA_CALLSIGN";
        const string STRIP_ITEM_DIST_FLAG = "AURORA_DIST_FLAG";
        const string STRIP_ITEM_RVSM_FLAG = "AURORA_RVSM_FLAG";
        const string STRIP_ITEM_MAN_EST = "AURORA_MAN_EST";
        readonly static CustomColour EastboundColour = new CustomColour(240, 255, 255);
        readonly static CustomColour WestboundColour = new CustomColour(240, 231, 140);
        readonly static CustomColour NonRVSM = new CustomColour(242, 133, 0);
        readonly static CustomColour SepFlags = new CustomColour(0, 196, 253);
        readonly static CustomColour Probe = new CustomColour(0, 255, 0);
        readonly static CustomColour NotCDA = new CustomColour(100, 0, 100);
        readonly ConcurrentDictionary<string, bool> eastboundCallsigns = new ConcurrentDictionary<string, bool>();
        readonly ConcurrentDictionary<string, char> pbnValues = new ConcurrentDictionary<string, char>();

        /// Plugin Name
        public string Name { get => "Aurora Label Items"; }

        /// This is called each time a flight data record is updated
        /// Here we are updating the eastbound callsigns dictionary with each flight data record
        /// When the FDR is updated we check if it still exists in the Flight Data Processor and remove from our dictionary if not. Otherwise we do some simple regex matching to find 
        /// the flight planned PBN category and store the character we want to display in the label in the dictionary.
        
        public void OnFDRUpdate(FDP2.FDR updated)
        {
            if (FDP2.GetFDRIndex(updated.Callsign) == -1) //FDR was removed (that's what triggered the update)
                eastboundCallsigns.TryRemove(updated.Callsign, out _);

            else

            {

                if (updated.ParsedRoute.Count > 1)
                {
                    //calculate track from first route point to last (Departure point to destination point)
                    var rte = updated.ParsedRoute;
                    double trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong, rte.Last().Intersection.LatLong);
                    bool east = trk >= 0 && trk < 180;
                    eastboundCallsigns.AddOrUpdate(updated.Callsign, east, (c, e) => east);
                }
            }
        }

        //internal void SendContactMe(NetworkPilot pilot, VSCSFrequency freq)
        //{
        //    if (validATC && HaveConnection && !((DateTime.UtcNow - pilot.LastContactMe).TotalMinutes < 1.0))
        //    {
        //        string text = "Please contact me on " + Conversions.FrequencyToString(freq.Frequency);
        //        if (freq.AliasFrequency != VSCSFrequency.None.Frequency)
        //        {
        //            text = text + " (" + Conversions.FrequencyToString(freq.AliasFrequency) + ")";
        //        }
        //
        //        SendTextMessage(pilot.Callsign, text);
        //        pilot.LastContactMe = DateTime.UtcNow;
        //    }
        //}

        /// This is called each time a radar track is updated
        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {

        }

        /// vatSys calls this function when it encounters a custom label item (defined in Labels.xml) during the label rendering.
        /// itemType is the value of the Type attribute in Labels.xml
        /// If it's not our item being called (another plugins, for example), return null.
        /// As a general rule, don't do processing in here as you'll slow down the ASD refresh. In the case of parsing a level to a string though, that's fine.
        public CustomLabelItem GetPrivateMessage(RadioMessage downLink, FDP2.FDR flightDataRecord)
        {

            if (downLink.Request & downLink.Address == flightDataRecord.Callsign)
                return new CustomLabelItem()
                {
                    Type = LABEL_ITEM_ADSB_CPDLC,
                    Text = "⬜▼⟏"
                };
                    return null;
        }

        public CustomLabelItem GetCustomLabelItem(string itemType, Track track, FDP2.FDR flightDataRecord, RDP.RadarTrack radarTrack)
        {

            if (flightDataRecord == null)
                return null;

            Match pbn = Regex.Match(flightDataRecord.Remarks, @"PBN\/\w+\s");
            bool cpdlc = Regex.IsMatch(flightDataRecord.AircraftEquip, @"J5") || Regex.IsMatch(flightDataRecord.AircraftEquip, @"J7");
            bool adsc = Regex.IsMatch(flightDataRecord.AircraftSurvEquip, @"D1");
            bool adsb = flightDataRecord.ADSB; //Regex.IsMatch(flightDataRecord.AircraftSurvEquip, @"B\d");
            bool rnp10 = Regex.IsMatch(pbn.Value, @"A1");
            bool rnp4 = Regex.IsMatch(pbn.Value, @"L1");
            bool rvsm = flightDataRecord.RVSM;
            int level = radarTrack == null ? flightDataRecord.PRL / 100 : radarTrack.CorrectedAltitude / 100;
            int cfl;
            bool isCfl = Int32.TryParse(flightDataRecord.CFLString, out cfl);
            //string sLevel = level.ToString("D3");
            //string ca = AlertTypes.STCA.ToString(@"CA");
            //string la = AlertTypes.MSAW.ToString(@"LA");
            //string ra = AlertTypes.DAIW.ToString(@"RA");
            //string rcf = AlertTypes.RAD.ToString(@"RCF");
            //string dup = AlertTypes.DAIW.ToString(@"DUP");
            //string pos = AlertTypes.MPR.ToString("POS");
            //string spd = AlertTypes.ETO.ToString(@"SPD");
            //var mti = transponder.TransponderCode = 7777;



            switch (itemType)
            {
                case LABEL_ITEM_COMM_ICON:
                    
                         return new CustomLabelItem()
                         {
                             Text = "⬜"
                         };

                case LABEL_ITEM_ADSB_CPDLC:


                    if (!adsb & cpdlc)

                        return new CustomLabelItem()
                        {
                            Text = "⧆"
                        };

                    else if (!adsb)

                        return new CustomLabelItem()
                        {
                            Text = "⎕"
                        };

                    else if (cpdlc & track.State == MMI.HMIStates.Preactive | track.State is MMI.HMIStates.Announced)

                        return new CustomLabelItem()
                        {
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = NotCDA,
                            Text = "*"           
                        };
                    else if (cpdlc)

                        return new CustomLabelItem()
                        {
                            Text = "*"
                        };

                    return null;


                //case LABEL_ITEM_SCC:
                //
                //        return new CustomLabelItem()
                //        {
                //            Text = pos //| ca | la | ra | rcf | dup | spd 
                //        };                
                                                  
                                         
                case LABEL_ITEM_ANNOT_IND:
                                         
                    return new CustomLabelItem()
                    {
                        Text = "◦" //&
                    };


                case LABEL_ITEM_RESTR:

                    if (Regex.IsMatch(flightDataRecord.GlobalOpData, @"AT \d\d\d\d"))

                    return new CustomLabelItem()
                    {
                        Text = "x"
                    };

                    return null;

                case LABEL_ITEM_VMI:


                    if (level == cfl || level == flightDataRecord.RFL)//level

                        return new CustomLabelItem()
                        {
                            Text = null
                        };

                    else if (cfl > level && track.NewCFL || radarTrack.VerticalSpeed > 300)//Issued or trending climb

                        return new CustomLabelItem()
                        {
                            Text = "↑",
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = !flightDataRecord.RVSM ? NonRVSM : track.NewCFL ? Probe : null,
                        };

                    else if (cfl > 0 && cfl < level && track.NewCFL || radarTrack.VerticalSpeed < -300)//Issued or trending descent

                        return new CustomLabelItem()
                        {
                            Text = "↓",
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = !flightDataRecord.RVSM ? NonRVSM : track.NewCFL ? Probe : null,
                        };

                    else if (level - flightDataRecord.RFL / 100 >= 3)//deviating above

                        return new CustomLabelItem()
                        {
                            Text = "+"
                        };

                    else if (level - flightDataRecord.RFL / 100 <= -3)//deviating below

                        return new CustomLabelItem()
                        {
                            Text = "-"
                        };

                    return null;

                //else if (Track.TrackTypes.TRACK_TYPE_FP & flightDataRecord.PRL > 0)//Manually entered PRL
                //
                //    return new CustomLabelItem()
                //    {
                //        Text = "R"
                //    };




                case LABEL_ITEM_CLEARED_LEVEL:

                    return new CustomLabelItem()
                    {
                        Text = level == cfl ? null : flightDataRecord.CFLString, // flightDataRecord.CFLLower + "B" + flightDataRecord.CFLUpper
                        ForeColourIdentity = Colours.Identities.Custom,
                        CustomForeColour = !flightDataRecord.RVSM ? NonRVSM : track.NewCFL ? Probe : null,
                    };

                case LABEL_ITEM_RADAR_IND:

                    return new CustomLabelItem()
                    {
                        Text = "◦",//★
                        //OnMouseClick = 
                    };


                case LABEL_ITEM_3DIGIT_GROUNDSPEED:
                    //get groundspeed value from either FDR or radarTrack if coupled
                    var gs = radarTrack == null ? flightDataRecord.PredictedPosition.Groundspeed : radarTrack.GroundSpeed;
                    return new CustomLabelItem()
                    {
                        Text = "N" + gs.ToString("000")//format as 3 digits (with leading zeros)
                    };

                default:
                    return null;
            }
        }
        private void ItemMouseClick(CustomLabelItemMouseClickEventArgs e)
        {
            e.Track.IQL = !e.Track.IQL;
            //MMI.ClickspotTypes.Label_Toggle;
            e.Handled = true;
        }
        public CustomLabelItem TrackSelectBox(Track a, MMI.ClickspotCategories b)  //box around selected tracks
        {
            //if (MMI.ClickspotTypes.Track_Select)
            return new CustomLabelItem()
            {
                Border = MMI.SelectedTrack != null ? BorderFlags.All : BorderFlags.None,
            };
        }
                          
        public CustomColour SelectASDTrackColour(Track track)
        {
            //only apply East/West colour to jurisdiction state
            if (track.State != MMI.HMIStates.Jurisdiction)
                return null;

            var fdr = track.GetFDR();
            //if track doesn't have an FDR coupled do nothing
            if (fdr == null)
                return null;

            //read our dictionary of stored bools (true means is easterly) and return the correct colour
            return GetDirectionColour(fdr.Callsign);
        }

        public CustomColour SelectGroundTrackColour(Track track)
        {
            return null;
        }

        private CustomColour GetDirectionColour(string callsign)
        {
            if (eastboundCallsigns.TryGetValue(callsign, out bool east))
            {
                if (east)
                    return EastboundColour;
                else
                    return WestboundColour;
            }

            return null;
        }
        public void MachNumberTech(string itemType, PerformanceData mnt)
        {
            mnt.IsJet = true;

        }

        //public void Estimates(FDP2.FDR.ExtractedRoute.Segment estimate)
        //{ 
        //    estimate.IsPETO = true;
        //}
        public CustomStripItem GetCustomStripItem(string itemType, Track track, FDP2.FDR flightDataRecord, RDP.RadarTrack radarTrack)
          
        {
            Match pbn = Regex.Match(flightDataRecord.Remarks, @"PBN\/\w+\s");
            bool cpdlc = Regex.IsMatch(flightDataRecord.AircraftEquip, @"J5") || Regex.IsMatch(flightDataRecord.AircraftEquip, @"J7");
            bool adsc = Regex.IsMatch(flightDataRecord.AircraftSurvEquip, @"D1");
            bool adsb = flightDataRecord.ADSB; //Regex.IsMatch(flightDataRecord.AircraftSurvEquip, @"B\d");
            bool rnp10 = Regex.IsMatch(pbn.Value, @"A1");
            bool rnp4 = Regex.IsMatch(pbn.Value, @"L1");
            bool rvsm = flightDataRecord.RVSM;
            int level = radarTrack == null ? flightDataRecord.PRL / 100 : radarTrack.CorrectedAltitude / 100;
            int cfl;
            bool isCfl = Int32.TryParse(flightDataRecord.CFLString, out cfl);
            bool isEastBound = true;

            if (flightDataRecord is null)
                return null;

            switch (itemType)
            {
                case STRIP_ITEM_CALLSIGN:

                    eastboundCallsigns.TryGetValue(flightDataRecord.Callsign, out isEastBound);

                    if (isEastBound)
                    {
                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.StripText,
                            ForeColourIdentity = Colours.Identities.StripBackground,
                            Text = flightDataRecord.Callsign
                        };
                    }
                    else
                    {
                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.StripBackground,
                            ForeColourIdentity = Colours.Identities.StripText,
                            Text = flightDataRecord.Callsign
                        };
                    }
                case LABEL_ITEM_ADSB_CPDLC:


                    if (isEastBound & !adsb & cpdlc)

                            return new CustomStripItem()
                            {
                                BackColourIdentity = Colours.Identities.StripText,
                                ForeColourIdentity = Colours.Identities.StripBackground,
                                Text = "⧆"
                            }; 

                    else if (!adsb & cpdlc)

                        return new CustomStripItem()
                        {
                            Text = "⧆"
                        };


                    else if (!adsb)

                        return new CustomStripItem()
                        {
                            Text = "⎕"
                        };

                    else if (cpdlc)

                        return new CustomStripItem()
                        {
                            Text = "*"
                        };

                    return null;

                case STRIP_ITEM_DIST_FLAG:


                    //if (MachNumberTech.isJet;)
                    //    return new CustomStripItem()
                    //    {
                    //        Text = "M" + "R"
                    //    };
                    //return null;

                    if (adsc & cpdlc & rnp4 || rnp10)

                        return new CustomStripItem()
                        {
                            Text = rnp4 ? "3" : "D"
                        };

                    return null;

                case STRIP_ITEM_RVSM_FLAG:

                    if (rvsm)

                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.Custom,
                            CustomBackColour = SepFlags,
                            Text = "W"
                        };
                    return null;

                //case STRIP_ITEM_MAN_EST:

                    //if (Estimates)
                    //    return new CustomStripItem()
                    //    {
                    //        BackColourIdentity = Colours.Identities.HighlightedText
                    //    };
                    //return null;

                default: return null;
            }
    }
    }
}