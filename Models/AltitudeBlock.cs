using vatsys;

namespace AtopPlugin.Models;

public record struct AltitudeBlock(int LowerAltitude, int UpperAltitude)
{
    private const int Fl450 = 45000;
    private const int Fl600 = 60000;

    public bool IsBelowRvsm()
    {
        return UpperAltitude <= FDP2.RVSM_BAND_LOWER;
    }

    public bool IsAbove450()
    {
        return UpperAltitude > Fl450 || LowerAltitude > Fl450;
    }

    public bool IsAbove600()
    {
        return UpperAltitude > Fl600 || LowerAltitude > Fl600;
    }

    public bool IsAboveRvsm()
    {
        return UpperAltitude > FDP2.RVSM_BAND_UPPER || LowerAltitude > FDP2.RVSM_BAND_UPPER;
    }

    public static int Difference(AltitudeBlock block1, AltitudeBlock block2)
    {
        // check for intersection
        if (block1.LowerAltitude <= block2.UpperAltitude && block2.LowerAltitude <= block1.UpperAltitude) return 0;

        var isBlock1Lower = block1.UpperAltitude < block2.LowerAltitude;

        return isBlock1Lower
            ? block2.LowerAltitude - block1.UpperAltitude
            : block1.LowerAltitude - block2.UpperAltitude;
    }

    public static AltitudeBlock ExtractAltitudeBlock(FDP2.FDR fdr)
    {
        var cflUpperAsNull = AsNullWhenNegative(fdr.CFLUpper);
        var cflLowerAsNull = AsNullWhenNegative(fdr.CFLLower);
        var altitudeUpper = cflUpperAsNull ?? fdr.RFL;
        var altitudeLower = cflLowerAsNull ?? (cflUpperAsNull ?? fdr.RFL);
        return new AltitudeBlock(altitudeLower, altitudeUpper);
    }

    private static int? AsNullWhenNegative(int number)
    {
        return number >= 0 ? number : null;
    }

    public override string ToString()
    {
        if (LowerAltitude == UpperAltitude) return (UpperAltitude / 100).ToString();

        return LowerAltitude / 100 + "B" + UpperAltitude / 100;
    }
}