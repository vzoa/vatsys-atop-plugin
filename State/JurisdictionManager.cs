using System;
using System.Linq;
using vatsys;

namespace AtopPlugin.State;

public static class JurisdictionManager
{
    public static void HandleFdrUpdate(FDP2.FDR fdr)
    {
        // TODO(msalikhov): Because of this its impossible to cancel an FDR that's disconnected
        if (!fdr.ESTed && MMI.IsMySectorConcerned(fdr)) MMI.EstFDR(fdr);

        var isInControlledSector = IsInControlledSector(fdr.GetLocation(), fdr.PRL);

        // check if aircraft previously tracked to avoid re-tracking manually dropped/handed off tracks
        if (isInControlledSector && !fdr.IsTracked && !fdr.GetAtopState().PreviouslyTracked)
        {
            MMI.AcceptJurisdiction(fdr);
            fdr.GetAtopState().PreviouslyTracked = true;
        }

        // if they're outside sector, currently tracked, and not going to re-enter, drop them
        if (!isInControlledSector && fdr.IsTrackedByMe && !WillEnter(fdr)) MMI.HandoffToNone(fdr);
    }

    public static void HandleRadarTrackUpdate(RDP.RadarTrack rt)
    {
        if (rt.CoupledFDR == null) return;
        HandleFdrUpdate(rt.CoupledFDR);
    }

    private static bool WillEnter(FDP2.FDR fdr)
    {
        return MMI.GetSectorEntryTime(fdr) != DateTime.MaxValue;
    }

    private static bool IsInControlledSector(Coordinate? location, int altitude)
    {
        if (location == null || double.IsNaN(location.Latitude) || double.IsNaN(location.Longitude)) return false;
        return MMI.SectorsControlled.ToList().Exists(sector => sector.IsInSector(location, altitude));
    }
}