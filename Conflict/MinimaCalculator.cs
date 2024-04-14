using System;
using vatsys;

namespace AtopPlugin.Conflict;

public class MinimaCalculator : IMinimaCalculator
{
    public static readonly MinimaCalculator Instance = new();
    private static readonly IMinimaCalculator MinimaCalculatorImplementation = GetMinimaDelegate();

    public int GetLateralMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        return MinimaCalculatorImplementation.GetLateralMinima(fdr1, fdr2);
    }

    public int GetVerticalMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        return MinimaCalculatorImplementation.GetVerticalMinima(fdr1, fdr2);
    }

    public TimeSpan GetLongitudinalTimeMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        return MinimaCalculatorImplementation.GetLongitudinalTimeMinima(fdr1, fdr2);
    }

    public int? GetLongitudinalDistanceMinima(FDP2.FDR fdr1, FDP2.FDR fdr2)
    {
        return MinimaCalculatorImplementation.GetLongitudinalDistanceMinima(fdr1, fdr2);
    }

    private static IMinimaDelegate GetMinimaDelegate()
    {
        return Config.MinimaRegion switch
        {
            MinimaRegion.Pacific => new PacificMinimaDelegate(),
            MinimaRegion.NorthAtlantic => throw new ArgumentOutOfRangeException(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}