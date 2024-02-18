using AuroraLabelItemsPlugin.Fdr;
using vatsys;
using vatsys.Plugin;

namespace AuroraLabelItemsPlugin.Display;

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
        return fdr.GetExtendedState().DirectionOfFlight switch
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