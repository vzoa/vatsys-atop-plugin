using System;
using System.Diagnostics;
using System.Linq;
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
                Debug.WriteLine($"[ERROR] atopState is null for {fdr.Callsign}");
                return; // Prevent null reference error
            }

            if (localAtopState.NextSector == null)
            {
                Debug.WriteLine($"[ERROR] atopState.NextSector is null for {fdr.Callsign}");
                return; // Prevent passing null to HandoffJurisdiction
            }

            // Proceed only if atopState and NextSector are valid
            MMI.HandoffJurisdiction(fdr, atopState.NextSector);
        }

        // Normal state of an aircraft progressing towards next FIR, transfer of comms 5 mins prior to boundary
        if (isInControlledSector && fdr.IsTrackedByMe && fdr.State is not FDR.FDRStates.STATE_INHIBITED &&
            DateTime.UtcNow == atopState.BoundaryTime.Subtract(TimeSpan.FromMinutes(1)))
            Network.SendRadioMessage("MONITOR");
        
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