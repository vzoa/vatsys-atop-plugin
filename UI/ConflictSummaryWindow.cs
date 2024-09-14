using System.Windows.Forms;
using System;
using System.Collections.Generic;
using AtopPlugin.Conflict;
using vatsys;
using System.Drawing;
using System.Threading.Tasks;
using static vatsys.FDP2;
using AtopPlugin.State;
using System.Linq;
using static vatsys.CPDLC;
using AtopPlugin.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static vatsys.FDP2.FDR.ExtractedRoute;
using System.Threading;
using static AtopPlugin.Conflict.ConflictProbe;

namespace AtopPlugin.UI
{
    public partial class ConflictSummaryWindow : BaseForm
    {
        private SynchronizationContext uiContext;

        private ConflictSummaryWindow(SynchronizationContext context)
        {
            uiContext = context ?? throw new ArgumentNullException(nameof(context));
            InitializeComponent();
            ConflictsUpdated += UpdateConflicts;
            conflictListView.MouseClick += ConflictListView_MouseClick; // Subscribe to the event only once
        }

        public ConflictSummaryWindow()
        {

        }

        private void UpdateConflicts(object sender, EventArgs e)
        {
            _ = DisplayConflictsAsync();
        }

        private async void ConflictSummaryWindow_Load(object sender, EventArgs e)
        {
            await DisplayConflictsAsync();
        }

        private async Task DisplayConflictsAsync()
        {
            conflictListView.View = View.Details;
            conflictListView.HeaderStyle = ColumnHeaderStyle.None;

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

            conflictListView.Items.Clear();

            // Fetch and process conflicts in the background
            var conflictDatas = await Task.Run(() => ConflictDatas.OrderBy(t => t.EarliestLos).ToList());

            // Batch updates to minimize UI refreshes
            var listViewItems = new List<ListViewItem>();
            foreach (ConflictData conflict in conflictDatas)
            {
                AtopAircraftState intruderState = conflict.Intruder?.GetAtopState();
                AtopAircraftState activeState = conflict.Active.GetAtopState();

                if (intruderState != null && activeState != null)
                {
                    AtopAircraftDisplayState intAtt = new AtopAircraftDisplayState(intruderState);
                    AtopAircraftDisplayState actAtt = new AtopAircraftDisplayState(activeState);

                    ListViewItem item = new ListViewItem(conflict.Intruder?.Callsign.PadRight(7));
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

            // Add all items at once to avoid repetitive updates
            conflictListView.Items.AddRange(listViewItems.ToArray());

            // Perform UI updates only after all data is processed
            conflictListView.Refresh();

            // Avoid multiple distinct checks inside the loop
            this.Visible = conflictDatas.Any();
        }

        private void ConflictListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && conflictListView.SelectedItems.Count > 0)
            {
                ListViewItem item = conflictListView.SelectedItems[0];
                ConflictData conflictData = (ConflictData)item.Tag;
                DoShowreport(conflictData);
            }
        }

        private static void DoShowreport(ConflictData conflict)
        {
            ConflictReportWindow report = new ConflictReportWindow(conflict);
            report.Show(Form.ActiveForm);
        }
    }
}