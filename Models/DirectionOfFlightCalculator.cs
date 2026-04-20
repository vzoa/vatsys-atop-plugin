// Decompiled with JetBrains decompiler
// Type: AtopPlugin.Models.DirectionOfFlightCalculator
// Assembly: vatsys-atop-plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53F96C82-A187-47C4-B3F9-41A57159448E
// Assembly location: C:\Users\domyn\Downloads\vatsys-atop-plugin.dll

using vatsys;

#nullable disable
namespace AtopPlugin.Models
{
  public static class DirectionOfFlightCalculator
  {
    public static DirectionOfFlight GetDirectionOfFlight(FDP2.FDR fdr)
    {
      if (fdr.ParsedRoute.Count <= 1)
        return DirectionOfFlight.Undetermined;
      double track = Conversions.CalculateTrack(fdr.ParsedRoute.First().Intersection.LatLong, fdr.ParsedRoute.Last().Intersection.LatLong);
      return track < 0.0 || track >= 180.0 ? DirectionOfFlight.Westbound : DirectionOfFlight.Eastbound;
    }
  }
}
