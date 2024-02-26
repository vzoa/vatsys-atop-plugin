using System.Linq;
using System.Text.RegularExpressions;
using AtopPlugin.Models;
using vatsys;

namespace AtopPlugin.Logic;

public static class FlightDataCalculator
{
    private const string Rnp10Pbn = "A1";
    private const string Rnp4Pbn = "L1";

    private const string Rsp180Sur = "RSP180";

    private const string AdscSurvEquip = "D1";
    private const string Rcp240SurvEquip = "P2";

    private const string PbnEquip = "R";

    private static readonly string[] CpdlcEquip =
    {
        "J5", "J7"
    };

    public static CalculatedFlightData GetCalculatedFlightData(FDP2.FDR fdr)
    {
        // TODO(msalikhov): can we pull from fdr.PBNCapability
        var pbn = Regex.Match(fdr.Remarks, @"PBN\/\w+\s").Value;
        var sur = Regex.Match(fdr.Remarks, @"SUR\/\w+\s").Value;

        var isPbn = fdr.AircraftEquip.Contains(PbnEquip);

        return new CalculatedFlightData(
            isPbn && pbn.Contains(Rnp4Pbn),
            isPbn && pbn.Contains(Rnp10Pbn),
            CpdlcEquip.Any(cpdlcVal => fdr.AircraftEquip.Contains(cpdlcVal)),
            fdr.AircraftSurvEquip.Contains(AdscSurvEquip),
            sur.Contains(Rsp180Sur) && fdr.AircraftSurvEquip.Contains(Rcp240SurvEquip)
        );
    }
}