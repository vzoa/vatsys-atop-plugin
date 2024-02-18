#nullable enable
using AuroraLabelItemsPlugin.Models;
using vatsys;

namespace AuroraLabelItemsPlugin.Fdr;

public class ExtendedFdrState
{
    public ExtendedFdrState(FDP2.FDR fdr)
    {
        UpdateFromFdr(fdr);
        DownlinkIndicator = false;
        RadarToggleIndicator = false;
    }

    public ParsedFdrFields ParsedFdrFields { get; private set; }
    public DirectionOfFlight DirectionOfFlight { get; private set; }
    public SccFlag? HighestSccFlag { get; private set; }
    public AltitudeFlag? AltitudeFlag { get; private set; }
    public bool DownlinkIndicator { get; set; }
    public bool RadarToggleIndicator { get; set; }

    private bool PendingAltitudeChange { get; set; }
    private AltitudeBlock PreviousAltitudeBlock { get; set; }

    public void UpdateFromFdr(FDP2.FDR updatedFdr)
    {
        ParsedFdrFields = FdrParser.ParseFdrFields(updatedFdr);
        DirectionOfFlight = DirectionOfFlightCalculator.GetDirectionOfFlight(updatedFdr);
        HighestSccFlag = SccFlagCalculator.CalculateHighestPriorityFlag(updatedFdr);

        // ensure the bool for altitude change is calculated first since it is used in the altitude flag calculation
        PendingAltitudeChange =
            AltitudeCalculator.CalculateAltitudeChangePending(updatedFdr, PreviousAltitudeBlock, PendingAltitudeChange);
        AltitudeFlag = AltitudeCalculator.CalculateAltitudeFlag(updatedFdr, PendingAltitudeChange);

        // update this last since so we have the previous value for the next update
        PreviousAltitudeBlock = AltitudeBlock.ExtractAltitudeBlock(updatedFdr);
    }
}