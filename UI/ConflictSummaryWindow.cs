using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        ConflictsUpdated += ConflictSummaryWindow_Load;
        conflictListView.MouseClick += ConflictListView_MouseClick; // Subscribe to the event only once\
        conflictListView.View = View.Details;
        conflictListView.HeaderStyle = ColumnHeaderStyle.None;
    }

    private async void ConflictSummaryWindow_Load(object sender, EventArgs e)
    {
         await DisplayConflictsAsync();
    }

    private async Task DisplayConflictsAsync()
    {
        // Only clear and add columns if necessary
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

        // Fetch and process conflicts in the background
        var conflictDatas = await Task.Run(() => ConflictDatas.OrderBy(t => t.EarliestLos).ToList());

        // Batch updates to minimize UI refreshes
        var listViewItems = new List<ListViewItem>();
        foreach (var conflict in conflictDatas)
        {
            var intruderState = conflict.Intruder?.GetAtopState();
            var activeState = conflict.Active.GetAtopState();

            if (intruderState != null && activeState != null)
            {
                var intAtt = new AtopAircraftDisplayState(intruderState);
                var actAtt = new AtopAircraftDisplayState(activeState);

                var item = new ListViewItem(conflict.Intruder?.Callsign.PadRight(7));
                item.SubItems.Add(intAtt.ConflictAttitudeFlag?.Value.ToString().PadRight(1) ?? "");
                item.SubItems.Add(conflict.Active.Callsign.PadRight(7));
                item.SubItems.Add(actAtt.ConflictAttitudeFlag?.Value.ToString().PadRight(6) ?? "");
                item.SubItems.Add(" ").ToString().PadRight(2);
                item.SubItems.Add(AtopAircraftDisplayState.GetConflictSymbol(conflict).PadRight(2));
                item.SubItems.Add(conflict.EarliestLos.ToString("HHmm").PadRight(4));
                item.SubItems.Add(conflict.ConflictEnd.ToString("HHmm").PadRight(4));
                item.Tag = conflict;

                listViewItems.Add(item);
            }
        }

        // Check if the control is ready to be invoked
        if (IsDisposed || !IsHandleCreated || conflictListView.IsDisposed || !conflictListView.IsHandleCreated)
            return;

        conflictListView.Invoke(new MethodInvoker(() =>
        {
            if (conflictListView.IsDisposed) return;

            conflictListView.Items.Clear();

            // Add all items at once to avoid repetitive updates
            conflictListView.Items.AddRange(listViewItems.ToArray());

            // Perform UI updates only after all data is processed
            conflictListView.Refresh();
        }));

        // msalikhov(2024-10-13): disabling automatically showing the window until we fix the rendering
        if (IsDisposed || !IsHandleCreated)
            return;

        Invoke(new MethodInvoker(() =>
        {
            if (IsDisposed) return;
            // Avoid multiple distinct checks inside the loop
            Visible = conflictDatas.Any();
        }));
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