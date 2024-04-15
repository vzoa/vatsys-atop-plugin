using System;
using System.Collections.Generic;
using System.Linq;

namespace AtopPlugin.Display.Label;

public static class LabelItemRegistry
{
    private static readonly ILabelItem[] LabelItems = { new AltitudeLabelItem() };
    private static readonly Dictionary<string, ILabelItem> LabelItemsMap = BuildRegistry();

    public static ILabelItem? GetLabelItem(string key)
    {
        LabelItemsMap.TryGetValue(key, out var output);
        return output;
    }

    private static Dictionary<string, ILabelItem> BuildRegistry()
    {
        return LabelItems.ToDictionary(item => item.GetFieldKey(), item => item);
    }
}