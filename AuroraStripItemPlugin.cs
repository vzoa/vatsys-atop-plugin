//<--Need to add a reference to System.ComponentModel.Composition
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using AuroraLabelItemsPlugin;
using vatsys;
using vatsys.Plugin;
using static vatsys.FDP2;

namespace AuroraStripItemsPlugin
{
    [Export(typeof(IPlugin))]
    public class AuroraStripItemsPlugin : IStripPlugin
    {

        private readonly ConcurrentDictionary<string, bool> eastboundCallsigns =
            new ConcurrentDictionary<string, bool>();

        private readonly ConcurrentDictionary<string, byte> flagtoggle = new ConcurrentDictionary<string, byte>();
        private CPAR cpar = new CPAR();

        private AuroraLabelItemsPlugin.AuroraLabelItemsPlugin label =
            new AuroraLabelItemsPlugin.AuroraLabelItemsPlugin();

        /// Plugin Name
        public string Name => "Aurora Label Items";

        public void OnFDRUpdate(FDR updated)
        {
            if (GetFDRIndex(updated.Callsign) == -1) //FDR was removed (that's what triggered the update)
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
                    var trk = Conversions.CalculateTrack(rte.First().Intersection.LatLong,
                        rte.Last().Intersection.LatLong);
                    var east = trk >= 0 && trk < 180;
                    eastboundCallsigns.AddOrUpdate(updated.Callsign, east, (c, e) => east);
                }
            }
        }

        /// This is called each time a radar track is updated
        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
        }

        public CustomStripItem GetCustomStripItem(string itemType, Track track, FDR flightDataRecord,
            RDP.RadarTrack radarTrack)

        {
            var pbn = Regex.Match(flightDataRecord.Remarks, @"PBN\/\w+\s");
            var rnp10 = pbn.Value.Contains("A1");
            var rnp4 = pbn.Value.Contains("L1");
            var cpdlc = flightDataRecord.AircraftEquip.Contains("J5") || flightDataRecord.AircraftEquip.Contains("J7");
            var adsc = flightDataRecord.AircraftSurvEquip.Contains("D1");
            var adsb = flightDataRecord.ADSB;
            var rvsm = flightDataRecord.RVSM;
            var level = radarTrack == null ? flightDataRecord.PRL / 100 : radarTrack.CorrectedAltitude / 100;
            var isEastBound = true;
            eastboundCallsigns.TryGetValue(flightDataRecord.Callsign, out isEastBound);
            var prl = flightDataRecord.PRL / 100;
            var cfl = flightDataRecord.CFLUpper / 100;
            var rfl = flightDataRecord.RFL / 100;
            var alt = flightDataRecord.CFLUpper == -1 ? rfl : cfl;

            if (flightDataRecord == null)
                return null;

            switch (itemType)
            {
                case StripConstants.STRIP_ITEM_CALLSIGN:


                    if (isEastBound)
                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.StripText,
                            ForeColourIdentity = Colours.Identities.StripBackground,
                            Text = flightDataRecord.Callsign
                        };
                    return new CustomStripItem
                    {
                        BackColourIdentity = Colours.Identities.StripBackground,
                        ForeColourIdentity = Colours.Identities.StripText,
                        Text = flightDataRecord.Callsign
                    };

                case StripConstants.STRIP_ITEM_CTLSECTOR:

                    var pendingCoordination = flightDataRecord.State ==
                                              (FDR.FDRStates.STATE_PREACTIVE | FDR.FDRStates.STATE_COORDINATED);
                    var sectorName = flightDataRecord.ControllingSector == null
                        ? ""
                        : flightDataRecord.ControllingSector.Name;

                {
                    return new CustomStripItem
                    {
                        ForeColourIdentity = pendingCoordination ? Colours.Identities.Custom : default,
                        CustomForeColour = CustomColors.Pending,
                        Text = sectorName
                    };
                }


                case StripConstants.STRIP_ITEM_NXTSECTOR:

                    TOC toc;
                    toc = new TOC(flightDataRecord);

                    var firName = toc.nextSector == null ? "" : toc.nextSector.Name;

                {
                    return new CustomStripItem
                    {
                        ForeColourIdentity = Colours.Identities.Custom,
                        CustomForeColour = CustomColors.Pending,
                        Text = firName
                    };
                }


                case StripConstants.LABEL_ITEM_ADSB_CPDLC:


                    if (!isEastBound && !adsb && cpdlc)

                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.StripBackground,
                            ForeColourIdentity = Colours.Identities.StripText,
                            Text = "⧆"
                        };

                    if (isEastBound && !adsb && cpdlc)

                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.StripText,
                            ForeColourIdentity = Colours.Identities.StripBackground,
                            Text = "⧆"
                        };

                    if (!isEastBound && !adsb)

                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.StripBackground,
                            ForeColourIdentity = Colours.Identities.StripText,
                            Text = "⎕"
                        };

                    if (isEastBound && !adsb)

                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.StripText,
                            ForeColourIdentity = Colours.Identities.StripBackground,
                            Text = "⎕"
                        };

                    if (!isEastBound && cpdlc)

                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.StripBackground,
                            ForeColourIdentity = Colours.Identities.StripText,
                            Text = "*"
                        };

                    if (isEastBound && cpdlc)

                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.StripText,
                            ForeColourIdentity = Colours.Identities.StripBackground,
                            Text = "*"
                        };

                    return null;

                case StripConstants.STRIP_ITEM_T10_FLAG:

                    if (flightDataRecord.PerformanceData?.IsJet ?? false)

                        return new CustomStripItem
                        {
                            //BackColourIdentity = Colours.Identities.Custom,
                            //CustomBackColour = SepFlags,
                            Text = "M"
                            //OnMouseClick = ItemMouseClick
                        };

                    return null;

                case StripConstants.STRIP_ITEM_MNT_FLAG:

                    if (flightDataRecord.PerformanceData?.IsJet ?? false)

                        return new CustomStripItem
                        {
                            //BackColourIdentity = Colours.Identities.Custom,
                            //CustomBackColour = SepFlags,
                            Text = "R"
                        };

                    return null;

                case StripConstants.STRIP_ITEM_DIST_FLAG:

                    if (adsc & cpdlc & (rnp4 || rnp10))


                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.Custom,
                            CustomBackColour = CustomColors.SepFlags,
                            Text = rnp4 ? "3" : "D"
                        };
                    return null;

                case StripConstants.STRIP_ITEM_RVSM_FLAG:

                    if (rvsm)

                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.Custom,
                            CustomBackColour = CustomColors.SepFlags,
                            Text = "W"
                        };

                    return null;

                case StripConstants.STRIP_ITEM_VMI:
                    var vs = radarTrack == null
                        ? flightDataRecord.PredictedPosition.VerticalSpeed
                        : radarTrack.VerticalSpeed;

                    if (level == cfl || level == flightDataRecord.RFL) //level

                        return new CustomStripItem
                        {
                            Text = ""
                        };
                    if ((cfl > level && track.NewCFL) || vs > 300) //Issued or trending climb

                        return new CustomStripItem
                        {
                            Text = "↑"
                        };

                    if ((cfl > 0 && cfl < level && track.NewCFL) || vs < -300) //Issued or trending descent

                        return new CustomStripItem
                        {
                            Text = "↓"
                        };

                    return null;

                case StripConstants.STRIP_ITEM_COMPLEX:

                    if (flightDataRecord.LabelOpData.Contains("AT ") || flightDataRecord.LabelOpData.Contains(" BY ") ||
                        flightDataRecord.LabelOpData.Contains("CLEARED TO "))

                        return new CustomStripItem
                        {
                            Text = "*"
                        };

                    return null;

                case StripConstants.STRIP_ITEM_CLEARED_LEVEL:

                    if (cfl == 0)
                        return new CustomStripItem
                        {
                            Text = ""
                        };

                    return new CustomStripItem
                    {
                        Text = cfl.ToString()
                    };

                case StripConstants.STRIP_ITEM_REQUESTED_LEVEL:

                    if (flightDataRecord.State == FDR.FDRStates.STATE_INACTIVE ||
                        flightDataRecord.State == FDR.FDRStates.STATE_INACTIVE)
                        return new CustomStripItem
                        {
                            Text = rfl.ToString()
                        };

                    return new CustomStripItem
                    {
                        Text = ""
                    };

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
                case StripConstants.STRIP_ITEM_ROUTE:

                    return new CustomStripItem
                    {
                        Text = "F"
                        //OnMouseClick = ItemMouseClick
                    };


                case StripConstants.STRIP_ITEM_RADAR_IND:

                    return new CustomStripItem
                    {
                        Text = "A"
                        //OnMouseClick = 
                    };


                case StripConstants.STRIP_ITEM_ANNOT_IND:

                    var scratch = string.IsNullOrEmpty(flightDataRecord.LabelOpData);

                    if (scratch)
                        return new CustomStripItem
                        {
                            Text = "."
                        };

                    return new CustomStripItem
                    {
                        Text = "&"
                    };


                case StripConstants.STRIP_ITEM_LATERAL_FLAG:

                    if (adsc & cpdlc & rnp4 || rnp10)


                        return new CustomStripItem
                        {
                            BackColourIdentity = Colours.Identities.Custom,
                            CustomBackColour = CustomColors.SepFlags,
                            Text = rnp4 ? "4" : rnp10 ? "R" : ""
                            //OnMouseClick = ItemMouseClick
                        };
                    return null;

                case StripConstants.STRIP_ITEM_RESTR:

                    if (flightDataRecord.LabelOpData.Contains("AT ") || flightDataRecord.LabelOpData.Contains(" BY ") ||
                        flightDataRecord.LabelOpData.Contains("CLEARED TO "))

                        return new CustomStripItem
                        {
                            Text = "x"
                        };

                    return null;

                // case STRIP_ITEM_CLRD_RTE:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = "Cleared Route:" + flightDataRecord.RouteNoParse
                //         };
                //
                //     if (label.imminentConflict.TryGetValue(flightDataRecord.Callsign, out _) || (label.advisoryConflict.TryGetValue(flightDataRecord.Callsign, out)) == flightDataRecord.Callsign;
                //
                // case CPAR_ITEM_TYPE:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = 
                //         };
                //
                // case CPAR_ITEM_REQUIRED:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].callsign
                //         };
                //
                // case CPAR_ITEM_INTRUDER:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].callsign
                //         };
                //
                // case CPAR_ITEM_LOS:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].callsign
                //         };
                //
                // case CPAR_ITEM_ACTUAL:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].callsign
                //         };
                //
                // case CPAR_ITEM_PASSING:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].callsign
                //         };
                //
                // case CPAR_ITEM_CONF_SEG_START_1:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].startLatlong.ToString()
                //         };
                //
                // case CPAR_ITEM_CONF_SEG_START_2:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments2[0].startLatlong.ToString()
                //         };
                //
                // case CPAR_ITEM_CONF_SEG_END_1:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].endLatlong.ToString()
                //         };
                //
                // case CPAR_ITEM_CONF_SEG_END_2:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments2[0].endLatlong.ToString()
                //         };
                //
                // case CPAR_ITEM_STARTIME_1:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].startTime.ToString()
                //         };
                //
                // case CPAR_ITEM_STARTIME_2:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments2[0].startTime.ToString()
                //         };
                //
                // case CPAR_ITEM_ENDTIME_1:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments1[0].endTime.ToString()
                //         };
                //
                // case CPAR_ITEM_ENDTIME_2:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments2[0].endTime.ToString()
                //         };
                //
                // case CPAR_ITEM_AID_2:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = cpar.Segments2[0].callsign
                //         };
                //
                // case CPAR_ITEM_TYP_2:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = 
                //         };
                //
                // case CPAR_ITEM_SPD_2:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = 
                //         };
                //
                // case CPAR_ITEM_ALT_2:
                //
                //         return new CustomStripItem()
                //         {
                //             Text = 
                //         };

                default: return null;
            }
        }

        //public void Estimates(FDP2.FDR.ExtractedRoute.Segment estimate)
        //{ 
        //    estimate.IsPETO = true;
        //}

        private void ItemMouseClick(CustomStripItemMouseClickEventArgs e)
        {
            var flagToggled = flagtoggle.TryGetValue(e.Track.GetFDR().Callsign, out _);

            if (flagToggled)
                flagtoggle.TryRemove(e.Track.GetFDR().Callsign, out _);
            else
                flagtoggle.TryAdd(e.Track.GetFDR().Callsign, 0);
            e.Handled = true;
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