using vatsys;

namespace AtopPlugin.Models;

public enum DirectionOfFlight
{
    Eastbound,
    Westbound,
    Undetermined
}

public static class DirectionOfFlightCalculator
{
    public static DirectionOfFlight GetDirectionOfFlight(FDP2.FDR fdr)
    {
        // TODO(msalikhov): handle case where aircraft go the "long way around"
        if (fdr.ParsedRoute.Count <= 1) return DirectionOfFlight.Undetermined;

        var track = Conversions.CalculateTrack(fdr.ParsedRoute.First().Intersection.LatLong,
            fdr.ParsedRoute.Last().Intersection.LatLong);

        return track is >= 0.0 and < 180.0 ? DirectionOfFlight.Eastbound : DirectionOfFlight.Westbound;
    }
}