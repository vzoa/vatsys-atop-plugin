using System;
using vatsys;

namespace AtopPlugin.Conflict;

public interface IMinimaCalculator
{
    public int GetLateralMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
    public int GetVerticalMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
    public TimeSpan GetLongitudinalTimeMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
    public int? GetLongitudinalDistanceMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
}

public interface IMinimaDelegate : IMinimaCalculator
{
    public MinimaRegion GetRegion();
}

public enum MinimaRegion
{
    Pacific,
    NorthAtlantic
}