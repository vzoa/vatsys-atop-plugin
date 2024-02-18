using System;
using AuroraLabelItemsPlugin.Fdr;
using AuroraLabelItemsPlugin.Models;
using vatsys;

namespace AuroraLabelItemsPlugin.Conflict;

public static class MinimaCalculator
{
    private const int Rnp4Lateral = 23;
    private const int Rnp10Lateral = 50;
    private const int StandardLateral = 100;

    private const int StandardVertical = 1000;
    private const int NonRvsmVertical = 2000;
    private const int SupersonicVertical = 4000;
    private const int Above600Vertical = 5000;

    private const int TimeLongitudinal = 15;
    private const int JetLongitudinal = 10;
    private const int DistanceLongitudinal = 50;
    private const int Rnp4Longitudinal = 30;

    public static int GetLateralMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        if (CanApplyRnp4(fdr1) && CanApplyRnp4(fdr2))
        {
            return Rnp4Lateral;
        }

        if (CanApplyRnp10(fdr1) && CanApplyRnp10(fdr2))
        {
            return Rnp10Lateral;
        }

        return StandardLateral;
    }

    public static int GetVerticalMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        var block1 = AltitudeBlock.ExtractAltitudeBlock(fdr1);
        var block2 = AltitudeBlock.ExtractAltitudeBlock(fdr2);

        if (block1.IsAbove600() || block2.IsAbove600()) // technically this only applies if they are military
        {
            return Above600Vertical;
        }

        if (block1.IsAbove450() || block2.IsAbove450()) // technically this only applies if one is supersonic
        {
            return SupersonicVertical;
        }

        if (!(block1.IsBelowRvsm() || block2.IsBelowRvsm()) && (block1.IsAboveRvsm() || block2.IsAboveRvsm()))
        {
            return NonRvsmVertical;
        }

        return StandardVertical;
    }

    public static TimeSpan GetLongitudinalTime(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        return IsJet(fdr1) && IsJet(fdr2)
            ? new TimeSpan(0, JetLongitudinal, 0)
            : new TimeSpan(0, TimeLongitudinal, 0);
    }

    public static int? GetLongitudinalDistance(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        if (CanApplyRnp4(fdr1) && CanApplyRnp4(fdr2))
        {
            return Rnp4Longitudinal;
        }

        if (HasDatalink(fdr1) && HasDatalink(fdr2) && CanApplyRnp10(fdr1) && CanApplyRnp10(fdr2))
        {
            return DistanceLongitudinal;
        }

        return null;
    }

    private static bool HasDatalink(FDP2.FDR fdr)
    {
        return fdr.GetExtendedState().ParsedFdrFields is { Pbcs: true, Adsc: true, Cpdlc: true };
    }

    private static bool CanApplyRnp4(FDP2.FDR fdr)
    {
        return HasDatalink(fdr) && fdr.GetExtendedState().ParsedFdrFields.Rnp4;
    }

    private static bool CanApplyRnp10(FDP2.FDR fdr)
    {
        return fdr.GetExtendedState().ParsedFdrFields is { Rnp10: true };
    }

    private static bool IsJet(FDP2.FDR fdr)
    {
        return fdr.PerformanceData.IsJet;
    }
}