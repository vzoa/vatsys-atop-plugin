using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using vatsys;
using static vatsys.FDP2;

namespace AtopPlugin.State;

public static class JurisdictionManager
{
    private const int FdrUpdateDelay = 5000;

    public static async Task HandleFdrUpdate(FDP2.FDR fdr)
    {
        // Delay the processing so that vatSys can sync EST state
        await Task.Delay(FdrUpdateDelay);

        //TODO: Logic to exclude initial coordination if ATC Facility is staffed?
        if (!fdr.ESTed && fdr.State is not FDR.FDRStates.STATE_FINISHED && MMI.IsMySectorConcerned(fdr)) MMI.EstFDR(fdr);

        var isInControlledSector = await IsInControlledSector(fdr.GetLocation(), fdr.PRL);
        var atopState = fdr.GetAtopState();

        // check if aircraft previously tracked to avoid re-tracking manually dropped/handed off tracks
        if (AtopPluginStateManager.Activated && isInControlledSector && !fdr.IsTracked &&
            atopState is { WasHandedOff: false })
            MMI.AcceptJurisdiction(fdr);

        // Sets the RFL as the CFL if CFL is empty
        if (fdr.CFLUpper == -1 && fdr.ESTed && fdr.State > FDR.FDRStates.STATE_PREACTIVE)
            FDP2.SetCFL(fdr, fdr.RFL.ToString(), false);

        // Normal state of an aircraft progressing towards the next FIR, implicit transfer of control 1 min prior to boundary
        if (AtopPluginStateManager.Activated && isInControlledSector && fdr.IsTracked &&
            atopState is { WasHandedOff: false } &&
            DateTime.UtcNow == atopState.BoundaryTime.Subtract(TimeSpan.FromMinutes(1)))
            MMI.HandoffJurisdiction(fdr, atopState.NextSector);

        // if they're outside sector, currently tracked, and not going to re-enter, hand FP off to next sector
        // also drop them if we are not activated
        if ((!isInControlledSector && fdr.IsTrackedByMe && !await WillEnter(fdr)) ||
            (fdr.IsTrackedByMe && !AtopPluginStateManager.Activated))
        {
            // Ensure atopState is not null before accessing it
            var localAtopState = fdr.GetAtopState();
            if (localAtopState == null)
            {
                Errors.Add(new Exception($"[ERROR] atopState is null for {fdr.Callsign}"));
                return; // Prevent null reference error
            }

            if (localAtopState.NextSector == null)
            {
                Errors.Add(new Exception($"[ERROR] atopState.NextSector is null for {fdr.Callsign}"));
                return; // Prevent passing null to HandoffJurisdiction
            }

            // Proceed only if atopState and NextSector are valid
            MMI.HandoffJurisdiction(fdr, atopState.NextSector);
        }

        // Invoke the internal method SendTextMessage from Network
        Type networkType = typeof(Network);
        if (networkType != null)
        {
            // Get the method info for the internal method
            MethodInfo sendTextMessageMethod = networkType.GetMethod("SendTextMessage", BindingFlags.NonPublic | BindingFlags.Static);
            object networkInstance = networkType.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (sendTextMessageMethod != null)
            {
                if (isInControlledSector && fdr.IsTrackedByMe && fdr.State is not FDR.FDRStates.STATE_INHIBITED &&
                DateTime.UtcNow == atopState.BoundaryTime.Subtract(TimeSpan.FromSeconds(2820)))
                {
                    // Invoke the internal method
                    // Normal state of an aircraft progressing towards next FIR, send Next Data Authority ~47 mins prior to boundary
                    sendTextMessageMethod.Invoke(networkInstance, new object[] { fdr.Callsign, "NEXT DATA AUTHORITY" + atopState.NextSector?.FullName });
                }

                // Normal state of an aircraft progressing towards next FIR, transfer of comms 5 mins prior to boundary
                if (isInControlledSector && fdr.IsTrackedByMe && fdr.State is not FDR.FDRStates.STATE_INHIBITED &&
                    DateTime.UtcNow == atopState.BoundaryTime.Subtract(TimeSpan.FromSeconds(300)))
                {
                    // Invoke the internal method
                    sendTextMessageMethod.Invoke(networkInstance, new object[] { fdr.Callsign, "CONTACT" + atopState.NextSector?.FullName + atopState.NextSector?.Frequency });
                }

                // Normal state of an aircraft progressing towards next FIR, CPDLC EOS 3 mins prior to boundary
                if (isInControlledSector && fdr.IsTrackedByMe && fdr.State is not FDR.FDRStates.STATE_INHIBITED &&
                    DateTime.UtcNow == atopState.BoundaryTime.Subtract(TimeSpan.FromSeconds(180)))
                {
                    sendTextMessageMethod.Invoke(networkInstance, new object[] { fdr.Callsign, "END SERVICE" });
                }
            }


        }
    }

    public static async Task HandleRadarTrackUpdate(RDP.RadarTrack rt)
    {
        if (rt.CoupledFDR == null) return;
        await HandleFdrUpdate(rt.CoupledFDR);
    }

    private static async Task<bool> WillEnter(FDP2.FDR fdr)
    {
        return await Task.Run(() => MMI.GetSectorEntryTime(fdr) != DateTime.MaxValue);
    }

    private static async Task<bool> IsInControlledSector(Coordinate? location, int altitude)
    {
        if (location == null || double.IsNaN(location.Latitude) || double.IsNaN(location.Longitude)) return false;
        return await Task.Run(() =>
            MMI.SectorsControlled.ToList().Exists(sector => sector.IsInSector(location, altitude)));
    }
}