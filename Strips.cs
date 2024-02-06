﻿using System;
using System.Text.RegularExpressions;
using vatsys;
using vatsys.Plugin;
using System.Collections.Concurrent;
using System.ComponentModel.Composition; //<--Need to add a reference to System.ComponentModel.Composition
using static vatsys.FDP2;
using System.Collections.Generic;
using System.Linq;
using AuroraLabelItemsPlugin;

namespace AuroraStripItemsPlugin
{
    [Export(typeof(IPlugin))]

    public class AuroraStripItemsPlugin : IStripPlugin
    {
        AuroraLabelItemsPlugin.GEO label = new AuroraLabelItemsPlugin.GEO();
        CPAR.ConflictData cpar = new CPAR.ConflictData();
        FPAP fpap = new FPAP();
        /// The name of the custom label item we've added to Labels
        /// in the Profile
        const string LABEL_ITEM_ADSB_CPDLC = "AURORA_ADSB_CPDLC"; //field c(4)
        const string STRIP_ITEM_CALLSIGN = "AURORA_CALLSIGN";
        const string STRIP_ITEM_CTLSECTOR = "AURORA_CDA";
        const string STRIP_ITEM_NXTSECTOR = "AURORA_NDA";
        const string STRIP_ITEM_T10_FLAG = "AURORA_T10_FLAG";
        const string STRIP_ITEM_MNT_FLAG = "AURORA_MNT_FLAG";
        const string STRIP_ITEM_DIST_FLAG = "AURORA_DIST_FLAG";
        const string STRIP_ITEM_RVSM_FLAG = "AURORA_RVSM_FLAG";
        const string STRIP_ITEM_VMI = "AURORA_STRIP_VMI";
        const string STRIP_ITEM_COMPLEX = "AURORA_STRIP_COMPLEX";
        const string STRIP_ITEM_CLEARED_LEVEL = "AURORA_CLEARED_LEVEL";
        const string STRIP_ITEM_REQUESTED_LEVEL = "AURORA_REQUESTED_LEVEL";
        const string STRIP_ITEM_MAN_EST = "AURORA_MAN_EST";
        const string STRIP_ITEM_POINT = "AURORA_POINT";
        const string STRIP_ITEM_ROUTE = "AURORA_ROUTE_STRIP";
        const string STRIP_ITEM_RADAR_IND = "AURORA_RADAR_IND";
        const string STRIP_ITEM_ANNOT_IND = "AURORA_ANNOT_STRIP";
        const string STRIP_ITEM_LATERAL_FLAG = "AURORA_LATERAL_FLAG";
        const string STRIP_ITEM_RESTR = "AURORA_RESTR_STRIP";
        const string STRIP_ITEM_CLRD_RTE = "AURORA_CLRD_RTE";
        const string CPAR_ITEM_TYPE = "CPAR_TYP";
        const string CPAR_ITEM_REQUIRED = "CPAR_REQUIRED";
        const string CPAR_ITEM_ANGLE = "CPAR_ANGLE";
        const string CPAR_ITEM_LOS = "CPAR_LOS";
        const string CPAR_ITEM_ACTUAL = "CPAR_ACTUAL";
        const string CPAR_ITEM_PASSING = "CPAR_PASSING";
        const string CPAR_ITEM_CONF_SEG_START_1 = "CPAR_CONF_SEG_START_1";
        const string CPAR_ITEM_CONF_SEG_START_2 = "CPAR_CONF_SEG_START_2";
        const string CPAR_ITEM_CONF_SEG_END_1 = "CPAR_CONF_SEG_END_1";
        const string CPAR_ITEM_CONF_SEG_END_2 = "CPAR_CONF_SEG_END_2";
        const string CPAR_ITEM_STARTIME_1 = "CPAR_START_TIME_1";
        const string CPAR_ITEM_STARTIME_2 = "CPAR_START_TIME_2";
        const string CPAR_ITEM_ENDTIME_1 = "CPAR_END_TIME_1";
        const string CPAR_ITEM_ENDTIME_2 = "CPAR_END_TIME_2";
        const string CPAR_ITEM_AID_2 = "CPAR_AID_2";
        const string CPAR_ITEM_TYP_2 = "CPAR_TYP_2";
        const string CPAR_ITEM_SPD_2 = "CPAR_SPD_2";
        const string CPAR_ITEM_ALT_2 = "CPAR_ALT_2";
        readonly static CustomColour NonRVSM = new CustomColour(242, 133, 0);
        readonly static CustomColour SepFlags = new CustomColour(0, 196, 253);
        readonly static CustomColour Pending = new CustomColour(46, 139, 87);
        readonly static CustomColour NotCDA = new CustomColour(100, 0, 100);
        readonly ConcurrentDictionary<string, bool> eastboundCallsigns = new ConcurrentDictionary<string, bool>();
        readonly ConcurrentDictionary<string, byte> flagtoggle = new ConcurrentDictionary<string, byte>();

        /// Plugin Name
        public string Name { get => "Aurora Label Items"; }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            if (FDP2.GetFDRIndex(updated.Callsign) == -1) //FDR was removed (that's what triggered the update)
            {
                eastboundCallsigns.TryRemove(updated.Callsign, out _);
                flagtoggle.TryRemove(updated.Callsign, out _);
            }

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

        /// This is called each time a radar track is updated
        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {

        }

        //public void Estimates(FDP2.FDR.ExtractedRoute.Segment estimate)
        //{ 
        //    estimate.IsPETO = true;
        //}

        private void ItemMouseClick(CustomStripItemMouseClickEventArgs e)
        {
            bool flagToggled = flagtoggle.TryGetValue(e.Track.GetFDR().Callsign, out _);

            if (flagToggled)
            {
                flagtoggle.TryRemove(e.Track.GetFDR().Callsign, out _);
            }
            else
            {
                flagtoggle.TryAdd(e.Track.GetFDR().Callsign, 0);
            }
            e.Handled = true;
        }
        public CustomStripItem GetCustomStripItem(string itemType, Track track, FDP2.FDR flightDataRecord, RDP.RadarTrack radarTrack)

        {          
            int level = radarTrack == null ? flightDataRecord.PRL / 100 : radarTrack.CorrectedAltitude / 100;
            bool isEastBound = true;
            eastboundCallsigns.TryGetValue(flightDataRecord.Callsign, out isEastBound);


            if (flightDataRecord == null)
                return null;

            switch (itemType)
            {
                case STRIP_ITEM_CALLSIGN:

                    
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

              case STRIP_ITEM_CTLSECTOR:
             
                    bool pendingCoordination = flightDataRecord.State == (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED);                    
                    string sectorName = flightDataRecord.ControllingSector == null ? "" : flightDataRecord.ControllingSector.Name;

                    {
                        return new CustomStripItem()
                        {
                            ForeColourIdentity = pendingCoordination ? Colours.Identities.Custom : default,
                            CustomForeColour = Pending,
                            Text =  sectorName
                        };
                    }



             
             case STRIP_ITEM_NXTSECTOR:
                   
                    TOC toc;
                    toc = new TOC(flightDataRecord);

                    string firName = toc.nextSector == null ? "" : toc.nextSector.Name;

                    {
                        return new CustomStripItem()
                        {
                            ForeColourIdentity = Colours.Identities.Custom,
                            CustomForeColour = Pending,
                            Text = firName
                        };
                    }



                case LABEL_ITEM_ADSB_CPDLC:


                    if (!isEastBound && !flightDataRecord.ADSB && fpap.cpdlc)

                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.StripBackground,
                            ForeColourIdentity = Colours.Identities.StripText,
                            Text = "⧆"
                        };

                    if (isEastBound && !flightDataRecord.ADSB && fpap.cpdlc)

                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.StripText,
                            ForeColourIdentity = Colours.Identities.StripBackground,
                            Text = "⧆"
                        };

                    if (!isEastBound && !flightDataRecord.ADSB)

                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.StripBackground,
                            ForeColourIdentity = Colours.Identities.StripText,
                            Text = "⎕"
                        };

                    if (isEastBound && !flightDataRecord.ADSB)

                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.StripText,
                            ForeColourIdentity = Colours.Identities.StripBackground,
                            Text = "⎕"
                        };

                    if (!isEastBound && flightDataRecord.ADSB)

                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.StripBackground,
                            ForeColourIdentity = Colours.Identities.StripText,
                            Text = "*"
                        };

                    if (isEastBound && fpap.cpdlc)

                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.StripText,
                            ForeColourIdentity = Colours.Identities.StripBackground,
                            Text = "*"
                        };

                    return null;

                case STRIP_ITEM_T10_FLAG:

                    if (flightDataRecord.PerformanceData?.IsJet ?? false)

                        return new CustomStripItem()
                        {
                            //BackColourIdentity = Colours.Identities.Custom,
                            //CustomBackColour = SepFlags,
                            Text = "M"
                            //OnMouseClick = ItemMouseClick
                        };

                    return null;

                case STRIP_ITEM_MNT_FLAG:

                    if (flightDataRecord.PerformanceData?.IsJet ?? false)

                        return new CustomStripItem()
                        {
                            //BackColourIdentity = Colours.Identities.Custom,
                            //CustomBackColour = SepFlags,
                            Text = "R"
                        };

                    return null;

                case STRIP_ITEM_DIST_FLAG:

                    if (fpap.adsc && fpap.cpdlc && (fpap.rnp4 || fpap.rnp10) && fpap.pbcs)


                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.Custom,
                            CustomBackColour = SepFlags,
                            Text = fpap.rnp4 ? "3" : "D"
                        };
                    return null;

                case STRIP_ITEM_RVSM_FLAG:

                    if (flightDataRecord.RVSM)

                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.Custom,
                            CustomBackColour = SepFlags,
                            Text = "W"
                        };

                    return null;

                case STRIP_ITEM_VMI:
                    var vs = radarTrack == null ? flightDataRecord.PredictedPosition.VerticalSpeed : radarTrack.VerticalSpeed;

                    if (level == fpap.cfl || level == flightDataRecord.RFL)//level

                        return new CustomStripItem()
                        {
                            Text = ""
                        };
                    else if (fpap.cfl > level && track.NewCFL || vs > 300)//Issued or trending climb

                        return new CustomStripItem()
                        {
                            Text = "↑",
                        };

                    else if (fpap.cfl > 0 && fpap.cfl < level && track.NewCFL || vs < -300)//Issued or trending descent

                        return new CustomStripItem()
                        {
                            Text = "↓",
                        };

                    return null;

                case STRIP_ITEM_COMPLEX:

                    if (flightDataRecord.LabelOpData.Contains("AT ") || flightDataRecord.LabelOpData.Contains(" BY ") ||
                        flightDataRecord.LabelOpData.Contains("CLEARED TO "))

                        return new CustomStripItem()
                        {
                            Text = "*"
                        };

                    return null;

                case STRIP_ITEM_CLEARED_LEVEL:

                    if (fpap.cfl == 0)
                    {
                        return new CustomStripItem()
                        {
                            Text = ""
                        };
                    }

                    else
                    {
                        return new CustomStripItem()
                        {
                            Text = fpap.cfl.ToString()
                        };
                    }

                case STRIP_ITEM_REQUESTED_LEVEL:

                    if (flightDataRecord.State == FDR.FDRStates.STATE_INACTIVE || flightDataRecord.State == FDR.FDRStates.STATE_INACTIVE)
                    {
                        return new CustomStripItem()
                        {
                            Text = fpap.rfl.ToString(),
                        };
                    }

                    else
                    {
                        return new CustomStripItem()
                        {
                            Text = ""
                        };
                    }

                //case STRIP_ITEM_MAN_EST:

                //if (Estimates)
                //    return new CustomStripItem()
                //    {
                //        BackColourIdentity = Colours.Identities.HighlightedText
                //    };
                //return null;

                //case STRIP_ITEM_POINT:
                //    Coordinate coordinate = Conversions.ConvertToCoordinate(FDR.ExtractedRoute);
                //
                //    if (StripItemType.Point == )
                //    return new CustomStripItem()
                //    {
                //        Text = Conversions.ConvertToFlightplanLatLong()
                //    };
                case STRIP_ITEM_ROUTE:

                        return new CustomStripItem()
                        {
                            Text = "F",
                            //OnMouseClick = ItemMouseClick
                        };


                case STRIP_ITEM_RADAR_IND:

                        return new CustomStripItem()
                        {
                            Text = "A",
                            //OnMouseClick = 
                        };


                case STRIP_ITEM_ANNOT_IND:

                    bool scratch = String.IsNullOrEmpty(flightDataRecord.LabelOpData);

                    if (scratch)
                    {
                        return new CustomStripItem()
                        {
                            Text = "."
                        };
                    }

                    else
                    {
                        return new CustomStripItem()
                        {
                            Text = "&"
                        };
                    }


                case STRIP_ITEM_LATERAL_FLAG:

                    if (fpap.adsc && fpap.cpdlc && (fpap.rnp4 || fpap.rnp10) && fpap.pbcs)


                        return new CustomStripItem()
                        {
                            BackColourIdentity = Colours.Identities.Custom,
                            CustomBackColour = SepFlags,
                            Text = fpap.rnp4 ? "4" : fpap.rnp10 ? "R" : "", 
                            //OnMouseClick = ItemMouseClick
                        };
                    return null;

                case STRIP_ITEM_RESTR:

                    if (flightDataRecord.LabelOpData.Contains("AT ") || flightDataRecord.LabelOpData.Contains(" BY ") ||
                        flightDataRecord.LabelOpData.Contains("CLEARED TO "))

                        return new CustomStripItem()
                        {
                            Text = "x"
                        };

                    return null;

                case STRIP_ITEM_CLRD_RTE:

                        return new CustomStripItem()
                        {
                            Text = "Cleared Route:" + flightDataRecord.RouteNoParse
                        };

                    //if (label.imminentConflict.TryGetValue(flightDataRecord.Callsign, out _) || (label.advisoryConflict.TryGetValue(flightDataRecord.Callsign, out)) == flightDataRecord.Callsign;

                case CPAR_ITEM_TYPE:

                        return new CustomStripItem()
                        {
                            Text = cpar.conflictType
                        };

                case CPAR_ITEM_REQUIRED:

                    if(cpar.longType == cpar.timeLongsame)

                        return new CustomStripItem()
                        {
                            Text = "REQUIRED " + cpar.longTimesep + " ( " + cpar.latSep + ") " + cpar.verticalSep + " ft"
                        };

                    else
                        return new CustomStripItem()
                        {
                            Text = "REQUIRED " + cpar.longDistsep + " ( " + cpar.latSep + ") " + cpar.verticalSep + " ft"
                        };

                case CPAR_ITEM_ANGLE:

                        return new CustomStripItem()
                        {
                            Text = cpar.trkAngle.ToString() + " degrees"
                        };

                case CPAR_ITEM_LOS:

                        return new CustomStripItem()
                        {
                            Text = cpar.earliestLOS.ToString()
                        };

                case CPAR_ITEM_ACTUAL:

                    if (cpar.longType == cpar.timeLongsame)

                        return new CustomStripItem()
                        {
                            Text = " ACTUAL " + cpar.longTimeact + " ( " + cpar.latAct + ") " + cpar.verticalAct + " ft"
                        };

                    else
                        return new CustomStripItem()
                        {
                            Text = " ACTUAL " + cpar.longDistact + " ( " + cpar.latAct +") " + cpar.verticalAct + " ft"
                        };

                case CPAR_ITEM_PASSING:

                        return new CustomStripItem()
                        {
                            Text = cpar.top.Time.ToString()
                        };

                case CPAR_ITEM_CONF_SEG_START_1:

                        return new CustomStripItem()
                        {
                            Text = cpar.startLatlong.ToString()
                        };

                case CPAR_ITEM_CONF_SEG_START_2:

                        return new CustomStripItem()
                        {
                            Text = cpar.startLatlong.ToString()
                        };

                case CPAR_ITEM_CONF_SEG_END_1:

                        return new CustomStripItem()
                        {
                            Text = cpar.endLatlong.ToString()
                        };

                case CPAR_ITEM_CONF_SEG_END_2:

                        return new CustomStripItem()
                        {
                            Text = cpar.endLatlong.ToString()
                        };

                case CPAR_ITEM_STARTIME_1:

                        return new CustomStripItem()
                        {
                            Text = cpar.startTime.ToString()
                        };

                case CPAR_ITEM_STARTIME_2:

                        return new CustomStripItem()
                        {
                            Text = cpar.startTime.ToString()
                        };

                case CPAR_ITEM_ENDTIME_1:

                        return new CustomStripItem()
                        {
                            Text = cpar.endTime.ToString()
                        };

                case CPAR_ITEM_ENDTIME_2:

                        return new CustomStripItem()
                        {
                            Text = cpar.endTime.ToString()
                        };

                case CPAR_ITEM_AID_2:

                        return new CustomStripItem()
                        {
                            Text = cpar.fdr2.Callsign
                        };

                case CPAR_ITEM_TYP_2:

                        return new CustomStripItem()
                        {
                            Text = cpar.fdr2.AircraftType
                        };

                case CPAR_ITEM_SPD_2:

                        return new CustomStripItem()
                        {
                            Text = cpar.fdr2.TAS.ToString()
                        };

                case CPAR_ITEM_ALT_2:

                        return new CustomStripItem()
                        {
                            Text = cpar.fdr2.PredictedPosition.Altitude.ToString()
                        };

                default: return null;
            }
        }
       //private CustomColour PETOColor(FDP2.FDR.ExtractedRoute.Segment estimate, Colours.Identities state)
       //{
       //    if (estimate.IsPETO)
       //    {
       //        return state = state;
       //    }
       //
       //    return null;
       //}
    }
}
