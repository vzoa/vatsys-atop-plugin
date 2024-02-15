using vatsys;

namespace AuroraLabelItemsPlugin.Fdr;

public record struct AltitudeBlock(int LowerAltitude, int UpperAltitude)
{
    public static AltitudeBlock ExtractAltitudeBlock(FDP2.FDR fdr)
    {
        var altitudeUpper = ExtractClearedOrRequestedValue(fdr.CFLUpper, fdr.RFL);
        var altitudeLower = ExtractClearedOrRequestedValue(fdr.CFLLower, fdr.RFL);
        return new AltitudeBlock(altitudeLower, altitudeUpper);
    }

    private static int ExtractClearedOrRequestedValue(int clearedValue, int requestedValue)
    {
        return clearedValue == -1 ? requestedValue : clearedValue;
    }
}