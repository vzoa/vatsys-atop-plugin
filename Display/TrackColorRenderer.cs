using AtopPlugin.Models;
using vatsys;
using vatsys.Plugin;

namespace AtopPlugin.Display;

public static class TrackColorRenderer
{
    public static CustomColour GetAsdColor(Track track)
    {
        var fdr = track.GetFDR();

        if (fdr == null) return null;

        // TODO(msalikhov): render conflict color
        return IsInJurisdiction(track) ? GetDirectionColour(fdr) : null;
    }

    private static CustomColour GetDirectionColour(FDP2.FDR fdr)
    {
        return fdr.GetAtopState().DirectionOfFlight switch
        {
            DirectionOfFlight.Eastbound => CustomColors.EastboundColour,
            DirectionOfFlight.Westbound => CustomColors.WestboundColour,
            _ => null
        };
    }

    private static bool IsInJurisdiction(Track track)
    {
        return track.State == MMI.HMIStates.Jurisdiction;
    }
}