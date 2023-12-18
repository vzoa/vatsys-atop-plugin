using System;
using System.Threading;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Linq; //<--Need to add a reference to System.ComponentModel.Composition
using System.Text.RegularExpressions;
using vatsys;
using vatsys.Plugin;
using static vatsys.FDP2;
using static vatsys.SectorsVolumes;
using static vatsys.Network;
using System.Collections.Generic;
using System.Net;




//Note the reference to vatsys (set Copy Local to false) ----->

namespace AuroraLabelItemsPlugin
{
    [Export(typeof(IPlugin))]
    public class AuroraLabelItemsPlugin : ILabelPlugin
    {
        /// The name of the custom label item we've added to Labels
        /// in the Profile
        const string LABEL_ITEM_SELECT_HORI = "SELECT_HORI";
        const string LABEL_ITEM_SELECT_VERT = "SELECT_VERT";
        const string LABEL_ITEM_COMM_ICON = "AURORA_COMM_ICON"; //field a(2)
        const string LABEL_ITEM_ADSB_CPDLC = "AURORA_ADSB_CPDLC"; //field c(4)
        const string LABEL_ITEM_ADS_FLAGS = "AURORA_ADS_FLAGS"; //field c(4)
        const string LABEL_ITEM_MNT_FLAGS = "AURORA_MNT_FLAGS"; //field c(4)
        const string LABEL_ITEM_SCC = "AURORA_SCC"; //field d(5)
        const string LABEL_ITEM_ANNOT_IND = "AURORA_ANNOT_IND"; //field e(1)
        const string LABEL_ITEM_RESTR = "AURORA_RESTR"; //field f(1)
        const string LABEL_ITEM_LEVEL = "AURORA_LEVEL"; //field g(3)
        const string LABEL_ITEM_VMI = "AURORA_VMI"; //field h(1)
        const string LABEL_ITEM_CLEARED_LEVEL = "AURORA_CLEARED_LEVEL"; //field i(7)
        const string LABEL_ITEM_HANDOFF_IND = "AURORA_HO_IND"; //field j(4)
        const string LABEL_ITEM_RADAR_IND = "AURORA_RADAR_IND"; //field k(1)
        const string LABEL_ITEM_INHIBIT_IND = "AURORA_INHIBIT_IND"; //field l(1)
        const string LABEL_ITEM_FILED_SPEED = "AURORA_FILEDSPEED"; //field m(4)
        const string LABEL_ITEM_3DIGIT_GROUNDSPEED = "AURORA_GROUNDSPEED"; //field n(5)
        readonly static CustomColour EastboundColour = new CustomColour(240, 255, 255);
        readonly static CustomColour WestboundColour = new CustomColour(240, 231, 140);
        readonly static CustomColour NonRVSM = new CustomColour(242, 133, 0);
        readonly static CustomColour Probe = new CustomColour(0, 255, 0);
        readonly static CustomColour NotCDA = new CustomColour(100, 0, 100);
        readonly static CustomColour Advisory = new CustomColour(255, 165, 0);
        readonly static CustomColour Imminent = new CustomColour(255, 0, 0);
        readonly static CustomColour SpecialConditionCode = new CustomColour(255, 255, 0);
        readonly ConcurrentDictionary<string, bool> eastboundCallsigns = new ConcurrentDictionary<string, bool>();
        readonly ConcurrentDictionary<string, char> adsbcpdlcValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, char> adsflagValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, char> mntflagValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, string> sccflagValues = new ConcurrentDictionary<string, string>();
        readonly ConcurrentDictionary<string, char> altValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, byte> radartoggle = new ConcurrentDictionary<string, byte>();
        readonly ConcurrentDictionary<string, byte> mntflagtoggle = new ConcurrentDictionary<string, byte>();
        readonly ConcurrentDictionary<string, byte> downlink = new ConcurrentDictionary<string, byte>();
        readonly ConcurrentDictionary<string, HashSet<string>> advisoryConflict = new ConcurrentDictionary<string, HashSet<string>>();
        readonly ConcurrentDictionary<string, HashSet<string>> imminentConflict = new ConcurrentDictionary<string, HashSet<string>>();



        public AuroraLabelItemsPlugin()
        {
            Network.PrivateMessagesChanged += Network_PrivateMessagesChanged;
            Network.RadioMessageAcknowledged += Network_RadioMessageAcknowledged;
        }


        private void Network_RadioMessageAcknowledged(object sender, RadioMessageEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Network_PrivateMessagesChanged(object sender, Network.GenericMessageEventArgs e)
        {
            bool downLink = e.Message.Sent == true;

            if (downLink)
            {
                downlink.TryRemove(e.Message.Address, out _);
            }
            else
            {
                downlink.TryAdd(e.Message.Address, 0);
            }
        }


        /// Plugin Name
        public string Name
        {
            get => "Aurora Label Items";
        }

        /// This is called each time a flight data record is updated
        /// Here we are updating the eastbound callsigns dictionary with each flight data record
        /// When the FDR is updated we check if it still exists in the Flight Data Processor and remove from our dictionary if not. Otherwise we do some simple regex matching to find 
        /// the flight planned PBN category and store the character we want to display in the label in the dictionary.
        public void OnFDRUpdate(FDP2.FDR updated)
        {
            var pbn = Regex.Match(updated.Remarks, @"PBN\/\w+\s");
            bool rnp10 = pbn.Value.Contains ("A1");
            bool rnp4 = pbn.Value.Contains("L1");
            bool cpdlc = updated.AircraftEquip.Contains("J5") || updated.AircraftEquip.Contains("J7");
            bool adsc = updated.AircraftSurvEquip.Contains("D1");
            bool jet = updated.PerformanceData?.IsJet ?? false;
            var vs = updated.PredictedPosition.VerticalSpeed;
            int prl = updated.PRL / 100;
            int cfl = updated.CFLUpper;
            int rfl = updated.RFL;
            int alt = cfl == -1 ? rfl : cfl;

           
            if (FDP2.GetFDRIndex(updated.Callsign) == -1)
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

                string d1 = string.Empty;

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

                if (Math.Abs(updated.PRL - alt) <=  299) //level flight
                    h1 = default;

                else if (alt / 100 > prl || vs > 300) //Issued or trending climb
                    h1 = '↑';

                else if (alt / 100 > 0 && alt < prl || vs < -300) //Issued or trending descent
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
                    double trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong,
                        rte.Last().Intersection.LatLong);
                    bool east = trk >= 0 && trk < 180;
                    eastboundCallsigns.AddOrUpdate(updated.Callsign, east, (c, e) => east);
                }

                TransferOfControl(updated);

                if (jet & rnp4 & cpdlc & adsc)                ///Using CalculateAreaofConflict method
                {
                    ConflictProbe(updated, 23);
                }
                else if (rnp4 || rnp10)
                {
                    ConflictProbe(updated, 50);
                }
                else if (rnp10)
                {
                    ConflictProbe(updated, 75);
                }
                else
                {
                    ConflictProbe(updated, 100);
                }
            }
        }


        private static void TOC(RDP.RadarTrack rt)
        {
            var fdr = rt.CoupledFDR;
            if (fdr != null && rt != null)
            {
                TransferOfControl(fdr);
            }
        }


        private static void TransferOfControl(FDP2.FDR fdr)
        {
            TOC toc;
            toc = new TOC(fdr);
   
            var rt = fdr.CoupledTrack;
            if (fdr != null)
                if (!fdr.ESTed && MMI.IsMySectorConcerned(fdr))
                {
                    MMI.EstFDR(fdr);
                }

            if (MMI.SectorsControlled.ToList()
                .Exists(s => s.IsInSector(fdr.GetLocation(), fdr.PRL)) && !fdr.IsTrackedByMe && !fdr.IsTracked) //MMI.SectorsControlled.Contains(fdr.ControllingSector) || fdr.ControllingSector == null
            {
                MMI.AcceptJurisdiction(fdr);
            }



            foreach (Volume volume in Volumes)
            {
                if (rt != null && (fdr.PRL == -1 || (fdr.PRL <= volume.UpperLevel && fdr.PRL > volume.LowerLevel)) && Conversions.IsLatlonInPoly(fdr.GetLocation(), volume.Boundary))
                {

                    var whichVol = volume;
                    var whichFIR = SectorsVolumes.FindSector(whichVol);
                    if (MMI.GetSectorEntryTime(fdr) == DateTime.MaxValue && MMI.SectorsControlled.ToList().TrueForAll(s => !s.IsInSector(fdr.GetLocation(), fdr.PRL))) // && whichFIR != fdr.ControllingSector 

                    {
                        toc.HandoffNextSector();
                        MMI.HandoffJurisdiction(fdr, whichFIR);
                        MMI.Inhibit(fdr);
                        Thread.Sleep(300000);
                        RDP.DeCouple(rt);
                    }
                }
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
        //        }
        //
        //        network.SendTextMessage(pilot.Callsign, text);
        //        pilot.LastContactMe = DateTime.UtcNow;
        //    }
        //}

        public void ConflictProbe(FDP2.FDR fdr, int value)
        {
            if ((fdr == null) || !MMI.IsMySectorConcerned(fdr)) return;
            int cfl = fdr.CFLUpper;
            int rfl = fdr.RFL;
            int alt = cfl == -1 ? rfl : cfl;
            var isRvsm = fdr.RVSM;

            foreach (var fdr2 in FDP2.GetFDRs)
            {
                if (fdr2 == null || fdr.Callsign == fdr2.Callsign || !MMI.IsMySectorConcerned(fdr2)) continue;
                var rte = fdr.ParsedRoute;
                var rte2 = fdr2.ParsedRoute;
                double trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong,
                    rte.Last().Intersection.LatLong);
                double trk2 = Conversions.CalculateTrack(rte2.First().Intersection.LatLong,
                    rte2.Last().Intersection.LatLong);
                var trkdelta = Math.Abs(trk2 - trk);
                bool sameDir = trkdelta < 45;
                bool crossing = (trkdelta >= 45 && trkdelta <= 135) || (trkdelta >= 315 && trkdelta <= 225);
                bool oppoDir = trkdelta > 135 && trkdelta < 225;
                int cfl2 = fdr2.CFLUpper;
                int rfl2 = fdr2.RFL;
                int alt2 = cfl2 == -1 ? rfl2 : cfl2;
                var isRvsm2 = fdr2.RVSM;
                var delta = Math.Abs(alt - alt2);
                int verticalSep = (alt > 45000 && alt < 60000) ? 4000 : alt > 60000 ? 5000 : (alt2 > 45000 && alt2 < 60000) ? 4000 : alt2 > 60000 ? 5000 :
                                     (alt > FDP2.RVSM_BAND_LOWER && !isRvsm) ||
                                     (alt2 > FDP2.RVSM_BAND_LOWER && !isRvsm2) || (alt > FDP2.RVSM_BAND_UPPER || alt2 > FDP2.RVSM_BAND_UPPER)
                    ? 2000
                    : 1000;

                if (delta < verticalSep)
                {
                    Match pbn2 = Regex.Match(fdr2.Remarks, @"PBN\/\w+\s");
                    bool rnp10 = pbn2.Value.Contains("A1");
                    bool rnp4 = pbn2.Value.Contains("L1");
                    bool cpdlc = (Regex.IsMatch(fdr2.AircraftEquip, @"J5") || Regex.IsMatch(fdr2.AircraftEquip, @"J7"));
                    bool adsc = Regex.IsMatch(fdr2.AircraftSurvEquip, @"D1");
                    bool jet = fdr2.PerformanceData?.IsJet ?? false;

                    var newValue = value;

                    if (rnp4 && adsc && cpdlc)
                    {
                        newValue = Math.Max(newValue, 23);
                    }


                    else if (rnp10 && adsc && cpdlc)
                    {
                        newValue = Math.Max(newValue, 50);
                    }

                    else
                    {
                        newValue = Math.Max(newValue, 100);
                    }
                    var cpar = new CPAR(fdr2, fdr, newValue);
                    var segments1 = cpar.Segments1;
                    var segments2 = cpar.Segments2;

                    segments1.Sort((Comparison<CPAR.Segment>)((s, t) => s.startTime.CompareTo(t.startTime))); //sort by first conflict time
                    segments2.Sort((Comparison<CPAR.Segment>)((s, t) => s.startTime.CompareTo(t.startTime)));

                    var firstConflictTime = segments1.FirstOrDefault();
                    var firstConflictTime2 = segments2.FirstOrDefault();
                    var isConflictPair = segments1.Count > 0;
                    if (firstConflictTime == null || firstConflictTime2 == null) continue;


                    var timeLongSame = sameDir && isConflictPair && firstConflictTime.endTime > DateTime.UtcNow
                        && (firstConflictTime2.startTime - firstConflictTime.startTime).Duration() < (jet ? (new TimeSpan(0, 0, 10, 0)) : (new TimeSpan(0, 0, 15, 0)));//check time based longitudinal for same direction                    
                    var timeLongCross = crossing && isConflictPair && firstConflictTime.endTime > DateTime.UtcNow
                        && (firstConflictTime2.startTime - firstConflictTime.startTime).Duration() < (new TimeSpan(0, 0, 15, 0));
                    var distLongSame = sameDir && isConflictPair && firstConflictTime.endTime > DateTime.UtcNow
                        && Conversions.CalculateDistance(firstConflictTime.startLatlong, firstConflictTime2.startLatlong)
                        < (jet && rnp4 && cpdlc && adsc ? 30 : (rnp4 || rnp10) ? 50 : 50);

                    var timeLongOpposite = false;
                    TimeOfPassing top = null;

                    if (isConflictPair && oppoDir)
                    {
                        try
                        {
                            top = new TimeOfPassing(fdr, fdr2);
                        }

                        catch (Exception e)
                        {
                            return;
                        }
                        timeLongOpposite = top.Time > DateTime.UtcNow
                            && (top.Time.Add(new TimeSpan(0, 0, 10, 0)) > DateTime.UtcNow) && (top.Time.Subtract(new TimeSpan(0, 0, 10, 0)) < DateTime.UtcNow);
                    }




                    //var distLongOpposite = oppoDir && isConflictPair && firstConflictTime.endTime > DateTime.UtcNow 
                    //    && Conversions.CalculateDistance(firstConflictTime.endLatlong, firstConflictTime.endLatlong)
                    //    < (jet && rnp4 && cpdlc && adsc ? 30 : (rnp4 || rnp10) ? 50 : 50);


                    var lossOfSep = timeLongSame || timeLongCross || distLongSame || timeLongOpposite;  // || distLongOpposite 
                    var earliestLOS = (isConflictPair && oppoDir ? top.Time.Subtract(new TimeSpan(0, 0, 10, 0))
                        : (DateTime.Compare(firstConflictTime.startTime, firstConflictTime2.startTime) < 0 ? firstConflictTime.startTime : firstConflictTime2.startTime));
                    var imminentConflicts = (isConflictPair && oppoDir && (delta < verticalSep)) ? (new TimeSpan(0, 0, 30, 0, 0) >= earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        : (lossOfSep && new TimeSpan(0, 0, 30, 0, 0) >= earliestLOS.Subtract(DateTime.UtcNow).Duration()); //check if timediff < 30 mins
                    var advisoryConflicts = (isConflictPair && oppoDir && (delta < verticalSep)) ? (new TimeSpan(0, 2, 0, 0, 0) > earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        && (earliestLOS.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0))
                        : (lossOfSep && new TimeSpan(0, 2, 0, 0, 0) > earliestLOS.Subtract(DateTime.UtcNow).Duration())
                        && (earliestLOS.Subtract(DateTime.UtcNow).Duration() >= new TimeSpan(0, 0, 30, 0, 0));  //check if timediff < 2 hours



                    if (imminentConflicts)
                    {
                        imminentConflict.AddOrUpdate(fdr.Callsign, new HashSet<string>(new string[] { fdr2.Callsign }),
                        (k, v) => { v.Add(fdr2.Callsign); return v; });


                    }
                    else
                    {
                        HashSet<string> maybeSet;
                        bool exists = imminentConflict.TryGetValue(fdr.Callsign, out maybeSet);
                        if (exists)
                        {
                            maybeSet.Clear();
                        }


                        var emptyImminentConflicts = imminentConflict.Where(pair => pair.Value.Count == 0)
                            .Select(pair => pair.Key)
                            .ToList();
                        foreach (var callsign in emptyImminentConflicts)
                        {
                            imminentConflict.TryRemove(callsign, out _);
                        }



                        if (advisoryConflicts)
                        {
                            advisoryConflict.AddOrUpdate(fdr.Callsign, new HashSet<string>(new string[] { fdr2.Callsign }),
                            (k, v) => { v.Add(fdr2.Callsign); return v; });

                        }
                        else
                        {
                            HashSet<string> admaybeSet;
                            bool adexists = advisoryConflict.TryGetValue(fdr.Callsign, out admaybeSet);
                            if (adexists)
                            {
                                admaybeSet.Clear();
                            }

                            var emptyAdvisoryConflicts = advisoryConflict.Where(pair => pair.Value.Count == 0)
                                .Select(pair => pair.Key)
                                .ToList();
                            foreach (var callsign in emptyAdvisoryConflicts)
                            {
                                advisoryConflict.TryRemove(callsign, out _);
                            }

                        }

                    }
                    }
                else
                {
                    HashSet<string> maybeSet; // defines an uninitialized variable for the result of the below method to get placed in
                    HashSet<string> admaybeSet;
                    bool adexists = advisoryConflict.TryGetValue(fdr.Callsign, out admaybeSet);
                    bool exists = imminentConflict.TryGetValue(fdr.Callsign, out maybeSet); // tries to get the set of conflicts corresponding to a callsign, places it in the `maybeSet` variable if it exists. The bool `exists` represents whether there was a set of conflicts for that callsign
                    if (exists)
                    {
                        maybeSet.Remove(fdr2.Callsign); // remove the fdr2 (which is the one we are currently evaluating for conflicts against fdr) from the set of conflicts
                    }
                    if (adexists)
                    {
                        admaybeSet.Remove(fdr2.Callsign); // remove the fdr2 (which is the one we are currently evaluating for conflicts against fdr) from the set of conflicts
                    }

                    var emptyAdvisoryConflicts = advisoryConflict.Where(pair => pair.Value.Count == 0)
                        .Select(pair => pair.Key)
                        .ToList();
                    foreach (var callsign in emptyAdvisoryConflicts)
                    {
                        advisoryConflict.TryRemove(callsign, out _);
                    }
                    var emptyImminentConflicts = imminentConflict.Where(pair => pair.Value.Count == 0)
                        .Select(pair => pair.Key)
                        .ToList();
                    foreach (var callsign in emptyImminentConflicts)
                    {
                        imminentConflict.TryRemove(callsign, out _);
                    }
                }
            }
        }





        ///  Could use the new position of the radar track or its change in state (cancelled, etc.) to do some processing. 
        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {

        }


        private void HandleRadarFlagClick(CustomLabelItemMouseClickEventArgs e)
        {
            bool radarToggled = radartoggle.TryGetValue(e.Track.GetFDR().Callsign, out _);

            if (radarToggled)
            {
                radartoggle.TryRemove(e.Track.GetFDR().Callsign, out _);
            }
            else
            {
                radartoggle.TryAdd(e.Track.GetFDR().Callsign, 0);
            }

            e.Handled = true;
        }


        /// vatSys calls this function when it encounters a custom label item (defined in Labels.xml) during the label rendering.
        /// itemType is the value of the Type attribute in Labels.xml
        /// If it's not our item being called (another plugins, for example), return null.
        /// As a general rule, don't do processing in here as you'll slow down the ASD refresh. In the case of parsing a level to a string though, that's fine.
        public CustomLabelItem GetCustomLabelItem(string itemType, Track track, FDP2.FDR flightDataRecord, RDP.RadarTrack radarTrack)
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

            bool radarToggled = radartoggle.TryGetValue(flightDataRecord.Callsign, out _);
            bool mntflagToggled = mntflagtoggle.TryGetValue(flightDataRecord.Callsign, out _);
            bool downLink = downlink.TryGetValue(flightDataRecord.Callsign, out _);
            bool isNonRVSM = !flightDataRecord.RVSM;
            var pbn = Regex.Match(flightDataRecord.Remarks, @"PBN\/\w+\s");
            bool rnp10 = pbn.Value.Contains("A1");
            bool rnp4 = pbn.Value.Contains("L1");
            int prl = flightDataRecord.PRL / 100;
            int cfl = flightDataRecord.CFLUpper / 100;
            int rfl = flightDataRecord.RFL / 100;
            int alt = flightDataRecord.CFLUpper == -1 ? rfl : cfl;
            var notProbe = flightDataRecord.State == (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED) ? BorderFlags.All : BorderFlags.None;
            var colour = (!flightDataRecord.RVSM ? NonRVSM : ExcludeConflictColor(track, flightDataRecord));

            switch (itemType)

            {

               case LABEL_ITEM_SELECT_HORI:

                    if (MMI.SelectedTrack?.GetFDR()?.Callsign == track.GetFDR().Callsign)
                   {
                       return new CustomLabelItem()
               
                       {
                           Text = " ",
                           Border = BorderFlags.Bottom
                       };
                   }
               
                   return null;
               
               case LABEL_ITEM_SELECT_VERT:
               
                   if (MMI.SelectedTrack?.GetFDR()?.Callsign == track.GetFDR().Callsign)
                   {
                       return new CustomLabelItem()
                       {
                           Text = " ",
                           Border = BorderFlags.Left
                       };
                   }
               
                   return null;

                case LABEL_ITEM_COMM_ICON:

                    if (downLink)
                    {
                        return new CustomLabelItem()
                        {
                            Text = "▽", //▼
                            BackColourIdentity = Colours.Identities.Jurisdiction,
                            //Border = BorderFlags.All
                    };
                    }
                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = "⬜",
                        };
                    }
                case LABEL_ITEM_ADSB_CPDLC:

                    bool useCustomForeColour =
                        flightDataRecord.State == (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED);

                    if (useCustomForeColour)
                    {
                        return new CustomLabelItem()
                        {
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = NotCDA,
                            Text = c1.ToString()
                        };
                    }
                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = c1.ToString(),
                        };
                    }

                case LABEL_ITEM_ADS_FLAGS:

                    return new CustomLabelItem()
                    {
                        Text = c2.ToString(),
                    };


                case LABEL_ITEM_MNT_FLAGS:

                    if (mntflagToggled)
                    {
                        return new CustomLabelItem()
                        {
                            Text = c3.ToString(),
                        };
                    }

                    else
                        return new CustomLabelItem()
                        {
                            Text = "",
                        };


                case LABEL_ITEM_SCC:
                
                    {
                        return new CustomLabelItem()
                        {
                            Text = d1,
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = SpecialConditionCode
                        };
                    }


                case LABEL_ITEM_ANNOT_IND:
                    bool scratch = String.IsNullOrEmpty(flightDataRecord.LabelOpData);

                    if (scratch)
                    {
                        return new CustomLabelItem()
                        {
                            Text = "◦",
                        };
                    }

                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = "&",
                        };
                    }

                case LABEL_ITEM_RESTR:

                    if (flightDataRecord.LabelOpData.Contains("AT ") || flightDataRecord.LabelOpData.Contains(" BY ") ||
                        flightDataRecord.LabelOpData.Contains("CLEARED TO "))

                        return new CustomLabelItem()
                        {
                            Text = "x",
                        };

                    return null;

                case LABEL_ITEM_LEVEL:

                    var level = prl == -1 ? null : (radarTrack.CorrectedAltitude / 100).ToString();

               
                    return new CustomLabelItem()
                        {
                            Text = level.ToString(),
                            Border = notProbe,
                            BorderColourIdentity = Colours.Identities.Custom,
                            CustomBorderColour = NotCDA
                        };                    

                case LABEL_ITEM_VMI:

                    return new CustomLabelItem()
                    {
                        Text = h1.ToString(),
                        ForeColourIdentity = isNonRVSM
                        ? Colours.Identities.Custom
                        : default,
                        CustomForeColour = NonRVSM
                    };



                case LABEL_ITEM_CLEARED_LEVEL:


                    if (radarTrack.ReachedCFL || prl == alt || Math.Abs(flightDataRecord.PRL - alt) <= 299)
                        return new CustomLabelItem()
                        {
                            Text = ""
                        };

                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = alt.ToString(),
                            Border = notProbe,
                            BorderColourIdentity = Colours.Identities.Custom,
                            CustomBorderColour = NotCDA
                        };
                    }

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
                    {
                        return new CustomLabelItem()
                        {
                            Text = "★",
                            OnMouseClick = HandleRadarFlagClick,
                        };
                    }

                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = "◦",
                            OnMouseClick = HandleRadarFlagClick,
                        };
                    }

                case LABEL_ITEM_INHIBIT_IND:


                    if (flightDataRecord.State == FDR.FDRStates.STATE_INHIBITED)
                    {
                        return new CustomLabelItem()
                        {
                            Text = "^",
                        };
                    }

                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = ""
                        };
                    }


                case LABEL_ITEM_FILED_SPEED:
                    var mach = Conversions.CalculateMach(flightDataRecord.TAS, GRIB.FindTemperature(flightDataRecord.PRL, track.GetLocation(), true));
                    return new CustomLabelItem()
                    {
                        Text = "M" + Convert.ToDecimal(mach).ToString("F2").Replace(".", ""),
                        //Text = "N" + flightDataRecord.TAS
                        ForeColourIdentity = Colours.Identities.Custom,
                        CustomForeColour = ExcludeConflictColor(track, flightDataRecord)

                    };

                case LABEL_ITEM_3DIGIT_GROUNDSPEED:
                    //get groundspeed value from either FDR or radarTrack if coupled
                    var gs = radarTrack == null
                        ? flightDataRecord.PredictedPosition.Groundspeed
                        : radarTrack.GroundSpeed;
                    return new CustomLabelItem()
                    {
                        Text = "N" + gs.ToString("000"),//format as 3 digits (with leading zeros)
                        ForeColourIdentity = Colours.Identities.Custom,
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
            if (track.State == MMI.HMIStates.Jurisdiction) //read our dictionary of stored bools (true means is easterly) and return the correct colour
            {

                return GetConflictColour(fdr.Callsign) ?? GetDirectionColour(fdr.Callsign);
            }

            if (fdr.State != (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED)) //only apply conflict colours if planes are of concern
            {
                return GetConflictColour(fdr.Callsign) ?? null;
            }

            else

                return null;

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

        private CustomColour GetConflictColour(string callsign)
        {
            if (imminentConflict.TryGetValue(callsign, out _))
            {
                return Imminent;
            }
            else if (advisoryConflict.TryGetValue(callsign, out _))
            {
                return Advisory;
            }
            
            return null;
        }

        private CustomColour ExcludeConflictColor(Track track, FDP2.FDR flightDataRecord)
        {
            if (SelectASDTrackColour(track) == GetConflictColour(flightDataRecord.Callsign))
            {
                return null;
            }
            return SelectASDTrackColour(track);

        }

    }
}