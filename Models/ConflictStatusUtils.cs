// Decompiled with JetBrains decompiler
// Type: AtopPlugin.Models.ConflictStatusUtils
// Assembly: vatsys-atop-plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53F96C82-A187-47C4-B3F9-41A57159448E
// Assembly location: C:\Users\domyn\Downloads\vatsys-atop-plugin.dll

#nullable disable
namespace AtopPlugin.Models
{
  public static class ConflictStatusUtils
  {
    public static ConflictStatus From(bool actual, bool imminent, bool advisory)
    {
      if (true)
        ;
      ConflictStatus conflictStatus = actual ? ConflictStatus.Actual : (imminent ? ConflictStatus.Imminent : (advisory ? ConflictStatus.Advisory : ConflictStatus.None));
      if (true)
        ;
      return conflictStatus;
    }
  }
}
