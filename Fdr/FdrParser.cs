using System.Linq;
using System.Text.RegularExpressions;
using vatsys;

namespace AuroraLabelItemsPlugin.Fdr;

public static class FdrParser
{
    private const string Rnp10Pbn = "A1";
    private const string Rnp4Pbn = "L1";

    private const string AdscSurvEquip = "D1";

    private static readonly string[] CpdlcEquip =
    {
        "J5", "J7"
    };

    public static ParsedFdrFields ParseFdrFields(FDP2.FDR fdr)
    {
        // TODO(msalikhov): can we pull from fdr.PBNCapability
        var pbn = Regex.Match(fdr.Remarks, @"PBN\/\w+\s").Value;

        return new ParsedFdrFields(
            pbn.Contains(Rnp4Pbn),
            pbn.Contains(Rnp10Pbn),
            CpdlcEquip.Any(cpdlcVal => fdr.AircraftEquip.Contains(cpdlcVal)),
            fdr.AircraftSurvEquip.Contains(AdscSurvEquip)
        );
    }
}