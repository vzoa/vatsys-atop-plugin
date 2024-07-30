using System;
using System.Collections.Generic;
using System.Windows.Forms;
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
        private static ConflictSummaryWindow instance;

        public static ConflictData SelectedConflict;

        public static AtopAircraftState Fdr;


        private SynchronizationContext uiContext;

        private ConflictSummaryWindow(SynchronizationContext context)
        {
            uiContext = context ?? throw new ArgumentNullException(nameof(context));
        }
        public static ConflictSummaryWindow GetInstance()
        {
            if (instance == null)
            {
                throw new InvalidOperationException("ErrorHandler not initialized.");
            }
            return instance;
        }
        public static void Initialize(SynchronizationContext context)
        {
            if (instance == null)
            {
                instance = new ConflictSummaryWindow(context);
            }
        }
        public ConflictSummaryWindow()
        {
            InitializeComponent();

            ConflictsUpdated += UpdateConflicts;
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
            conflictListView.Columns.Clear(); // Clear existing columns

            conflictListView.Columns.Add("Intruder Callsign", 80, HorizontalAlignment.Left);
            conflictListView.Columns.Add("Intruder Attitude", 25, HorizontalAlignment.Left);
            conflictListView.Columns.Add("Active Callsign", 70, HorizontalAlignment.Left);
            conflictListView.Columns.Add("Active Attitude", 20, HorizontalAlignment.Left);
            conflictListView.Columns.Add("Conflict Override", 50, HorizontalAlignment.Left);
            conflictListView.Columns.Add("Conflict Symbol", 50, HorizontalAlignment.Left);
            conflictListView.Columns.Add("Earliest LOS", 80, HorizontalAlignment.Left);
            conflictListView.Columns.Add("Conflict End", 80, HorizontalAlignment.Left);

            conflictListView.Items.Clear();

            var conflictDatas = await Task.Run(() => ConflictDatas.Distinct().OrderBy(t => t.EarliestLos));

            foreach (ConflictData conflict in conflictDatas.Distinct())
            {
                AtopAircraftDisplayState intAtt = new AtopAircraftDisplayState(conflict.Intruder.GetAtopState());
                AtopAircraftDisplayState actAtt = new AtopAircraftDisplayState(conflict.Active.GetAtopState());

                ListViewItem item = new ListViewItem(conflict.Intruder?.Callsign.PadRight(7));
                item.SubItems.Add(intAtt.ConflictAttitudeFlag.Value.ToString().PadRight(1));
                item.SubItems.Add(conflict.Active.Callsign.PadRight(7));
                item.SubItems.Add(actAtt.ConflictAttitudeFlag.Value.ToString().PadRight(6));
                item.SubItems.Add(" ").ToString().PadRight(2);
                item.SubItems.Add(AtopAircraftDisplayState.GetConflictSymbol(conflict).PadRight(2));
                item.SubItems.Add(conflict.EarliestLos.ToString("HHmm").PadRight(4));
                item.SubItems.Add(conflict.ConflictEnd.ToString("HHmm").PadRight(4));
                item.Tag = conflict;
                conflictListView.Items.Add(item);



                conflictListView.MouseClick += ConflictListView_MouseClick; // Subscribe to the event only once                

                conflictListView.Refresh();
                this.Invoke((Action)(() => this.Visible = ConflictDatas.Any()));
            }

        }
        private void ConflictListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                    ListViewItem item = conflictListView.SelectedItems[0];
                    ConflictData conflictData = (ConflictData)item.Tag;

                    ConflictReportWindow reportWindow = new ConflictReportWindow(conflictData);
                    reportWindow.ShowDialog();
                    reportWindow.DisplayConflictDetails();
                    //reportWindow.Show(ActiveForm);               
            }
        }
    }
}