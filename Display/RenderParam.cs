// Decompiled with JetBrains decompiler
// Type: AtopPlugin.Display.RenderParam
// Assembly: vatsys-atop-plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53F96C82-A187-47C4-B3F9-41A57159448E
// Assembly location: C:\Users\domyn\Downloads\vatsys-atop-plugin.dll

using SharpDX;
using vatsys;

#nullable disable
namespace AtopPlugin.Display
{
  public class RenderParam
  {
    public Coordinate ScreenCentre { get; }

    public double ViewScale { get; }

    public double Zoom { get; }

    public Size2 ClientSize { get; }

    public RectangleF ClipRectangle { get; }

    public float Rotation { get; }

    public bool RedrawBackground { get; }

    public bool UpdateColours { get; }

    public bool Flash { get; }

    public bool Timeshare { get; }

    public uint[] VisibleMaps { get; }

    public RenderParam(
      Coordinate screencentre,
      double viewscale,
      double zoom,
      Size2 clientsize,
      float rotation,
      bool redrawbackground,
      bool updatecolours,
      bool flash,
      bool timeshare,
      uint[] visibleMaps)
    {
      this.ScreenCentre = screencentre;
      this.ViewScale = viewscale;
      this.Zoom = zoom;
      this.ClientSize = clientsize;
      this.ClipRectangle = new RectangleF((float) (-clientsize.Width / 2 - 50), (float) (-clientsize.Height / 2 - 50), (float) (clientsize.Width + 100), (float) (clientsize.Height + 100));
      this.Rotation = rotation;
      this.RedrawBackground = redrawbackground;
      this.UpdateColours = updatecolours;
      this.Flash = flash;
      this.Timeshare = timeshare;
      this.VisibleMaps = visibleMaps;
    }
  }
}
