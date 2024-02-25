using AtopPlugin.Models;
using vatsys;
using vatsys.Plugin;

namespace AtopPlugin.Display;

public static class TrackColorRenderer
{
    public static CustomColour? GetAsdColor(Track track)
    {
        var fdr = track.GetFDR();

        if (fdr == null) return null;

        return (IsInJurisdiction(track), fdr.State) switch
        {
            { Item1: true } => GetConflictColour(fdr) ?? GetDirectionColour(fdr),
            { State: FDP2.FDR.FDRStates.STATE_PREACTIVE or FDP2.FDR.FDRStates.STATE_COORDINATED } =>
                GetConflictColour(fdr),
            _ => null
        };
    }

    private static CustomColour? GetDirectionColour(FDP2.FDR fdr)
    {
        return fdr.GetAtopState()?.DirectionOfFlight switch
        {
            DirectionOfFlight.Eastbound => CustomColors.EastboundColour,
            DirectionOfFlight.Westbound => CustomColors.WestboundColour,
            _ => null
        };
    }

    private static CustomColour? GetConflictColour(FDP2.FDR fdr)
    {
        return fdr.GetAtopState()?.Conflicts switch
        {
            { ActualConflicts.Count: > 0 } or { ImminentConflicts.Count: > 0 } => CustomColors.Imminent,
            { AdvisoryConflicts.Count: > 0 } => CustomColors.Advisory,
            _ => null
        };
    }

    private static bool IsInJurisdiction(Track track)
    {
        return track.State == MMI.HMIStates.Jurisdiction;
    }
}