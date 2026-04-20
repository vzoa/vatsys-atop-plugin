// Decompiled with JetBrains decompiler
// Type: AtopPlugin.Conflict.IMinimaCalculator
// Assembly: vatsys-atop-plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53F96C82-A187-47C4-B3F9-41A57159448E
// Assembly location: C:\Users\domyn\Downloads\vatsys-atop-plugin.dll

using System;
using vatsys;

#nullable disable
namespace AtopPlugin.Conflict
{
  public interface IMinimaCalculator
  {
    int GetLateralMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);

    int GetVerticalMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);

    TimeSpan GetLongitudinalTimeMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);

    int? GetLongitudinalDistanceMinima(FDP2.FDR fdr1, FDP2.FDR fdr2);
  }
}
