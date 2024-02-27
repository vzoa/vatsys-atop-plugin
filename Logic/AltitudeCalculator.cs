using AtopPlugin.Models;
using vatsys;

namespace AtopPlugin.Logic;

public static class AltitudeCalculator
{
    private const int LevelFlightThreshold = 300;

    public static bool CalculateAltitudeChangePending(FDP2.FDR updatedFdr, AltitudeBlock previousBlock,
        bool wasPreviouslyPending)
    {
        var newBlock = AltitudeBlock.ExtractAltitudeBlock(updatedFdr);
        return (newBlock != previousBlock || wasPreviouslyPending)
               && !IsWithinThreshold(updatedFdr.PRL, newBlock);
    }

    public static AltitudeFlag? CalculateAltitudeFlag(FDP2.FDR fdr, bool pendingAltitudeChange)
    {
        var altitudeBlock = AltitudeBlock.ExtractAltitudeBlock(fdr);
        var (altitudeLower, altitudeUpper) = altitudeBlock;
        var isOutsideThresholdAndNotBlank = !IsWithinThreshold(fdr.PRL, altitudeBlock) && !IsBlank(fdr.PRL);

        return isOutsideThresholdAndNotBlank switch
        {
            true when pendingAltitudeChange && fdr.PRL < altitudeLower => AltitudeFlag.Climbing,
            true when pendingAltitudeChange && fdr.PRL > altitudeUpper => AltitudeFlag.Descending,
            true when !pendingAltitudeChange && fdr.PRL < altitudeLower => AltitudeFlag.DeviatingBelow,
            true when !pendingAltitudeChange && fdr.PRL > altitudeUpper => AltitudeFlag.DeviatingAbove,
            _ => null
        };
    }

    public static bool IsWithinThreshold(int pilotReportedAltitude, AltitudeBlock altitudeBlock)
    {
        var lowerWithThreshold = altitudeBlock.LowerAltitude - LevelFlightThreshold;
        var upperWithThreshold = altitudeBlock.UpperAltitude + LevelFlightThreshold;
        return pilotReportedAltitude > lowerWithThreshold && pilotReportedAltitude < upperWithThreshold;
    }

    private static bool IsBlank(int altitude)
    {
        return altitude < 100;
    }
}