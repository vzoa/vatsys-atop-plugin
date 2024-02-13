using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using vatsys;
using static vatsys.SectorsVolumes;

namespace AuroraLabelItemsPlugin
{
    public class FPAP
    {
        public bool adsc;
        public int alt;
        public int cfl;
        public bool cpdlc;
        public bool jet;
        public bool pbcs;
        public Match pbn;
        public int prl;
        public int rfl;
        public bool rnp10;
        public bool rnp4;
        public Match sur;
        public double vs;

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            pbn = Regex.Match(updated.Remarks, @"PBN\/\w+\s");
            sur = Regex.Match(updated.Remarks, @"SUR\/\w+\s");
            rnp10 = pbn.Value.Contains("A1") && updated.AircraftEquip.Contains("R");
            rnp4 = pbn.Value.Contains("L1") && updated.AircraftEquip.Contains("R");
            pbcs = sur.Value.Contains("RSP180") && updated.AircraftSurvEquip.Contains("P2");
            cpdlc = updated.AircraftEquip.Contains("J5") || updated.AircraftEquip.Contains("J7");
            adsc = updated.AircraftSurvEquip.Contains("D1");
            jet = updated.PerformanceData?.IsJet ?? false;
            vs = updated.PredictedPosition.VerticalSpeed;
            prl = updated.PRL / 100;
            cfl = updated.CFLUpper;
            rfl = updated.RFL;
            alt = cfl == -1 ? rfl : cfl;
        }

        public void TOC(RDP.RadarTrack rt)
        {
            var fdr = rt.CoupledFDR;
            if (fdr != null && rt != null) TransferOfControl(fdr);
        }


        public static void TransferOfControl(FDP2.FDR fdr)
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
    }
}