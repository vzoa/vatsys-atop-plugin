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
    public static SccFlag Adsc => new("ADSC"); // ADSC flag to represent pilot has disconnected
}