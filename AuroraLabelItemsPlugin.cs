using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition; //<--Need to add a reference to System.ComponentModel.Composition
using System.Text.RegularExpressions;
using vatsys;
using vatsys.Plugin;

//Note the reference to vatsys (set Copy Local to false) ----->

namespace AuroraLabelItemsPlugin
{
    [Export(typeof(IPlugin))]

    public class AuroraLabelItemsPlugin : ILabelPlugin
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
        const string LABEL_ITEM_FILED_SPEED = "AURORA_FILEDSPEED"; //field m(4)
        const string LABEL_ITEM_3DIGIT_GROUNDSPEED = "AURORA_GROUNDSPEED"; //field n(5)
        readonly static CustomColour EastboundColour = new CustomColour(240, 255, 255);
        readonly static CustomColour WestboundColour = new CustomColour(240, 231, 140);
        readonly static CustomColour NonRVSM = new CustomColour(242, 133, 0);
        readonly static CustomColour Probe = new CustomColour(0, 255, 0);
        readonly static CustomColour NotCDA = new CustomColour(100, 0, 100);
        readonly ConcurrentDictionary<string, bool> eastboundCallsigns = new ConcurrentDictionary<string, bool>();
        readonly ConcurrentDictionary<string, char> adsbcpdlcValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, char> rvsmValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, char> altValues = new ConcurrentDictionary<string, char>();
        /// Plugin Name
        public string Name { get => "Aurora Label Items"; }

        /// This is called each time a flight data record is updated
        /// Here we are updating the eastbound callsigns dictionary with each flight data record
        /// When the FDR is updated we check if it still exists in the Flight Data Processor and remove from our dictionary if not. Otherwise we do some simple regex matching to find 
        /// the flight planned PBN category and store the character we want to display in the label in the dictionary.
        public void OnFDRUpdate(FDP2.FDR updated)
        {
            if (FDP2.GetFDRIndex(updated.Callsign) == -1)
            {
                eastboundCallsigns.TryRemove(updated.Callsign, out _);
                adsbcpdlcValues.TryRemove(updated.Callsign, out _);
                altValues.TryRemove(updated.Callsign, out _);
            }
            else
            {
                bool cpdlc = Regex.IsMatch(updated.AircraftEquip, @"J5") || Regex.IsMatch(updated.AircraftEquip, @"J7");
                bool adsc = Regex.IsMatch(updated.AircraftSurvEquip, @"D1");
                int cfl;
                bool isCfl = Int32.TryParse(updated.CFLString, out cfl);
                var vs = updated.PredictedPosition.VerticalSpeed;
                int level = updated.PRL / 100;


                char c4 = default;

                if (!updated.ADSB && cpdlc)

                    c4 = '⧆';

                else if (!updated.ADSB)

                    c4 = '⎕';

                else if (cpdlc)

                    c4 = '*';


                adsbcpdlcValues.AddOrUpdate(updated.Callsign, c4, (k, v) => c4);

                char h1 = default;

                if (level == updated.RFL)//level
                    h1 = default;

                else if (cfl > level || vs > 300)//Issued or trending climb
                    h1 = '↑';

                else if (cfl > 0 && cfl < level || vs < -300)//Issued or trending descent
                    h1 = '↓';

                else if (level - updated.RFL / 100 >= 3)//deviating above
                    h1 = '+';

                else if (level - updated.RFL / 100 <= -3)//deviating below
                    h1 = '-';

                //Track.TrackTypes.TRACK_TYPE_FP

                altValues.AddOrUpdate(updated.Callsign, h1, (k, v) => h1);



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


        ///  Could use the new position of the radar track or its change in state (cancelled, etc.) to do some processing. 
        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {

        }

        /// vatSys calls this function when it encounters a custom label item (defined in Labels.xml) during the label rendering.
        /// itemType is the value of the Type attribute in Labels.xml
        /// If it's not our item being called (another plugins, for example), return null.
        /// As a general rule, don't do processing in here as you'll slow down the ASD refresh. In the case of parsing a level to a string though, that's fine.
        public CustomLabelItem GetCustomLabelItem(string itemType, Track track, FDP2.FDR flightDataRecord, RDP.RadarTrack radarTrack)
        {

            if (flightDataRecord == null || track == null)
                return null;

            char c4;
            adsbcpdlcValues.TryGetValue(flightDataRecord.Callsign, out c4);
            char h1;
            altValues.TryGetValue(flightDataRecord.Callsign, out h1);

            switch (itemType)
            {
                case LABEL_ITEM_COMM_ICON:

                    return new CustomLabelItem()
                    {
                        Text = "⬜"  //⬜▼⟏
                    };

                case LABEL_ITEM_ADSB_CPDLC:

                    bool useCustomForeColour = track.State == MMI.HMIStates.Preactive || track.State == MMI.HMIStates.Announced;

                    if (useCustomForeColour)
                    {
                        return new CustomLabelItem()
                        {
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = NotCDA,
                            Text = c4.ToString()
                        };
                    }
                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = c4.ToString()
                        };
                    }

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


                //case LABEL_ITEM_RESTR:
                //
                //    if (Regex.IsMatch(flightDataRecord.GlobalOpData, @"AT \d\d\d\d"))
                //
                //        return new CustomLabelItem()
                //        {
                //            Text = "x"
                //        };
                //
                //    return null;

                case LABEL_ITEM_VMI:
                    bool isNonRVSMOrNewCFL = !flightDataRecord.RVSM || track.NewCFL;

                    if (isNonRVSMOrNewCFL)
                    {
                        return new CustomLabelItem()
                        {
                            Text = h1.ToString(),
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = !flightDataRecord.RVSM ? NonRVSM : Probe
                        };
                    }
                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = h1.ToString()
                        };
                    }


                case LABEL_ITEM_CLEARED_LEVEL:

                    return new CustomLabelItem()
                    {
                        Text = (track.NewCFL && radarTrack.ReachedCFL) ? string.Empty : flightDataRecord.CFLString,
                        ForeColourIdentity = Colours.Identities.Custom,
                        CustomForeColour = !flightDataRecord.RVSM ? NonRVSM : Probe
                    };

                case LABEL_ITEM_RADAR_IND:

                    return new CustomLabelItem()
                    {
                        Text = "◦"//★
                    };

                case LABEL_ITEM_FILED_SPEED:
                    return new CustomLabelItem()
                    {
                        Text = "N" + flightDataRecord.TAS
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
    }
}