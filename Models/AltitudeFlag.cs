namespace AtopPlugin.Models;

public class AltitudeFlag
{
    private AltitudeFlag(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static AltitudeFlag Climbing => new(Symbols.Climbing);
    public static AltitudeFlag Descending => new(Symbols.Descending);
    public static AltitudeFlag DeviatingAbove => new(Symbols.DeviatingAbove);
    public static AltitudeFlag DeviatingBelow => new(Symbols.DeviatingBelow);
}