// Decompiled with JetBrains decompiler
// Type: AtopPlugin.CustomColors
// Assembly: vatsys-atop-plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53F96C82-A187-47C4-B3F9-41A57159448E
// Assembly location: C:\Users\domyn\Downloads\vatsys-atop-plugin.dll

using vatsys.Plugin;

#nullable disable
namespace AtopPlugin
{
  public static class CustomColors
  {
    public static readonly CustomColour SepFlags = new CustomColour((byte) 0, (byte) 196, (byte) 253);
    public static readonly CustomColour Pending = new CustomColour((byte) 46, (byte) 139, (byte) 87);
    public static readonly CustomColour EastboundColour = new CustomColour((byte) 240, byte.MaxValue, byte.MaxValue);
    public static readonly CustomColour WestboundColour = new CustomColour((byte) 240, (byte) 231, (byte) 140);
    public static readonly CustomColour NonRvsm = new CustomColour((byte) 242, (byte) 133, (byte) 0);
    public static readonly CustomColour Probe = new CustomColour((byte) 70, (byte) 247, (byte) 57);
    public static readonly CustomColour NotCda = new CustomColour((byte) 211, (byte) 28, (byte) 111);
    public static readonly CustomColour Advisory = new CustomColour(byte.MaxValue, (byte) 165, (byte) 0);
    public static readonly CustomColour Imminent = new CustomColour(byte.MaxValue, (byte) 0, (byte) 0);
    public static readonly CustomColour SpecialConditionCode = new CustomColour(byte.MaxValue, byte.MaxValue, (byte) 0);
    public static readonly CustomColour ApsBlue = new CustomColour((byte) 141, (byte) 182, (byte) 205);
  }
}
