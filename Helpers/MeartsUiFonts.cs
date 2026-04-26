using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AtopPlugin.Helpers;

public static class MeartsUiFonts
{
    private const string RegularFontResource = "AtopPlugin.Fonts.lma11M15.otb";
    private const string BoldFontResource = "AtopPlugin.Fonts.lma11B15.otb";

    private static readonly Lazy<PrivateFontCollection> RegularFonts = new(() => LoadFontCollection(RegularFontResource));
    private static readonly Lazy<PrivateFontCollection> BoldFonts = new(() => LoadFontCollection(BoldFontResource));
    private static readonly ConcurrentDictionary<string, Font> FontCache = new(StringComparer.Ordinal);

    public static Font GetFont(float size, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        var key = $"{size:0.###}|{(int)style}|{(int)unit}";
        return FontCache.GetOrAdd(key, _ => CreateFont(size, style, unit));
    }

    public static Font CreateFont(Font source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return GetFont(source.Size, source.Style, source.Unit);
    }

    public static void Apply(Control root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        ApplyToControl(root);
        foreach (Control child in root.Controls)
            Apply(child);

        if (root.ContextMenuStrip != null)
            Apply(root.ContextMenuStrip.Items);
    }

    public static void Apply(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.Font = CreateFont(item.Font);
            if (item is ToolStripDropDownItem dropDownItem)
                Apply(dropDownItem.DropDownItems);
        }
    }

    private static void ApplyToControl(Control control)
    {
        control.Font = CreateFont(control.Font);

        var subFontProperty = control.GetType().GetProperty("SubFont", typeof(Font));
        if (subFontProperty?.CanWrite == true && subFontProperty.GetValue(control) is Font subFont)
            subFontProperty.SetValue(control, CreateFont(subFont));
    }

    private static Font CreateFont(float size, FontStyle style, GraphicsUnit unit)
    {
        var family = (style & FontStyle.Bold) == FontStyle.Bold
            ? GetPrimaryFamily(BoldFonts.Value)
            : GetPrimaryFamily(RegularFonts.Value);

        var renderedStyle = style & (FontStyle.Underline | FontStyle.Strikeout);
        return new Font(family, size, renderedStyle, unit);
    }

    private static FontFamily GetPrimaryFamily(PrivateFontCollection collection)
    {
        if (collection.Families.Length == 0)
            throw new InvalidOperationException("No MEARTS font families were loaded.");

        return collection.Families[0];
    }

    private static PrivateFontCollection LoadFontCollection(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font resource '{resourceName}' was not found.");

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();

        var collection = new PrivateFontCollection();
        var handle = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, handle, bytes.Length);
            collection.AddMemoryFont(handle, bytes.Length);
        }
        finally
        {
            Marshal.FreeCoTaskMem(handle);
        }

        return collection;
    }
}