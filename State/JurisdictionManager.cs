using System;
using System.Linq;
using System.Threading.Tasks;
using vatsys;

namespace AtopPlugin.State;

public static class JurisdictionManager
{
    private const int FdrUpdateDelay = 5000;

    public static async Task HandleFdrUpdate(FDP2.FDR fdr)
    {
        // Delay the processing so that vatSys can sync EST state
        await Task.Delay(FdrUpdateDelay);

        //TODO: Logic to exclude initial coordination if ATC Facility is staffed?
        if (!fdr.ESTed && MMI.IsMySectorConcerned(fdr)) MMI.EstFDR(fdr);

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
            MMI.HandoffJurisdiction(fdr, atopState.NextSector);
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