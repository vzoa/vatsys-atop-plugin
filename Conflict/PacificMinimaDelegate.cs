using System;
using AtopPlugin.Models;
using vatsys;
using System.Linq;

namespace AtopPlugin.Conflict;

public class PacificMinimaDelegate : IMinimaDelegate
{
    private const int Rnp4Lateral = 23;
    private const int Rnp10Lateral = 50;
    private const int StandardLateral = 100;

    private const int StandardVertical = 1000;
    private const int NonRvsmVertical = 2000;
    private const int SupersonicVertical = 4000;
    public const int Above600Vertical = 5000;

    private const int TimeLongitudinal = 15;
    private const int JetLongitudinal = 10;
    private const int DistanceLongitudinal = 50;
    private const int Rnp4Longitudinal = 30;

    public MinimaRegion GetRegion()
    {
        return MinimaRegion.Pacific;
    }

    public int GetLateralMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        if (CanApplyRnp4(fdr1) && CanApplyRnp4(fdr2)) return Rnp4Lateral;

        if (CanApplyRnp10(fdr1) && CanApplyRnp10(fdr2)) return Rnp10Lateral;

        if ((CanApplyRnp4(fdr1) && CanApplyRnp10(fdr2)) || (CanApplyRnp10(fdr1) && CanApplyRnp4(fdr2)))
            return Rnp10Lateral;

        if ((!CanApplyRnp4(fdr1) && !CanApplyRnp10(fdr1) && CanApplyRnp10(fdr2)) || CanApplyRnp4(fdr2))
            return (Rnp10Lateral + StandardLateral) / 2;

        return StandardLateral;
    }

    public int GetVerticalMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        var block1 = AltitudeBlock.ExtractAltitudeBlock(fdr1);
        var block2 = AltitudeBlock.ExtractAltitudeBlock(fdr2);

        if (block1.IsAbove600() || block2.IsAbove600()) // technically this only applies if they are military
            return Above600Vertical;

        if (block1.IsAbove450() || block2.IsAbove450()) // technically this only applies if one is supersonic
            return SupersonicVertical;

        if (!(block1.IsBelowRvsm() || block2.IsBelowRvsm()) && (block1.IsAboveRvsm() || block2.IsAboveRvsm()))
            return NonRvsmVertical;

        return StandardVertical;
    }

    public TimeSpan GetLongitudinalTimeMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        return fdr1.IsJet() && fdr2.IsJet()
            ? new TimeSpan(0, JetLongitudinal, 0)
            : new TimeSpan(0, TimeLongitudinal, 0);
    }

    public int? GetLongitudinalDistanceMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        if (CanApplyRnp4(fdr1) && CanApplyRnp4(fdr2)) return Rnp4Longitudinal;

        if (HasDatalink(fdr1) && HasDatalink(fdr2) && CanApplyRnp10(fdr1) && CanApplyRnp10(fdr2))
            return DistanceLongitudinal;

        return null;
    }

    private static bool HasDatalink(FDP2.FDR fdr)
    {
        return fdr.GetAtopState()?.CalculatedFlightData is { Pbcs: true, Adsc: true, Cpdlc: true };
    }

    private static bool CanApplyRnp4(FDP2.FDR fdr)
    {
        return HasDatalink(fdr) && fdr.GetAtopState()?.CalculatedFlightData is { Rnp4: true };
    }

    private static bool CanApplyRnp10(FDP2.FDR fdr)
    {
        return fdr.GetAtopState()?.CalculatedFlightData is { Rnp10: true };
    }
}