using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AtopPlugin.Conflict;
using AtopPlugin.State;
using vatsys;
using static AtopPlugin.Conflict.ConflictProbe;

namespace AtopPlugin.UI;

public partial class ConflictSummaryWindow : BaseForm
{
    public ConflictSummaryWindow()
    {
        InitializeComponent();
        ConflictsUpdated += OnConflictsUpdated;
        conflictListView.MouseClick += ConflictListView_MouseClick;
        conflictListView.View = View.Details;
        conflictListView.HeaderStyle = ColumnHeaderStyle.None;
    }

    private void ConflictSummaryWindow_Load(object sender, EventArgs e)
    {
        try
        {
            DisplayConflicts();
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSummaryWindow_Load: {ex.Message}", ex));
        }
    }

    private void OnConflictsUpdated(object sender, EventArgs e)
    {
        try
        {
            MMI.InvokeOnGUI(DisplayConflicts);
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSummaryWindow.OnConflictsUpdated: {ex.Message}", ex));
        }
    }

    private void DisplayConflicts()
    {
        try
        {
            if (IsDisposed) return;

            // Only add columns once
            if (conflictListView.Columns.Count == 0)
            {
                conflictListView.Columns.Add("Intruder Callsign", 80, HorizontalAlignment.Left);
                conflictListView.Columns.Add("Intruder Attitude", 25, HorizontalAlignment.Left);
                conflictListView.Columns.Add("Active Callsign", 70, HorizontalAlignment.Left);
                conflictListView.Columns.Add("Active Attitude", 20, HorizontalAlignment.Left);
                conflictListView.Columns.Add("Conflict Override", 50, HorizontalAlignment.Left);
                conflictListView.Columns.Add("Conflict Symbol", 50, HorizontalAlignment.Left);
                conflictListView.Columns.Add("Earliest LOS", 80, HorizontalAlignment.Left);
                conflictListView.Columns.Add("Conflict End", 80, HorizontalAlignment.Left);
            }

            var conflictDatas = ConflictDatas?.OrderBy(t => t.EarliestLos).ToList() ?? new List<ConflictData>();

            conflictListView.BeginUpdate();
            conflictListView.Items.Clear();

            foreach (var conflict in conflictDatas)
            {
                var intruderState = conflict.Intruder?.GetAtopState();
                var activeState = conflict.Active?.GetAtopState();

                if (intruderState != null && activeState != null)
                {
                    var intAtt = new AtopAircraftDisplayState(intruderState);
                    var actAtt = new AtopAircraftDisplayState(activeState);

                    var item = new ListViewItem(conflict.Intruder?.Callsign?.PadRight(7) ?? "");
                    item.SubItems.Add(intAtt.ConflictAttitudeFlag?.Value.ToString().PadRight(1) ?? "");
                    item.SubItems.Add(conflict.Active.Callsign?.PadRight(7) ?? "");
                    item.SubItems.Add(actAtt.ConflictAttitudeFlag?.Value.ToString().PadRight(6) ?? "");
                    item.SubItems.Add("");
                    item.SubItems.Add(AtopAircraftDisplayState.GetConflictSymbol(conflict).PadRight(2));
                    item.SubItems.Add(conflict.EarliestLos.ToString("HHmm"));
                    item.SubItems.Add(conflict.ConflictEnd.ToString("HHmm"));
                    item.Tag = conflict;

                    conflictListView.Items.Add(item);
                }
            }

            conflictListView.EndUpdate();

            // Auto-show when there are conflicts, auto-hide when there aren't
            if (conflictDatas.Count > 0)
            {
                if (!Visible) Show();
            }
            else
            {
                if (Visible) Hide();
            }
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"ConflictSummaryWindow.DisplayConflicts: {ex.Message}", ex));
        }
    }

    private void ConflictListView_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && conflictListView.SelectedItems.Count > 0)
        {
            var item = conflictListView.SelectedItems[0];
            var conflictData = (ConflictData)item.Tag;
            DoShowreport(conflictData);
        }
    }

    private static void DoShowreport(ConflictData conflict)
    {
        var report = new ConflictReportWindow(conflict);
        report.Show(ActiveForm);
    }
}