using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq; //<--Need to add a reference to System.ComponentModel.Composition
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using vatsys;
using vatsys.Plugin;
using static vatsys.FDP2;


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
        const string LABEL_ITEM_SCC = "AURORA_SCC"; //field c(5)
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
        readonly static CustomColour Advisory = new CustomColour(255, 0, 0);
        readonly static CustomColour Imminent = new CustomColour(255, 165, 0);
        readonly ConcurrentDictionary<string, bool> eastboundCallsigns = new ConcurrentDictionary<string, bool>();
        readonly ConcurrentDictionary<string, char> adsbcpdlcValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, char> adsflagValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, char> mntflagValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, char> altValues = new ConcurrentDictionary<string, char>();
        readonly ConcurrentDictionary<string, byte> radartoggle = new ConcurrentDictionary<string, byte>();
        readonly ConcurrentDictionary<string, byte> mntflagtoggle = new ConcurrentDictionary<string, byte>();
        readonly ConcurrentDictionary<string, byte> downlink = new ConcurrentDictionary<string, byte>();
        // key: callsign, value: acknowledged status
        readonly ConcurrentDictionary<string, bool> advisoryConflict = new ConcurrentDictionary<string, bool>();
        readonly ConcurrentDictionary<string, bool> imminentConflict = new ConcurrentDictionary<string, bool>();

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
            AutoAssume(updated);
            AutoDrop(updated);
            ProbeStandardConflict(updated, 23);
            if (FDP2.GetFDRIndex(updated.Callsign) == -1)
            {
                eastboundCallsigns.TryRemove(updated.Callsign, out _);
                adsbcpdlcValues.TryRemove(updated.Callsign, out _);
                adsflagValues.TryRemove(updated.Callsign, out _);
                mntflagValues.TryRemove(updated.Callsign, out _);
                altValues.TryRemove(updated.Callsign, out _);
                radartoggle.TryRemove(updated.Callsign, out _);
                mntflagtoggle.TryRemove(updated.Callsign, out _);
                downlink.TryRemove(updated.Callsign, out _);
                advisoryConflict.TryRemove(updated.Callsign, out _);
                imminentConflict.TryRemove(updated.Callsign, out _);

            }
            else
            {
                Match pbn = Regex.Match(updated.Remarks, @"PBN\/\w+\s");
                bool rnp10 = Regex.IsMatch(pbn.Value, @"A1");
                bool rnp4 = Regex.IsMatch(pbn.Value, @"L1");
                bool cpdlc = Regex.IsMatch(updated.AircraftEquip, @"J5") || Regex.IsMatch(updated.AircraftEquip, @"J7");
                bool adsc = Regex.IsMatch(updated.AircraftSurvEquip, @"D1");
                int cfl;
                bool isCfl = Int32.TryParse(updated.CFLString, out cfl);
                var vs = updated.PredictedPosition.VerticalSpeed;
                int level = updated.PRL / 100;

                char c1 = default;

                if (!updated.ADSB && cpdlc)

                    c1 = '⧆';

                else if (!updated.ADSB)

                    c1 = '⎕';

                else if (cpdlc)

                    c1 = '*';


                adsbcpdlcValues.AddOrUpdate(updated.Callsign, c1, (k, v) => c1);

                char c2 = default;

                if (adsc & cpdlc & rnp4)
                    c2 = '3';

                else if (adsc & cpdlc & rnp10)
                    c2 = 'D';

                adsflagValues.AddOrUpdate(updated.Callsign, c2, (k, v) => c2);

                char c3 = default;

                if (AuroraStripItemsPlugin.AuroraStripItemsPlugin.MachNumberTech(updated.PerformanceData))
                    c3 = 'M';

                mntflagValues.AddOrUpdate(updated.Callsign, c3, (k, v) => c3);


                char h1 = default;

                if (level == cfl || level == updated.RFL) //level
                    h1 = default;

                else if (cfl > level || vs > 300) //Issued or trending climb
                    h1 = '↑';

                else if (cfl > 0 && cfl < level || vs < -300) //Issued or trending descent
                    h1 = '↓';

                else if (level - updated.RFL / 100 >= 3) //deviating above
                    h1 = '+';

                else if (level - updated.RFL / 100 <= -3) //deviating below
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
            }
        }

        public class Segment
        {
            public string callsign;
            public Coordinate startLatlong;
            public Coordinate endLatlong;
            public DateTime startTime = DateTime.MaxValue;
            public DateTime endTime = DateTime.MaxValue;
            public FDP2.FDR.ExtractedRoute.Segment routeSegment;
        }

        private static List<Coordinate> CreatePolygon(Coordinate point1, Coordinate point2, int value)
        {
            List<Coordinate> polygon = new List<Coordinate>();
            double track = Conversions.CalculateTrack(point1, point2);
            double num1 = track - 90.0;
            for (int index = 0; index <= 180; index += 10)
            {
                double heading = num1 - (double)index;
                Coordinate fromBearingRange = Conversions.CalculateLLFromBearingRange(point1, (double)value, heading);
                polygon.Add(fromBearingRange);
            }
            double num2 = track + 90.0;
            for (int index = 0; index <= 180; index += 10)
            {
                double heading = num2 - (double)index;
                Coordinate fromBearingRange = Conversions.CalculateLLFromBearingRange(point2, (double)value, heading);
                polygon.Add(fromBearingRange);
            }
            polygon.Add(polygon[0]);
            return polygon;
        }

        private static List<Coordinate> CalculatePolygonIntersections(
            List<Coordinate> polygon,
            Coordinate point1,
            Coordinate point2)
        {
            List<Coordinate> polygonIntersections = new List<Coordinate>();
            for (int index = 1; index < polygon.Count; ++index)
            {
                List<Coordinate> gcIntersectionLl = Conversions.CalculateAllGCIntersectionLL(polygon[index - 1], polygon[index], point1, point2);
                if (gcIntersectionLl != null)
                    polygonIntersections.AddRange(gcIntersectionLl);
            }
            for (int index = 0; index < polygonIntersections.Count; ++index)
            {
                Coordinate intsect = polygonIntersections[index];
                polygonIntersections.RemoveAll(c => c != intsect && Conversions.CalculateDistance(intsect, c) < 0.01);
            }
            return polygonIntersections;
        }

        private static List<Segment> CalculateAreaOfConflict(FDP2.FDR fdr1, FDP2.FDR fdr2, int value)
        {
            List<Segment> segs = new List<Segment>();
            List<FDP2.FDR.ExtractedRoute.Segment> route1waypoints = fdr1.ParsedRoute.ToList().Where(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT).ToList();
            List<FDP2.FDR.ExtractedRoute.Segment> route2waypoints = fdr2.ParsedRoute.ToList().Where(s => s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT).ToList();
            for (int wp1index = 1; wp1index < route1waypoints.Count; ++wp1index)
            {
                List<Coordinate> route1Segment = CreatePolygon(route1waypoints[wp1index - 1].Intersection.LatLong, route1waypoints[wp1index].Intersection.LatLong, value);
                for (int wp2index = 1; wp2index < route2waypoints.Count; wp2index++)
                {
                    List<Coordinate> source = new List<Coordinate>();
                    List<Coordinate> intersectionPoints = new List<Coordinate>();
                    source.AddRange((IEnumerable<Coordinate>)CalculatePolygonIntersections(route1Segment, route2waypoints[wp2index - 1].Intersection.LatLong, route2waypoints[wp2index].Intersection.LatLong));
                    int num1 = 0;
                    int num2 = 0;
                    foreach (Coordinate coordinate in source.ToList<Coordinate>())
                    {
                        if (Conversions.IsLatLonOnGC(route2waypoints[wp2index - 1].Intersection.LatLong, route2waypoints[wp2index].Intersection.LatLong, coordinate))
                        {
                            intersectionPoints.Add(coordinate);
                        }
                        else
                        {
                            double track = Conversions.CalculateTrack(route2waypoints[wp2index - 1].Intersection.LatLong, route2waypoints[wp2index].Intersection.LatLong);
                            if (Math.Abs(track - Conversions.CalculateTrack(route2waypoints[wp2index - 1].Intersection.LatLong, coordinate)) > 90.0)
                                ++num1;
                            if (Math.Abs(track - Conversions.CalculateTrack(coordinate, route2waypoints[wp2index].Intersection.LatLong)) > 90.0)
                                ++num2;
                        }
                    }
                    if (num1 % 2 != 0 && num2 % 2 != 0)
                    {
                        intersectionPoints.Clear();
                        intersectionPoints.Add(route2waypoints[wp2index - 1].Intersection.LatLong);
                        intersectionPoints.Add(route2waypoints[wp2index].Intersection.LatLong);
                    }
                    else if (num2 % 2 != 0)
                        intersectionPoints.Add(route2waypoints[wp2index].Intersection.LatLong);
                    else if (num1 % 2 != 0)
                        intersectionPoints.Add(route2waypoints[wp2index - 1].Intersection.LatLong);
                    intersectionPoints.Sort((x, y) => Conversions.CalculateDistance(route2waypoints[wp2index - 1].Intersection.LatLong, x).CompareTo(Conversions.CalculateDistance(route2waypoints[wp2index - 1].Intersection.LatLong, y)));
                    for (int ipIndex = 1; ipIndex < intersectionPoints.Count; ipIndex += 2)
                    {
                        Segment seg = new Segment();
                        seg.startLatlong = intersectionPoints[ipIndex - 1];
                        seg.endLatlong = intersectionPoints[ipIndex];
                        List<Segment> conflictSegments = segs.Where<Segment>((Func<Segment, bool>)(s => s.routeSegment == route2waypoints[wp2index])).Where<Segment>((Func<Segment, bool>)(s => Conversions.CalculateDistance(s.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(seg.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) && Conversions.CalculateDistance(s.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) > Conversions.CalculateDistance(seg.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) || Conversions.CalculateDistance(s.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) > Conversions.CalculateDistance(seg.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) && Conversions.CalculateDistance(s.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(seg.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) || Conversions.CalculateDistance(s.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) > Conversions.CalculateDistance(seg.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) && Conversions.CalculateDistance(s.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(seg.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) || Conversions.CalculateDistance(s.startLatlong, seg.startLatlong) < 0.01 || Conversions.CalculateDistance(s.endLatlong, seg.endLatlong) < 0.01)).ToList<Segment>();
                        if (conflictSegments.Count > 0)
                        {
                            foreach (Segment segment in conflictSegments)
                            {
                                if (Conversions.CalculateDistance(segment.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(seg.endLatlong, route2waypoints[wp2index - 1].Intersection.LatLong))
                                    segment.endLatlong = seg.endLatlong;
                                if (Conversions.CalculateDistance(seg.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong) < Conversions.CalculateDistance(segment.startLatlong, route2waypoints[wp2index - 1].Intersection.LatLong))
                                    segment.startLatlong = seg.startLatlong;
                            }
                        }
                        else
                        {
                            seg.callsign = fdr2.Callsign;
                            seg.routeSegment = route2waypoints[wp2index];
                            segs.Add(seg);
                        }
                    }
                }
            }
            for (int i = 0; i < segs.Count; i++)
            {
                if (!segs.Exists((Predicate<Segment>)(s => Conversions.CalculateDistance(segs[i].startLatlong, s.endLatlong) < 0.01)))
                    segs[i].startTime = FDP2.GetSystemEstimateAtPosition(fdr2, segs[i].startLatlong, segs[i].routeSegment);
                if (!segs.Exists((Predicate<Segment>)(s => Conversions.CalculateDistance(segs[i].endLatlong, s.startLatlong) < 0.01)))
                    segs[i].endTime = FDP2.GetSystemEstimateAtPosition(fdr2, segs[i].endLatlong, segs[i].routeSegment);
            }
            return segs;
        }

        private void ProbeStandardConflict(FDP2.FDR fdr, int value)
        {
            if (fdr == null) return;
            int cfl;
            bool isCfl = Int32.TryParse(fdr.CFLString, out cfl);
            if (!isCfl) return;

            var isRvsm = fdr.RVSM;

            bool never;

            foreach (var fdr2 in FDP2.GetFDRs)
            {
                if (fdr2 == null || fdr.Callsign == fdr2.Callsign || !MMI.IsMySectorConcerned(fdr2)) continue;
                int cfl2;
                bool isCfl2 = Int32.TryParse(fdr.CFLString, out cfl2);
                if (!isCfl2) continue;
                var isRvsm2 = fdr2.RVSM;
                var delta = Math.Abs(cfl - cfl2);
                int requiredAltSep = (cfl > FDP2.RVSM_BAND_LOWER && !isRvsm) ||
                                     (cfl2 > FDP2.RVSM_BAND_LOWER && !isRvsm2) || cfl > FDP2.RVSM_BAND_UPPER ||
                                     cfl2 > FDP2.RVSM_BAND_UPPER
                    ? 2000
                    : 1000;

                if (delta < requiredAltSep)
                {
                    var segments1 = CalculateAreaOfConflict(fdr, fdr2, value);
                    // var segments2 = CalculateAreaOfConflict(fdr2, fdr, value);
                    segments1.Sort((Comparison<Segment>)((s, t) => s.startTime.CompareTo(t.startTime)));
                    var firstConflictTime = segments1.FirstOrDefault();                    
                    var timeDiff = firstConflictTime != null
                        ? new DateTime().Subtract(firstConflictTime.startTime)
                        : TimeSpan.MaxValue;
                    var advisoryConflicts = timeDiff.CompareTo(new TimeSpan(0, 2, 0, 0, 0)) < 31;
                    var imminentConflicts = timeDiff.CompareTo(new TimeSpan(0, 0, 30, 0, 0)) < 0;
                    if (advisoryConflicts)                        
                    {
                        // auto acknowledge
                        advisoryConflict.AddOrUpdate(fdr.Callsign, true, (k, v) => true);                        
                    }
                    if (imminentConflicts)
                    {
                        imminentConflict.AddOrUpdate(fdr.Callsign, true, (k, v) => true);
                    }
                    else
                    {
                        advisoryConflict.TryRemove(fdr.Callsign, out never);
                        imminentConflict.TryRemove(fdr.Callsign, out never);
                    }
                }
                else
                {
                    advisoryConflict.TryRemove(fdr.Callsign, out never);
                    imminentConflict.TryRemove(fdr.Callsign, out never);
                }
            }
        }

        private static void AutoAssume(RDP.RadarTrack rt)
        {
            var fdr = rt.CoupledFDR;
            if (fdr != null)
            {
                AutoAssume(fdr);
                RDP.Couple(fdr, rt);
            }
        }

        private static void AutoAssume(FDP2.FDR fdr)
        {
            if (fdr != null)
            {
                if (!fdr.ESTed && fdr.ControllingSector == null && MMI.IsMySectorConcerned(fdr))
                {
                    MMI.EstFDR(fdr);                    
                }

                if (MMI.SectorsControlled.ToList()
                        .Exists(s => s.IsInSector(fdr.GetLocation(), fdr.PRL)) && !fdr.IsTrackedByMe &&
                    MMI.SectorsControlled.Contains(fdr.ControllingSector) || fdr.ControllingSector == null)
                {
                    MMI.AcceptJurisdiction(fdr);
                }
            }
        }

        private static void AutoDrop(RDP.RadarTrack rt)
        {
            var fdr = rt.CoupledFDR;
            if (fdr != null)
            {
                AutoDrop(fdr);
            }
        }

        private static void AutoDrop(FDP2.FDR fdr)
        {
            var rt = fdr.CoupledTrack;
            if (fdr != null)
            {
                if (fdr.IsTrackedByMe && MMI.SectorsControlled.ToList()
                        .TrueForAll(s => !s.IsInSector(fdr.GetLocation(), fdr.PRL)))
                {
                    MMI.HandoffToNone(fdr);
                    //Thread.Sleep(300000);                    
                    //RDP.DeCouple(rt);
                }
            }
        }


        ///  Could use the new position of the radar track or its change in state (cancelled, etc.) to do some processing. 
        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            AutoAssume(updated);
            AutoDrop(updated);
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

       //private void HandleADSFlagClick(CustomLabelItemMouseClickEventArgs e)
       //{
       //    bool adsflagToggled = radartoggle.TryGetValue(e.Track.GetFDR().Callsign, out _);
       //
       //    if (adsflagToggled)
       //    {
       //        adsflagtoggle.TryRemove(e.Track.GetFDR().Callsign, out _);
       //    }
       //    else
       //    {
       //        adsflagtoggle.TryAdd(e.Track.GetFDR().Callsign, 0);
       //    }
       //
       //    e.Handled = true;
       //}
       //
       //private void HandleMNTFlagClick(CustomLabelItemMouseClickEventArgs e)
       //{
       //    bool mntflagToggled = radartoggle.TryGetValue(e.Track.GetFDR().Callsign, out _);
       //
       //    if (mntflagToggled)
       //    {
       //        mntflagtoggle.TryRemove(e.Track.GetFDR().Callsign, out _);
       //    }
       //    else
       //    {
       //        mntflagtoggle.TryAdd(e.Track.GetFDR().Callsign, 0);
       //    }
       //
       //    e.Handled = true;
       //}

        /// vatSys calls this function when it encounters a custom label item (defined in Labels.xml) during the label rendering.
        /// itemType is the value of the Type attribute in Labels.xml
        /// If it's not our item being called (another plugins, for example), return null.
        /// As a general rule, don't do processing in here as you'll slow down the ASD refresh. In the case of parsing a level to a string though, that's fine.
        public CustomLabelItem GetCustomLabelItem(string itemType, Track track, FDP2.FDR flightDataRecord,
            RDP.RadarTrack radarTrack)
        {
            if (flightDataRecord == null || track == null)
                return null;

            char c1;
            adsbcpdlcValues.TryGetValue(flightDataRecord.Callsign, out c1);
            char c2;
            adsflagValues.TryGetValue(flightDataRecord.Callsign, out c2);
            char c3;
            mntflagValues.TryGetValue(flightDataRecord.Callsign, out c3);
            char h1;
            altValues.TryGetValue(flightDataRecord.Callsign, out h1);

            bool radarToggled = radartoggle.TryGetValue(flightDataRecord.Callsign, out _);
            bool mntflagToggled = mntflagtoggle.TryGetValue(flightDataRecord.Callsign, out _);
            bool downLink = downlink.TryGetValue(flightDataRecord.Callsign, out _);
            bool selectedCallsign = MMI.SelectedTrack?.GetFDR()?.Callsign == flightDataRecord.Callsign;
            bool isAdvisory = advisoryConflict.TryGetValue(flightDataRecord.Callsign, out _);
            bool isImminent = imminentConflict.TryGetValue(flightDataRecord.Callsign, out _);

            switch (itemType)
            {
                case LABEL_ITEM_SELECT_HORI:

                    if (selectedCallsign)// || isAdvisory || isImminent)
                    {
                        return new CustomLabelItem()

                        {
                            Border = BorderFlags.Top,
                            //CustomBorderColour = isAdvisory ? Advisory : null
                        };
                    }

                    else
                    {
                        return new CustomLabelItem()

                        {
                            Border = BorderFlags.None,

                        };
                    }

                case LABEL_ITEM_SELECT_VERT:

                    if (selectedCallsign)// || isAdvisory || isImminent)
                    {
                        return new CustomLabelItem()
                        {
                            Text = "",
                            Border = BorderFlags.Left,
                            //CustomBorderColour = isAdvisory ? Advisory : null
                        };
                    }

                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = "",
                            Border = BorderFlags.None,
                        };
                    }

                case LABEL_ITEM_COMM_ICON:

                    if (downLink)
                    {
                        return new CustomLabelItem()
                        {
                            Text = "▼",
                            Border = BorderFlags.All
                        };
                    }
                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = "⬜"
                        };
                    }
                case LABEL_ITEM_ADSB_CPDLC:

                    bool useCustomForeColour =
                        track.State == MMI.HMIStates.Preactive || track.State == MMI.HMIStates.Announced;

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
                            Text = c1.ToString()
                        };
                    }

                case LABEL_ITEM_ADS_FLAGS:

                        return new CustomLabelItem()
                        {
                            Text = c2.ToString()
                        };


                case LABEL_ITEM_MNT_FLAGS:

                    if (mntflagToggled)
                    {
                        return new CustomLabelItem()
                        {
                            Text = c3.ToString()
                        };
                    }

                    else
                        return new CustomLabelItem()
                        {
                            Text = "",
                        };


                //case LABEL_ITEM_SCC:
                //
                //        return new CustomLabelItem()
                //        {
                //            Text = pos //| ca | la | ra | rcf | dup | spd 
                //        };                


                case LABEL_ITEM_ANNOT_IND:
                    bool scratch = String.IsNullOrEmpty(flightDataRecord.LabelOpData);

                    if (scratch)
                    {
                        return new CustomLabelItem()
                        {
                            Text = "◦"
                        };
                    }

                    else
                    {
                        return new CustomLabelItem()
                        {
                            Text = "&"
                        };
                    }

                case LABEL_ITEM_RESTR:

                    if (flightDataRecord.LabelOpData.Contains("AT") || flightDataRecord.LabelOpData.Contains("BY") ||
                        flightDataRecord.LabelOpData.Contains("CLEARED TO"))

                        return new CustomLabelItem()
                        {
                            Text = "x"
                        };

                    return null;

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
                        Text = (track.NewCFL == radarTrack.ReachedCFL) ? string.Empty : flightDataRecord.CFLString,
                        ForeColourIdentity = Colours.Identities.Custom,
                        CustomForeColour = !flightDataRecord.RVSM ? NonRVSM : Probe
                    };

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

                case LABEL_ITEM_FILED_SPEED:
                    var mach = flightDataRecord.TAS / 581.0;
                    return new CustomLabelItem()
                    {
                        Text = "M" + Convert.ToDecimal(mach).ToString("F2").Replace(".", "")
                        //Text = "N" + flightDataRecord.TAS
                    };

                case LABEL_ITEM_3DIGIT_GROUNDSPEED:
                    //get groundspeed value from either FDR or radarTrack if coupled
                    var gs = radarTrack == null
                        ? flightDataRecord.PredictedPosition.Groundspeed
                        : radarTrack.GroundSpeed;
                    return new CustomLabelItem()
                    {
                        Text = "N" + gs.ToString("000") //format as 3 digits (with leading zeros)
                    };

                default:
                    return null;
            }
        }

        public CustomColour SelectASDTrackColour(Track track)
        {
            var fdr = track.GetFDR();
            //if track doesn't have an FDR coupled do nothing
            if (fdr == null)
                return null;



            //only apply East/West colour to jurisdiction state
            if (track.State == MMI.HMIStates.Jurisdiction)

             //read our dictionary of stored bools (true means is easterly) and return the correct colour
                return GetDirectionColour(fdr.Callsign);

            if (MMI.IsMySectorConcerned(fdr))
                return GetConflictColour(fdr.Callsign);

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
            if (advisoryConflict.TryGetValue(callsign, out _))
            {
                return Advisory;
            }

            if (imminentConflict.TryGetValue(callsign, out _))
            {
                return Imminent;
            }

                return null; 
        }
    }
}