using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using AtopPlugin.Helpers;
using vatsys;

namespace AtopPlugin.Display;

/// <summary>
/// Plugin-owned controller for temporary probe routes.
/// Uses native Graphic Route rendering with labels hidden.
/// </summary>
public static class ProbeRouteRenderer
{
    private static readonly object LockObject = new();
    private static readonly Dictionary<string, DateTimeOffset> ActiveRoutes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Color ProbeRouteColor = Color.FromArgb(0, 255, 0);
    private static Color? OriginalRouteColor;
    private static bool RouteColorOverrideActive;
    private static bool RouteColorOverrideFailed;

    public static void ShowForTrack(Track? track)
    {
        if (track == null) return;

        var callsign = track.GetFDR()?.Callsign;
        if (string.IsNullOrWhiteSpace(callsign)) return;

        var colorChanged = false;

        lock (LockObject)
        {
            colorChanged = EnsureProbeRouteColor();
            MMI.ShowGraphicRoute(track, labels: false);
            ActiveRoutes[callsign] = DateTimeOffset.UtcNow;
        }

        MMI.RequestRedraw(false, colorChanged);
    }

    public static void HideForTrack(Track? track)
    {
        if (track == null) return;

        HideForCallsign(track.GetFDR()?.Callsign);
    }

    public static void HideForCallsign(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;

        var colorChanged = false;

        lock (LockObject)
        {
            var fdr = FDP2.GetFDRs.FirstOrDefault(f =>
                string.Equals(f.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
            var track = fdr != null ? MMI.FindTrack(fdr) : null;

            if (track != null)
                MMI.HideGraphicRoute(track);

            ActiveRoutes.Remove(callsign);
            colorChanged = RestoreOriginalRouteColorIfUnused();
        }

        MMI.RequestRedraw(false, colorChanged);
    }

    public static void ClearAll()
    {
        List<string> callsigns;
        lock (LockObject)
        {
            callsigns = ActiveRoutes.Keys.ToList();
        }

        foreach (var callsign in callsigns)
            HideForCallsign(callsign);
    }

    public static void EvaluateCpdlcReadback(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;

        DateTimeOffset sinceUtc;
        lock (LockObject)
        {
            if (!ActiveRoutes.TryGetValue(callsign, out sinceUtc))
                return;
        }

        if (CpdlcPluginBridge.HasWilcoReadbackSince(callsign, sinceUtc))
            HideForCallsign(callsign);
    }

    private static bool EnsureProbeRouteColor()
    {
        if (RouteColorOverrideActive || RouteColorOverrideFailed)
            return false;

        if (!TrySetRouteColor(ProbeRouteColor, captureOriginal: true))
        {
            RouteColorOverrideFailed = true;
            return false;
        }

        RouteColorOverrideActive = true;
        return true;
    }

    private static bool RestoreOriginalRouteColorIfUnused()
    {
        if (!RouteColorOverrideActive || ActiveRoutes.Count > 0 || !OriginalRouteColor.HasValue)
            return false;

        if (!TrySetRouteColor(OriginalRouteColor.Value, captureOriginal: false))
            return false;

        RouteColorOverrideActive = false;
        return true;
    }

    private static bool TrySetRouteColor(Color color, bool captureOriginal)
    {
        try
        {
            var coloursType = typeof(Colours);
            var allColoursField = coloursType.GetField("allColours", BindingFlags.NonPublic | BindingFlags.Static);
            var updateColourMethod = coloursType.GetMethod("UpdateColour", BindingFlags.NonPublic | BindingFlags.Static);

            if (allColoursField?.GetValue(null) is not IDictionary allColours || updateColourMethod == null)
                return false;

            var routeIdentity = Colours.Identities.Route;

            if (captureOriginal && !OriginalRouteColor.HasValue && allColours.Contains(routeIdentity))
            {
                if (allColours[routeIdentity] is Color originalColor)
                    OriginalRouteColor = originalColor;
            }

            allColours[routeIdentity] = color;
            updateColourMethod.Invoke(null, new object[] { routeIdentity, Colours.ToolBrightness });
            return true;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ProbeRouteRenderer.TrySetRouteColor: {ex.Message}", ex));
            return false;
        }
    }
}