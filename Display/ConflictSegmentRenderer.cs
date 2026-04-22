using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using AtopPlugin.Conflict;
using AtopPlugin.Models;
using vatsys;

namespace AtopPlugin.Display;

/// <summary>
/// Renders conflict segments on the ASD using the native LATC system.
/// Each conflict is toggled individually via its Draw button.
/// </summary>
public static class ConflictSegmentRenderer
{
    private static readonly object _lock = new();
    private static bool _initialized = false;

    // Reflection cache
    private static Type _latcType;
    private static Type _segmentType;
    private static PropertyInfo _latcsProperty;
    private static FieldInfo _segmentsField;
    private static FieldInfo _segCallsignField;
    private static FieldInfo _segStartLatlongField;
    private static FieldInfo _segEndLatlongField;
    private static FieldInfo _segStartTimeField;
    private static FieldInfo _segEndTimeField;

    // Per-conflict tracking: key = conflict key, value = LATC object in MMI
    private static readonly Dictionary<string, object> _drawnConflicts = new();
    private static readonly Dictionary<string, ConflictStatus> _drawnConflictStatuses = new();

    // Global LATC colour override (ASD uses StaticTools brush for LATC rendering)
    private static readonly Color ActualConflictColor = Color.FromArgb(255, 0, 0);
    private static readonly Color NonActualConflictColor = Color.FromArgb(255, 165, 0);
    private static Color? _originalLatcColor;
    private static bool _latcColorOverrideActive;

    /// <summary>
    /// Returns a stable key for a conflict pair (order-independent).
    /// </summary>
    private static string GetConflictKey(ConflictData conflict)
    {
        var a = conflict.Intruder?.Callsign ?? "";
        var b = conflict.Active?.Callsign ?? "";
        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"{a}-{b}" : $"{b}-{a}";
    }

    /// <summary>
    /// Check if a specific conflict is currently drawn.
    /// </summary>
    public static bool IsDrawn(ConflictData conflict)
    {
        lock (_lock)
        {
            return _drawnConflicts.ContainsKey(GetConflictKey(conflict));
        }
    }

    /// <summary>
    /// Toggle drawing for a specific conflict. Returns true if now drawn, false if removed.
    /// </summary>
    public static bool ToggleConflict(ConflictData conflict)
    {
        if (!_initialized) Initialize();
        if (!_initialized) return false;

        var colourChanged = false;
        var drawn = false;

        lock (_lock)
        {
            var key = GetConflictKey(conflict);
            if (_drawnConflicts.ContainsKey(key))
            {
                RemoveLatcFromMmi(_drawnConflicts[key]);
                _drawnConflicts.Remove(key);
                _drawnConflictStatuses.Remove(key);
                colourChanged = ApplyLatcSeverityColorOverride();
                drawn = false;
            }
            else
            {
                var latc = CreateLatcForConflict(conflict);
                if (latc != null)
                {
                    AddLatcToMmi(latc);
                    _drawnConflicts[key] = latc;
                    _drawnConflictStatuses[key] = conflict.ConflictStatus;
                }

                colourChanged = ApplyLatcSeverityColorOverride();
                drawn = true;
            }
        }

        if (colourChanged)
            MMI.RequestRedraw(false, true);

        return drawn;
    }

    /// <summary>
    /// Remove drawing for a specific conflict if it is drawn.
    /// </summary>
    public static void RemoveConflict(ConflictData conflict)
    {
        if (!_initialized) return;

        var colourChanged = false;

        lock (_lock)
        {
            var key = GetConflictKey(conflict);
            if (_drawnConflicts.TryGetValue(key, out var latc))
            {
                RemoveLatcFromMmi(latc);
                _drawnConflicts.Remove(key);
                _drawnConflictStatuses.Remove(key);
                colourChanged = ApplyLatcSeverityColorOverride();
            }
        }

        if (colourChanged)
            MMI.RequestRedraw(false, true);
    }

    /// <summary>
    /// Initialize the renderer. Call this on plugin startup.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            var assembly = typeof(MMI).Assembly;
            _latcType = assembly.GetType("vatsys.LATC");
            if (_latcType == null) return;

            _segmentType = _latcType.GetNestedType("Segment");
            if (_segmentType == null) return;

            _latcsProperty = typeof(MMI).GetProperty("LATCs", BindingFlags.Static | BindingFlags.NonPublic);
            if (_latcsProperty == null) return;

            _segmentsField = _latcType.GetField("Segments");
            _segCallsignField = _segmentType.GetField("callsign");
            _segStartLatlongField = _segmentType.GetField("startLatlong");
            _segEndLatlongField = _segmentType.GetField("endLatlong");
            _segStartTimeField = _segmentType.GetField("startTime");
            _segEndTimeField = _segmentType.GetField("endTime");

            _initialized = true;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer init error: {ex.Message}"));
        }
    }

    private static object CreateLatcForConflict(ConflictData conflict)
    {
        if (_latcType == null || _segmentType == null) return null;

        try
        {
            var latc = Activator.CreateInstance(_latcType);
            if (latc == null) return null;

            var segments = _segmentsField?.GetValue(latc) as System.Collections.IList;
            if (segments == null) return null;

            // Intruder segment
            if (conflict.FirstConflictTime?.StartLatlong != null &&
                conflict.FirstConflictTime?.EndLatlong != null &&
                conflict.Intruder != null)
            {
                var seg = CreateSegment(
                    conflict.Intruder.Callsign,
                    conflict.FirstConflictTime.StartLatlong,
                    conflict.FirstConflictTime.EndLatlong,
                    conflict.FirstConflictTime.StartTime,
                    conflict.FirstConflictTime.EndTime);
                if (seg != null) segments.Add(seg);
            }

            // Active segment
            if (conflict.FirstConflictTime2?.StartLatlong != null &&
                conflict.FirstConflictTime2?.EndLatlong != null &&
                conflict.Active != null)
            {
                var seg = CreateSegment(
                    conflict.Active.Callsign,
                    conflict.FirstConflictTime2.StartLatlong,
                    conflict.FirstConflictTime2.EndLatlong,
                    conflict.FirstConflictTime2.StartTime,
                    conflict.FirstConflictTime2.EndTime);
                if (seg != null) segments.Add(seg);
            }

            return latc;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer CreateLatcForConflict: {ex.Message}"));
            return null;
        }
    }

    private static object CreateSegment(string callsign, Coordinate startLatlong, Coordinate endLatlong, DateTime startTime, DateTime endTime)
    {
        try
        {
            var segment = Activator.CreateInstance(_segmentType);
            if (segment == null) return null;

            _segCallsignField?.SetValue(segment, callsign);
            _segStartLatlongField?.SetValue(segment, startLatlong);
            _segEndLatlongField?.SetValue(segment, endLatlong);
            _segStartTimeField?.SetValue(segment, startTime);
            _segEndTimeField?.SetValue(segment, endTime);
            return segment;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer CreateSegment: {ex.Message}"));
            return null;
        }
    }

    private static void AddLatcToMmi(object latc)
    {
        if (_latcsProperty == null || _latcType == null) return;

        try
        {
            var currentLatcs = _latcsProperty.GetValue(null) as Array;
            var newLength = (currentLatcs?.Length ?? 0) + 1;
            var newLatcs = Array.CreateInstance(_latcType, newLength);

            if (currentLatcs != null)
            {
                for (int i = 0; i < currentLatcs.Length; i++)
                    newLatcs.SetValue(currentLatcs.GetValue(i), i);
            }

            newLatcs.SetValue(latc, newLength - 1);
            _latcsProperty.SetValue(null, newLatcs);
            MMI.RequestRedraw(false, false);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer AddLatcToMmi: {ex.Message}"));
        }
    }

    private static void RemoveLatcFromMmi(object latc)
    {
        if (_latcsProperty == null || _latcType == null) return;

        try
        {
            var currentLatcs = _latcsProperty.GetValue(null) as Array;
            if (currentLatcs == null) return;

            var remaining = currentLatcs.Cast<object>().Where(l => !ReferenceEquals(l, latc)).ToArray();
            var typedArray = Array.CreateInstance(_latcType, remaining.Length);
            for (int i = 0; i < remaining.Length; i++)
                typedArray.SetValue(remaining[i], i);

            _latcsProperty.SetValue(null, typedArray);
            MMI.RequestRedraw(false, false);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer RemoveLatcFromMmi: {ex.Message}"));
        }
    }

    /// <summary>
    /// Clear all drawn conflict LATCs.
    /// </summary>
    public static void ClearAll()
    {
        var colourChanged = false;

        lock (_lock)
        {
            foreach (var latc in _drawnConflicts.Values)
                RemoveLatcFromMmi(latc);
            _drawnConflicts.Clear();
            _drawnConflictStatuses.Clear();
            colourChanged = ApplyLatcSeverityColorOverride();
        }

        if (colourChanged)
            MMI.RequestRedraw(false, true);
    }

    public static void Dispose()
    {
        ClearAll();
        _initialized = false;
    }

    private static bool ApplyLatcSeverityColorOverride()
    {
        if (_drawnConflictStatuses.Count == 0)
        {
            if (!_latcColorOverrideActive || !_originalLatcColor.HasValue)
                return false;

            var restored = TrySetColourIdentity(Colours.Identities.StaticTools, _originalLatcColor.Value, captureOriginal: false);
            if (restored)
                _latcColorOverrideActive = false;
            return restored;
        }

        var hasActual = _drawnConflictStatuses.Values.Any(s => s == ConflictStatus.Actual);
        var targetColor = hasActual ? ActualConflictColor : NonActualConflictColor;

        var changed = TrySetColourIdentity(Colours.Identities.StaticTools, targetColor, captureOriginal: true);
        if (changed)
            _latcColorOverrideActive = true;
        return changed;
    }

    private static bool TrySetColourIdentity(Colours.Identities identity, Color color, bool captureOriginal)
    {
        try
        {
            var coloursType = typeof(Colours);
            var allColoursField = coloursType.GetField("allColours", BindingFlags.NonPublic | BindingFlags.Static);
            var updateColourMethod = coloursType.GetMethod("UpdateColour", BindingFlags.NonPublic | BindingFlags.Static);

            if (allColoursField?.GetValue(null) is not System.Collections.IDictionary allColours || updateColourMethod == null)
                return false;

            if (captureOriginal && !_originalLatcColor.HasValue && allColours.Contains(identity))
            {
                if (allColours[identity] is Color original)
                    _originalLatcColor = original;
            }

            allColours[identity] = color;
            updateColourMethod.Invoke(null, new object[] { identity, Colours.ToolBrightness });
            return true;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer.TrySetColourIdentity: {ex.Message}"));
            return false;
        }
    }
}
