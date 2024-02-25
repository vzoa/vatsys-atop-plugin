namespace AtopPlugin.Models;

public class SccFlag
{
    private SccFlag(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static SccFlag Rnp => new("RNP");
    public static SccFlag Emg => new("EMG");
    public static SccFlag Rcf => new("RCF");
    public static SccFlag Mti => new("MTI");
}