using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AtopPlugin.Conflict;
using AtopPlugin.Models;
using vatsys;

namespace AtopPlugin.Display;

/// <summary>
/// Renders conflict segments on the ASD radar display using the native LATC system.
/// Uses reflection to access the internal LATC class and add segments to MMI.LATCs.
/// </summary>
public static class ConflictSegmentRenderer
{
    private static bool _enabled = false;
    private static readonly object _lock = new();
    private static bool _initialized = false;
    
    // Reflection cache for LATC
    private static Type? _latcType;
    private static Type? _segmentType;
    private static PropertyInfo? _latcsProperty;
    private static FieldInfo? _segmentsField;
    private static FieldInfo? _segCallsignField;
    private static FieldInfo? _segStartLatlongField;
    private static FieldInfo? _segEndLatlongField;
    private static FieldInfo? _segStartTimeField;
    private static FieldInfo? _segEndTimeField;
    
    // Track our LATCs so we can remove them
    private static readonly List<object> _ourLatcs = new();
    
    public static bool Enabled
    {
        get => _enabled;
        set
        {
            lock (_lock)
            {
                _enabled = value;
                if (!value)
                {
                    ClearConflictLines();
                }
                else
                {
                    UpdateConflictLines();
                }
            }
        }
    }
    
    /// <summary>
    /// Toggle rendering on/off
    /// </summary>
    public static void Toggle()
    {
        Enabled = !Enabled;
    }
    
    /// <summary>
    /// Initialize the renderer. Call this on plugin startup.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            // Get the internal LATC type via reflection
            var assembly = typeof(MMI).Assembly;
            _latcType = assembly.GetType("vatsys.LATC");
            
            if (_latcType == null)
            {
                Errors.Add(new Exception("ConflictSegmentRenderer: Could not find LATC type"));
                return;
            }
            
            // Get the nested Segment type
            _segmentType = _latcType.GetNestedType("Segment");
            if (_segmentType == null)
            {
                Errors.Add(new Exception("ConflictSegmentRenderer: Could not find LATC.Segment type"));
                return;
            }
            
            // Get the LATCs property from MMI
            _latcsProperty = typeof(MMI).GetProperty("LATCs", BindingFlags.Static | BindingFlags.NonPublic);
            if (_latcsProperty == null)
            {
                Errors.Add(new Exception("ConflictSegmentRenderer: Could not find MMI.LATCs property"));
                return;
            }
            
            // Get fields from LATC class
            _segmentsField = _latcType.GetField("Segments");
            
            // Get fields from Segment class
            _segCallsignField = _segmentType.GetField("callsign");
            _segStartLatlongField = _segmentType.GetField("startLatlong");
            _segEndLatlongField = _segmentType.GetField("endLatlong");
            _segStartTimeField = _segmentType.GetField("startTime");
            _segEndTimeField = _segmentType.GetField("endTime");
            
            _initialized = true;
            
            // Subscribe to conflict updates
            ConflictProbe.ConflictsUpdated += OnConflictsUpdated;
            
            Errors.Add(new Exception("ConflictSegmentRenderer: Initialized successfully"));
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer init error: {ex.Message}"));
        }
    }
    
    private static void OnConflictsUpdated(object? sender, EventArgs e)
    {
        if (Enabled)
        {
            UpdateConflictLines();
        }
    }
    
    /// <summary>
    /// Update the conflict lines on the display
    /// </summary>
    public static void UpdateConflictLines()
    {
        if (!_initialized) Initialize();
        if (!_initialized) return;
        
        try
        {
            // Clear existing conflict lines
            ClearConflictLines();
            
            var conflicts = ConflictProbe.ConflictDatas?.ToList();
            if (conflicts == null || conflicts.Count == 0)
            {
                return;
            }
            
            Errors.Add(new Exception($"ConflictSegmentRenderer: Rendering {conflicts.Count} conflicts"));
            
            foreach (var conflict in conflicts)
            {
                AddConflictAsLatc(conflict);
            }
            
            Errors.Add(new Exception($"ConflictSegmentRenderer: Added {_ourLatcs.Count} LATCs"));
            
            // Request redraw
            MMI.RequestRedraw(false, true);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer update error: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// Determine the worst (most severe) conflict status from a list of conflicts
    /// </summary>
    private static ConflictStatus DetermineWorstConflictStatus(List<ConflictData> conflicts)
    {
        if (conflicts.Any(c => c.ConflictStatus == ConflictStatus.Actual))
            return ConflictStatus.Actual;
        if (conflicts.Any(c => c.ConflictStatus == ConflictStatus.Imminent))
            return ConflictStatus.Imminent;
        return ConflictStatus.Advisory;
    }
    
    /// <summary>
    /// Clear all our conflict LATCs from the display
    /// </summary>
    public static void ClearConflictLines()
    {
        try
        {
            if (_latcsProperty == null || _ourLatcs.Count == 0) return;
            
            // Get current LATCs array
            var currentLatcs = _latcsProperty.GetValue(null) as Array;
            if (currentLatcs == null)
            {
                _ourLatcs.Clear();
                return;
            }
            
            // Filter out our LATCs
            var remainingLatcs = currentLatcs.Cast<object>()
                .Where(latc => !_ourLatcs.Contains(latc))
                .ToArray();
            
            // Create typed array
            var typedArray = Array.CreateInstance(_latcType!, remainingLatcs.Length);
            for (int i = 0; i < remainingLatcs.Length; i++)
            {
                typedArray.SetValue(remainingLatcs[i], i);
            }
            
            // Set back to MMI
            _latcsProperty.SetValue(null, typedArray);
            _ourLatcs.Clear();
            
            // Request redraw
            MMI.RequestRedraw(false, true);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer clear error: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// Add a conflict as a native LATC
    /// </summary>
    private static void AddConflictAsLatc(ConflictData conflict)
    {
        if (conflict == null || _latcType == null || _segmentType == null) return;
        
        try
        {
            // Create a new LATC instance using parameterless constructor
            var latc = Activator.CreateInstance(_latcType);
            if (latc == null) return;
            
            // Get the Segments list
            var segments = _segmentsField?.GetValue(latc) as System.Collections.IList;
            if (segments == null) return;
            
            // Add first aircraft's conflict segment (Intruder)
            if (conflict.FirstConflictTime?.StartLatlong != null && 
                conflict.FirstConflictTime?.EndLatlong != null &&
                conflict.Intruder != null)
            {
                var segment = CreateSegment(
                    conflict.Intruder.Callsign,
                    conflict.FirstConflictTime.StartLatlong,
                    conflict.FirstConflictTime.EndLatlong,
                    conflict.FirstConflictTime.StartTime,
                    conflict.FirstConflictTime.EndTime);
                    
                if (segment != null)
                    segments.Add(segment);
            }
            
            // Add second aircraft's conflict segment (Active)
            if (conflict.FirstConflictTime2?.StartLatlong != null && 
                conflict.FirstConflictTime2?.EndLatlong != null &&
                conflict.Active != null)
            {
                var segment = CreateSegment(
                    conflict.Active.Callsign,
                    conflict.FirstConflictTime2.StartLatlong,
                    conflict.FirstConflictTime2.EndLatlong,
                    conflict.FirstConflictTime2.StartTime,
                    conflict.FirstConflictTime2.EndTime);
                    
                if (segment != null)
                    segments.Add(segment);
            }
            
            // Add to MMI.LATCs
            AddLatcToMmi(latc);
            _ourLatcs.Add(latc);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer AddConflictAsLatc error: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// Create a LATC.Segment instance
    /// </summary>
    private static object? CreateSegment(string callsign, Coordinate startLatlong, Coordinate endLatlong, DateTime startTime, DateTime endTime)
    {
        if (_segmentType == null) return null;
        
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
            Errors.Add(new Exception($"ConflictSegmentRenderer CreateSegment error: {ex.Message}"));
            return null;
        }
    }
    
    /// <summary>
    /// Add a LATC to MMI.LATCs array
    /// </summary>
    private static void AddLatcToMmi(object latc)
    {
        if (_latcsProperty == null || _latcType == null) return;
        
        try
        {
            // Get current LATCs array
            var currentLatcs = _latcsProperty.GetValue(null) as Array;
            
            // Create new array with our LATC added
            var newLength = (currentLatcs?.Length ?? 0) + 1;
            var newLatcs = Array.CreateInstance(_latcType, newLength);
            
            // Copy existing
            if (currentLatcs != null)
            {
                for (int i = 0; i < currentLatcs.Length; i++)
                {
                    newLatcs.SetValue(currentLatcs.GetValue(i), i);
                }
            }
            
            // Add new
            newLatcs.SetValue(latc, newLength - 1);
            
            // Set back to MMI
            _latcsProperty.SetValue(null, newLatcs);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSegmentRenderer AddLatcToMmi error: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// Cleanup - call on plugin unload
    /// </summary>
    public static void Dispose()
    {
        ConflictProbe.ConflictsUpdated -= OnConflictsUpdated;
        ClearConflictLines();
        _initialized = false;
    }
}
