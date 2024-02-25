using System.Linq;
using vatsys;

namespace AtopPlugin.State;

public static class JurisdictionManager
{
    public static void HandleFdrUpdate(FDP2.FDR fdr)
    {
        var isInSector = MMI.SectorsControlled.ToList().Exists(sector => sector.IsInSector(fdr.GetLocation(), fdr.PRL));

        switch (isInSector)
        {
            // check if aircraft previously tracked to avoid re-tracking manually dropped/handed off tracks
            case true when !fdr.IsTracked && !fdr.GetAtopState().PreviouslyTracked:
                MMI.AcceptJurisdiction(fdr);
                fdr.GetAtopState().PreviouslyTracked = true;
                break;

            // if they're outside sector and currently, tracked, drop them
            case false when fdr.IsTrackedByMe:
                MMI.HandoffToNone(fdr);
                break;
        }
    }
}