namespace AuroraLabelItemsPlugin.Fdr;

public class AltitudeFlag
{
    private AltitudeFlag(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static AltitudeFlag Climbing => new("↑");
    public static AltitudeFlag Descending => new("↓");
    public static AltitudeFlag DeviatingAbove => new("+");
    public static AltitudeFlag DeviatingBelow => new("-");
}