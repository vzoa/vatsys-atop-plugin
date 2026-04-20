// Decompiled with JetBrains decompiler
// Type: AtopPlugin.Display.ASDRenderer
// Assembly: vatsys-atop-plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53F96C82-A187-47C4-B3F9-41A57159448E
// Assembly location: C:\Users\domyn\Downloads\vatsys-atop-plugin.dll

using AtopPlugin.Conflict;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System;
using vatsys;

#nullable disable
namespace AtopPlugin.Display
{
  public class ASDRenderer
  {
    private DeviceContext1 dc;
    private SolidColorBrush imminentBrush;
    private SolidColorBrush warningBrush;
    private TextFormat fontFormat;
    private int MainFontHeight = -1;
    private int MainFontWidth = -1;
    private RenderParam renderParams;

    private Vector2 ConvertLLToScreen(Coordinate ll, RenderParam renderParams)
    {
      if (ll == null)
        return Vector2.Zero;
      double num1 = ll.LongitudeRads - renderParams.ScreenCentre.LongitudeRads;
      double num2 = 2.0 / (1.0 + renderParams.ScreenCentre.SinLatitudeRads * ll.SinLatitudeRads + renderParams.ScreenCentre.CosLatitudeRads * ll.CosLatitudeRads * Math.Cos(num1));
      double num3 = num2 * ll.CosLatitudeRads * Math.Sin(num1);
      double num4 = num2 * (renderParams.ScreenCentre.CosLatitudeRads * ll.SinLatitudeRads - renderParams.ScreenCentre.SinLatitudeRads * ll.CosLatitudeRads * Math.Cos(num1));
      Vector2 point = new Vector2((float) (num3 * (renderParams.ViewScale * renderParams.Zoom)), (float) (num4 * (renderParams.ViewScale * renderParams.Zoom * -1.0)));
      return Matrix3x2.TransformPoint(Matrix3x2.Rotation(renderParams.Rotation), point);
    }

    public void PaintConflicts(RenderParam renderParams, LateralConflictCalculator[] conflicts)
    {
      foreach (LateralConflictCalculator conflict in conflicts)
      {
        foreach (LateralConflictCalculator.ConflictSegment conflictSegment in conflict.ConflictSegments)
        {
          Vector2 screen1 = this.ConvertLLToScreen(conflictSegment.StartLatlong, renderParams);
          Vector2 screen2 = this.ConvertLLToScreen(conflictSegment.EndLatlong, renderParams);
          this.dc.DrawLine((RawVector2) screen1, (RawVector2) screen2, (Brush) this.imminentBrush, 3f);
          this.imminentBrush = new SolidColorBrush((RenderTarget) this.dc, (RawColor4) Colours.GetColourDX(Colours.Identities.Emergency));
          this.warningBrush = new SolidColorBrush((RenderTarget) this.dc, (RawColor4) Colours.GetColourDX(Colours.Identities.Warning));
          if (conflictSegment.StartTime != DateTime.MaxValue)
          {
            string text = conflictSegment.StartTime.ToString("HHmm") + "\n" + conflictSegment.Callsign + " ";
            this.dc.DrawText(text, this.fontFormat, (RawRectangleF) new RectangleF(screen1.X, screen1.Y, (float) (text.Length * this.MainFontWidth), (float) this.MainFontHeight), (Brush) this.imminentBrush);
          }
          if (conflictSegment.EndTime != DateTime.MaxValue)
          {
            string text = conflictSegment.EndTime.ToString("HHmm") + "\n" + conflictSegment.Callsign + " ";
            this.dc.DrawText(text, this.fontFormat, (RawRectangleF) new RectangleF(screen2.X, screen2.Y, (float) (text.Length * this.MainFontWidth), (float) this.MainFontHeight), (Brush) this.imminentBrush);
          }
        }
      }
    }
  }
}
