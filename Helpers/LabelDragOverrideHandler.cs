using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using vatsys;

namespace AtopPlugin.Helpers;

/// <summary>
/// Overrides the native label drag behavior to allow unclamped leader line lengths.
/// vatSys natively clamps ASDLabelLeaderLength to 0-2 (short/medium/long).
/// This handler recalculates the length on label placement without the clamp,
/// so labels can be dragged to any distance.
/// </summary>
public static class LabelDragOverrideHandler
{
    private static Type _asdControlType;
    private static FieldInfo _labelMovingTrackField;
    private static MethodInfo _getLocationXYMethod;
    private static FieldInfo _interactiveFunctionField;
    private static Track _activeMovingTrack;
    private static readonly HashSet<int> _attachedControlHashes = new();

    public static void Initialize()
    {
        try
        {
            _asdControlType = typeof(MMI).Assembly.GetType("vatsys.ASDControlDX");
            if (_asdControlType == null) return;

            _labelMovingTrackField = _asdControlType.GetField("LabelMovingTrack",
                BindingFlags.NonPublic | BindingFlags.Instance);

            _getLocationXYMethod = typeof(Track).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetLocationXY" && m.GetParameters().Length == 1);

            _interactiveFunctionField = typeof(MMI).GetField("ASD_INTERACTIVE_FUNCTION_ACTIVE",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);

            if (_labelMovingTrackField == null || _getLocationXYMethod == null || _interactiveFunctionField == null) return;

            var scanTimer = new Timer { Interval = 3000 };
            scanTimer.Tick += (_, _) => ScanForASDControls();
            scanTimer.Start();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"LabelDragOverrideHandler.Initialize: {ex.Message}", ex));
        }
    }

    private static void ScanForASDControls()
    {
        try
        {
            foreach (Form form in Application.OpenForms)
            {
                FindAndAttach(form);
            }
        }
        catch { /* Forms collection can change during iteration */ }
    }

    private static void FindAndAttach(Control parent)
    {
        if (_asdControlType.IsInstanceOfType(parent))
        {
            int hash = parent.GetHashCode();
            if (!_attachedControlHashes.Contains(hash))
            {
                parent.MouseMove += OnMouseMove;
                parent.MouseDown += OnMouseDown;
                parent.Disposed += (_, _) => _attachedControlHashes.Remove(hash);
                _attachedControlHashes.Add(hash);
            }
        }

        foreach (Control child in parent.Controls)
        {
            FindAndAttach(child);
        }
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            if (!IsInteractiveFunctionActive()) return;

            var movingTrack = _labelMovingTrackField.GetValue(sender) as Track;
            if (movingTrack != null)
                _activeMovingTrack = movingTrack;
        }
        catch { }
    }

    private static void OnMouseDown(object sender, MouseEventArgs e)
    {
        try
        {
            // Label placement is committed on right-click while dragging.
            // By the time our handler fires, the native ASD_MouseDown has already:
            //   1. Called GetLabelOrientation (which clamps length to max 2)
            //   2. Set track.ASDLabelLeaderLength to the clamped value
            //   3. Set LabelMovingTrack = null
            // We use _activeMovingTrack (captured during MouseMove) to recalculate without clamping.
            if (e.Button != MouseButtons.Right) return;

            var track = _activeMovingTrack;
            _activeMovingTrack = null;
            if (track == null) return;

            // Confirm the native handler completed (LabelMovingTrack is now null)
            var currentMoving = _labelMovingTrackField.GetValue(sender) as Track;
            if (currentMoving != null) return; // Still in progress, don't interfere

            var control = (Control)sender;

            // Get track screen position via reflection (internal method, center-origin coords)
            var trackPosCenter = (Point)_getLocationXYMethod.Invoke(track, new object[] { sender });
            float trackX = trackPosCenter.X + control.ClientSize.Width / 2f;
            float trackY = trackPosCenter.Y + control.ClientSize.Height / 2f;

            // Mouse position in client coordinates (top-left origin)
            float mouseX = e.X;
            float mouseY = e.Y;

            // Calculate unclamped length (same formula as GetLabelOrientation, minus the clamp)
            double dy = mouseY - trackY;
            double dx = mouseX - trackX;
            double dist = Math.Sqrt(dy * dy + dx * dx) - 5.0;
            int unclampedLength = Math.Max(0, (int)Math.Round(dist / 35.0));

            // Override the clamped value. Direction is already correct (native doesn't clamp it).
            track.ASDLabelLeaderLength = unclampedLength;
        }
        catch { }
    }

    private static bool IsInteractiveFunctionActive()
    {
        try
        {
            return (bool)(_interactiveFunctionField?.GetValue(null) ?? false);
        }
        catch { return false; }
    }
}
