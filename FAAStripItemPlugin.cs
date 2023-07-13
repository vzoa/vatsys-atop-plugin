using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using vatsys;
using vatsys.Plugin;
using System.Collections.Concurrent;
using System.ComponentModel.Composition; //<--Need to add a reference to System.ComponentModel.Composition
using static vatsys.Performance;
using static vatsys.Network;
using System.Linq;

namespace FAAStripItemsPlugin
{
    [Export(typeof(IPlugin))]

    public class FAAStripItemsPlugin : IStripPlugin
    {

        /// The name of the custom label item we've added to Labels
        /// in the Profile
        const string STRIP_ITEM_HEAVY = "FAA_HEAVY";
        const string STRIP_ITEM_CTLSECTOR = "AURORA_CDA";
        const string STRIP_ITEM_NXTSECTOR = "AURORA_NDA";
        const string STRIP_ITEM_EQSUFFIX = "FAA_EQUIPMENT";
        const string STRIP_ITEM_TRUNCSYM = "FAA_TRUNCATION";
        const string STRIP_ITEM_TAS = "FAA_TAS";
        const string STRIP_ITEM_GS = "FAA_GS";
        const string STRIP_ITEM_HOUR = "FAA_HOUR";
        const string STRIP_ITEM_MINUTE = "FAA_MINUTE";
        const string STRIP_ITEM_ROUTE = "FAA_ROUTE";
        const string STRIP_ITEM_REMARKS = "FAA_REMARKS";

        public string Name => throw new NotImplementedException();

        /// Plugin Name

        public void OnFDRUpdate(FDP2.FDR updated)
        {

        }

        /// This is called each time a radar track is updated
        public void OnRadarTrackUpdate(RDP.RadarTrack updated)          
        {

        }

        private void ItemMouseClick(CustomLabelItemMouseClickEventArgs e)
        {

        }
        public CustomStripItem GetCustomStripItem(string itemType, Track track, FDP2.FDR flightDataRecord, RDP.RadarTrack radarTrack)

        {
            //bool trunc = flightDataRecord.DepAirport != flightDataRecord.Route.Equals;
            bool heavy = Regex.IsMatch(flightDataRecord.AircraftWake, @"H");
            bool faaFp = !flightDataRecord.IsICAOFormat;
            int level = radarTrack == null ? flightDataRecord.PRL / 100 : radarTrack.CorrectedAltitude / 100;
            int cfl;
            bool isCfl = Int32.TryParse(flightDataRecord.CFLString, out cfl);


            if (flightDataRecord is null)
                return null;

            switch (itemType)
            {
                case STRIP_ITEM_HEAVY:

                    if (heavy)
                    {
                        return new CustomStripItem()
                        {
                            Text = "H/"
                        };
                    }
                    return null;

                case STRIP_ITEM_EQSUFFIX:

                    if (faaFp)
                    {
                        return new CustomStripItem()
                        {
                            Text = "/" + flightDataRecord.AircraftEquip
                        };
                    }
                    return null;


                 case STRIP_ITEM_NXTSECTOR:
                
                       return new CustomStripItem()
                       {
                           Text = flightDataRecord.HandoffSector.ToString()
                       };

                case STRIP_ITEM_TAS:
                    var mach = flightDataRecord.TAS / 581.0;
                    return new CustomStripItem()
                    {
                        //Text = "M" + Convert.ToDecimal(mach).ToString("F2").Replace(".", "")
                        Text = "T" + flightDataRecord.TAS.ToString("000")//format as 3 digits (with leading zeros)
                    };

               case STRIP_ITEM_GS:

                   return new CustomStripItem()
                   {
                       Text = radarTrack == null ? null :"G" + radarTrack.GroundSpeed.ToString("000")//format as 3 digits (with leading zeros)
                   };

                case STRIP_ITEM_HOUR:
                   
                    return new CustomStripItem()
                    {
                        Text = flightDataRecord.PredictedPosition.ETO.ToString("HH") //format as 2 digit hour
                    };

                case STRIP_ITEM_MINUTE:


                    return new CustomStripItem()
                    {
                        Text = flightDataRecord.PredictedPosition.ETO.ToString("mm") //format as 2 digit minute
                    };

               case STRIP_ITEM_ROUTE:
                  
                   return new CustomStripItem()
                   {
                       Text = flightDataRecord.Route
                   };

                case STRIP_ITEM_REMARKS:

                    return new CustomStripItem()
                    {
                        Text = "○" + flightDataRecord.Remarks
                    };
                //case STRIP_ITEM_TRUNCSYM:
                //
                //    if (trunc)
                //    {
                //        return new CustomStripItem()
                //        {
                //            Text = "./."
                //        };
                //    }
                //    return null;

                default: return null;
            }
        }
    }
}
