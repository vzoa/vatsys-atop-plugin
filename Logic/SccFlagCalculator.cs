#nullable enable
using AuroraLabelItemsPlugin.Models;
using vatsys;

namespace AuroraLabelItemsPlugin.Logic;

public static class SccFlagCalculator
{
    private const int RadioFailureCode = 7600;
    private const int EmergencyCode = 7700;
    private const int MilitaryInterceptCode = 7777;

    public static SccFlag? CalculateHighestPriorityFlag(FDP2.FDR fdr)
    {
        var parsedFdrFields = fdr.GetAtopState().CalculatedFlightData;

        var transponderCode = fdr.GetTransponderCode();
        switch (transponderCode)
        {
            case EmergencyCode:
                return SccFlag.Emg;
            case RadioFailureCode:
                return SccFlag.Rcf;
            case MilitaryInterceptCode:
                return SccFlag.Mti;
        }

        if (!parsedFdrFields.Rnp4 || !parsedFdrFields.Rnp10) return SccFlag.Rnp;

        return null;
    }
}