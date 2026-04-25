using System;
using System.Reflection;
using vatsys;

namespace AtopPlugin.Helpers;

internal static class SelectedLabelBoxBridge
{
    private static bool _initialized;

    private static object? _alertsInstance;
    private static ConstructorInfo? _alertCtor;
    private static MethodInfo? _addAlertMethod;
    private static MethodInfo? _removeAlertMethod;

    private static object? _activeSyntheticAlert;

    private static bool _warningBrushColorCaptured;
    private static object? _originalWarningBrushColor;

    // Any non-EMG/RAD alert type will use warning box colour in ASDControlDX.
    private const AlertTypes SelectionBoxAlertType = AlertTypes.DUPE;
    private const AlertPriorities SelectionBoxPriority = AlertPriorities.P4;
    private const string SelectionBoxAlertName = "ATOP_SELECTED_LABEL_BOX";

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            ResolveReflectionTargets();
            MMI.SelectedTrackChanged += OnSelectedTrackChanged;
            RefreshSelectionAlert();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"SelectedLabelBoxBridge init: {ex.Message}", ex));
        }
    }

    public static void Shutdown()
    {
        try
        {
            MMI.SelectedTrackChanged -= OnSelectedTrackChanged;
            RemoveActiveSelectionAlert();
            RestoreWarningBrushColor();
        }
        catch
        {
            // best effort during teardown
        }
    }

    private static void OnSelectedTrackChanged(object? sender, EventArgs e)
    {
        RefreshSelectionAlert();
    }

    private static void ResolveReflectionTargets()
    {
        var vatsysAsm = typeof(FDP2).Assembly;

        var alertsType = vatsysAsm.GetType("vatsys.Alerts");
        var alertType = vatsysAsm.GetType("vatsys.Alert");

        if (alertsType == null || alertType == null)
            return;

        var instanceField = alertsType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        _alertsInstance = instanceField?.GetValue(null);

        _addAlertMethod = alertsType.GetMethod("AddAlert", BindingFlags.Public | BindingFlags.Instance);
        _removeAlertMethod = alertsType.GetMethod("RemoveAlert", BindingFlags.Public | BindingFlags.Instance);

        _alertCtor = alertType.GetConstructor(new[]
        {
            typeof(Track),
            typeof(AlertTypes),
            typeof(AlertPriorities),
            typeof(bool),
            typeof(string)
        });
    }

    private static void RefreshSelectionAlert()
    {
        RemoveActiveSelectionAlert();

        try
        {
            var selectedTrack = MMI.SelectedTrack;
            if (selectedTrack == null)
            {
                RestoreWarningBrushColor();
                MMI.RequestRedraw();
                return;
            }

            // Mirror vatSys renderer gate for label box drawing.
            if (selectedTrack.State is not (
                    MMI.HMIStates.Jurisdiction or
                    MMI.HMIStates.GhostJurisdiction or
                    MMI.HMIStates.HandoverIn or
                    MMI.HMIStates.HandoverOut))
            {
                RestoreWarningBrushColor();
                MMI.RequestRedraw();
                return;
            }

            if (_alertsInstance == null || _addAlertMethod == null || _alertCtor == null)
            {
                RestoreWarningBrushColor();
                MMI.RequestRedraw();
                return;
            }

            TryTintWarningBrushToSelectedTrack(selectedTrack);

            _activeSyntheticAlert = _alertCtor.Invoke(new object[]
            {
                selectedTrack,
                SelectionBoxAlertType,
                SelectionBoxPriority,
                false,
                SelectionBoxAlertName
            });

            _addAlertMethod.Invoke(_alertsInstance, new[] { _activeSyntheticAlert });
            MMI.RequestRedraw();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"SelectedLabelBoxBridge refresh: {ex.Message}", ex));
        }
    }

    private static void RemoveActiveSelectionAlert()
    {
        if (_activeSyntheticAlert == null || _alertsInstance == null || _removeAlertMethod == null)
            return;

        try
        {
            _removeAlertMethod.Invoke(_alertsInstance, new[] { _activeSyntheticAlert });
        }
        catch
        {
            // ignore stale alert instances
        }
        finally
        {
            _activeSyntheticAlert = null;
        }
    }

    private static void TryTintWarningBrushToSelectedTrack(Track selectedTrack)
    {
        try
        {
            var mmiType = typeof(MMI);
            var mainFormField = mmiType.GetField("MainForm", BindingFlags.NonPublic | BindingFlags.Static);
            var mainForm = mainFormField?.GetValue(null);
            if (mainForm == null) return;

            var asdField = mainForm.GetType().GetField("ASD", BindingFlags.NonPublic | BindingFlags.Instance);
            var asd = asdField?.GetValue(mainForm);
            if (asd == null) return;

            var warningBrushField = asd.GetType().GetField("warningBrush", BindingFlags.NonPublic | BindingFlags.Instance);
            var jurisBrushField = asd.GetType().GetField("jurisBrush", BindingFlags.NonPublic | BindingFlags.Instance);
            var ghostBrushField = asd.GetType().GetField("ghostJurisBrush", BindingFlags.NonPublic | BindingFlags.Instance);

            var warningBrush = warningBrushField?.GetValue(asd);
            var jurisBrush = jurisBrushField?.GetValue(asd);
            var ghostBrush = ghostBrushField?.GetValue(asd);
            if (warningBrush == null || jurisBrush == null || ghostBrush == null) return;

            var colorProp = warningBrush.GetType().GetProperty("Color");
            if (colorProp == null) return;

            if (!_warningBrushColorCaptured)
            {
                _originalWarningBrushColor = colorProp.GetValue(warningBrush);
                _warningBrushColorCaptured = true;
            }

            var sourceBrush = selectedTrack.State == MMI.HMIStates.GhostJurisdiction ? ghostBrush : jurisBrush;
            var sourceColorProp = sourceBrush.GetType().GetProperty("Color");
            if (sourceColorProp == null) return;

            var sourceColor = sourceColorProp.GetValue(sourceBrush);
            if (sourceColor == null) return;

            colorProp.SetValue(warningBrush, sourceColor);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"SelectedLabelBoxBridge tint: {ex.Message}", ex));
        }
    }

    private static void RestoreWarningBrushColor()
    {
        if (!_warningBrushColorCaptured || _originalWarningBrushColor == null) return;

        try
        {
            var mmiType = typeof(MMI);
            var mainFormField = mmiType.GetField("MainForm", BindingFlags.NonPublic | BindingFlags.Static);
            var mainForm = mainFormField?.GetValue(null);
            if (mainForm == null) return;

            var asdField = mainForm.GetType().GetField("ASD", BindingFlags.NonPublic | BindingFlags.Instance);
            var asd = asdField?.GetValue(mainForm);
            if (asd == null) return;

            var warningBrushField = asd.GetType().GetField("warningBrush", BindingFlags.NonPublic | BindingFlags.Instance);
            var warningBrush = warningBrushField?.GetValue(asd);
            if (warningBrush == null) return;

            var colorProp = warningBrush.GetType().GetProperty("Color");
            if (colorProp == null) return;

            colorProp.SetValue(warningBrush, _originalWarningBrushColor);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"SelectedLabelBoxBridge restore: {ex.Message}", ex));
        }
    }
}
