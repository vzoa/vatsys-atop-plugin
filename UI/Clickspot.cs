// Decompiled with JetBrains decompiler
// Type: vatsys.Clickspot
// Assembly: vatSys, Version=0.4.8114.34539, Culture=neutral, PublicKeyToken=null
// MVID: E82FB2F8-DAB0-42FD-91AA-1C44F8E62564
// Assembly location: E:\vatsys\bin\vatSys.exe
// XML documentation location: E:\vatsys\bin\vatSys.xml

using SharpDX;
using vatsys.Plugin;

namespace vatsys
{
  internal class Clickspot
  {
    public const int CLICKSPOT_BRL = 0;
    public const int CLICKSPOT_TRACK = 1;
    public const int CLICKSPOT_LABEL_CPROMPT = 2;
    public const int MOUSEBUTTON_LEFT = 0;
    public const int MOUSEBUTTON_MIDDLE = 1;
    public const int MOUSEBUTTON_RIGHT = 2;
    public RectangleF Area;
    public MMI.ClickspotTypes TypeMouseLeft;
    public MMI.ClickspotTypes TypeMouseMiddle;
    public MMI.ClickspotTypes TypeMouseRight;
    public object Id;
    public object Value;
    public CustomLabelItem CustomLabelItem;
    public CustomStripItem CustomStripItem;
    public MMI.ClickspotCategories Category = MMI.ClickspotCategories.Other;

    public Clickspot()
    {
    }

    public Clickspot(
      Rectangle area,
      MMI.ClickspotTypes mouseLeft,
      MMI.ClickspotTypes mouseMiddle,
      MMI.ClickspotTypes mouseRight,
      object _id,
      object value,
      MMI.ClickspotCategories cat)
    {
      this.Area = new RectangleF((float) area.X, (float) area.Y, (float) area.Width, (float) area.Height);
      this.TypeMouseLeft = mouseLeft;
      this.TypeMouseMiddle = mouseMiddle;
      this.TypeMouseRight = mouseRight;
      this.Id = _id;
      this.Value = value;
      this.Category = cat;
    }

    public Clickspot(
      RectangleF area,
      MMI.ClickspotTypes mouseLeft,
      MMI.ClickspotTypes mouseMiddle,
      MMI.ClickspotTypes mouseRight,
      object _id,
      object value,
      MMI.ClickspotCategories cat)
    {
      this.Area = area;
      this.TypeMouseLeft = mouseLeft;
      this.TypeMouseMiddle = mouseMiddle;
      this.TypeMouseRight = mouseRight;
      this.Id = _id;
      this.Value = value;
      this.Category = cat;
    }
  }
}
