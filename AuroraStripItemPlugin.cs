using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition; //<--Need to add a reference to System.ComponentModel.Composition
using System.Text.RegularExpressions;
using vatsys;
using vatsys.Plugin;
using static vatsys.Performance;

namespace AuroraStripItemsPlugin
{
    [Export(typeof(IPlugin))]

    public class AuroraStripItemsPlugin : IStripPlugin
    {

        /// The name of the custom label item we've added to Labels
        /// in the Profile
        const string LABEL_ITEM_ADSB_CPDLC = "AURORA_ADSB_CPDLC"; //field c(4)
        const string STRIP_ITEM_CALLSIGN = "AURORA_CALLSIGN";
        const string STRIP_ITEM_T10_FLAG = "AURORA_T10_FLAG";
        const string STRIP_ITEM_MNT_FLAG = "AURORA_MNT_FLAG";
        const string STRIP_ITEM_DIST_FLAG = "AURORA_DIST_FLAG";
        const string STRIP_ITEM_RVSM_FLAG = "AURORA_RVSM_FLAG";
        const string STRIP_ITEM_VMI = "AURORA_STRIP_VMI";
        const string STRIP_ITEM_MAN_EST = "AURORA_MAN_EST";
        const string STRIP_ITEM_LATERAL_FLAG = "AURORA_LATERAL_FLAG";
        const string STRIP_ITEM_ANNOT_IND = "AURORA_ANNOT_STRIP";
        readonly static CustomColour EastboundColour = new CustomColour(240, 255, 255);
        readonly static CustomColour WestboundColour = new CustomColour(240, 231, 140);
        readonly static CustomColour NonRVSM = new CustomColour(242, 133, 0);
        readonly static CustomColour SepFlags = new CustomColour(0, 196, 253);
        readonly static CustomColour Probe = new CustomColour(0, 255, 0);
        readonly static CustomColour NotCDA = new CustomColour(100, 0, 100);
        readonly ConcurrentDictionary<string, bool> eastboundCallsigns = new ConcurrentDictionary<string, bool>();
        /// Plugin Name
        public string Name { get => "Aurora Label Items"; }

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

        public static bool MachNumberTech(PerformanceData mnt)
        {
            if (mnt == null) return false;
            return mnt.IsJet;
        }

        //public void Estimates(FDP2.FDR.ExtractedRoute.Segment estimate)
        //{ 
        //    estimate.IsPETO = true;
        //}

        private void ItemMouseClick(CustomLabelItemMouseClickEventArgs e)
        {
            e.Item.CustomForeColour = SepFlags;
            e.Handled = true;
        }
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

                case STRIP_ITEM_T10_FLAG:

                    if (MachNumberTech(flightDataRecord.PerformanceData))

                        return new CustomStripItem()
                        {
                            //BackColourIdentity = Colours.Identities.Custom,
                            Text = "M",
                            OnMouseClick = ItemMouseClick
                        };

                    return null;

                case STRIP_ITEM_MNT_FLAG:

                    if (MachNumberTech(flightDataRecord.PerformanceData))

                        return new CustomStripItem()
                        {
                            //BackColourIdentity = Colours.Identities.Custom,
                            Text = "R",
                            OnMouseClick = ItemMouseClick
                        };

                    return null;

                case STRIP_ITEM_DIST_FLAG:

                    if (adsc & cpdlc & rnp4 || rnp10)


                        return new CustomStripItem()
                        {
                            //BackColourIdentity = Colours.Identities.Custom,
                            Text = rnp4 ? "3" : "D",
                            OnMouseClick = ItemMouseClick
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

                case STRIP_ITEM_VMI:
                    var vs = radarTrack == null ? flightDataRecord.PredictedPosition.VerticalSpeed : radarTrack.VerticalSpeed;

                    if (level == cfl || level == flightDataRecord.RFL)//level

                        return new CustomStripItem()
                        {
                            Text = ""
                        };
                    else if (cfl > level && track.NewCFL || vs > 300)//Issued or trending climb

                        return new CustomStripItem()
                        {
                            Text = "↑",
                        };

                    else if (cfl > 0 && cfl < level && track.NewCFL || vs < -300)//Issued or trending descent

                        return new CustomStripItem()
                        {
                            Text = "↓",
                        };

                    return null;

                //case STRIP_ITEM_MAN_EST:

                //if (Estimates)
                //    return new CustomStripItem()
                //    {
                //        BackColourIdentity = Colours.Identities.HighlightedText
                //    };
                //return null;

                case STRIP_ITEM_ANNOT_IND:

                    return new CustomStripItem()
                    {
                        Text = "." //&
                    };


                case STRIP_ITEM_LATERAL_FLAG:

                    if (adsc & cpdlc & rnp4 || rnp10)


                        return new CustomStripItem()
                        {
                            //BackColourIdentity = Colours.Identities.Custom,
                            Text = rnp4 ? "4" : "R",
                            OnMouseClick = ItemMouseClick
                        };
                    return null;

                default: return null;
            }
        }
    }
}
