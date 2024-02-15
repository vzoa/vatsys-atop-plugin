using vatsys;

namespace AuroraLabelItemsPlugin.Fdr;

public static class FdrExtensions
{
    public static ExtendedFdrState GetExtendedState(this FDP2.FDR fdr)
    {
        return FdrManager.GetExtendedFdrState(fdr.Callsign);
    }

    public static int? GetTransponderCode(this FDP2.FDR fdr)
    {
        return fdr.CoupledTrack?.ActualAircraft.TransponderCode;
    }
}