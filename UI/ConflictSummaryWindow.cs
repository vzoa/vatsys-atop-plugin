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


namespace AtopPlugin.UI
{
    public partial class ConflictSummaryWindow : BaseForm
    {

        public static ConflictData SelectedConflict;

        public static AtopAircraftState Fdr;

        private Task _loopTask;

        public static int updateInterval = 10000;

        public ConflictSummaryWindow()
        {
            InitializeComponent();
            //this.Shown += new EventHandler(this.ConflictWindow_Shown);


            ConflictProbe.ConflictsUpdated += UpdateConflicts;

            //_loopTask = Task.Run(async () => {
            //    for (; ; )
            //    {
            //        await Task.Delay(updateInterval);
            //        //UpdateConflicts();
            //        DisplayConflicts();
            //    }
            //});
        }

        ~ConflictSummaryWindow()
        {
            _loopTask.Dispose();
        }

        private void UpdateConflicts(object sender, EventArgs e)
        {
            DisplayConflicts();
        }


        private void ConflictSummaryWindow_Load(object sender, EventArgs e)
        {
            DisplayConflicts();
        }


        private void ConflictWindow_Shown(object sender, EventArgs e)
        {
            DisplayConflicts();
        }

        private void listView1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            DisplayConflicts();
        }
        private void DisplayConflicts()
        {
            listView1.Items.Clear();
            labelConflictData.Text = string.Empty;

            foreach (var conflict in ConflictProbe.ConflictDatas.OrderBy(t => t.EarliestLos).Distinct())
            {
                AtopAircraftDisplayState intAtt = new AtopAircraftDisplayState(conflict.Intruder.GetAtopState());
                AtopAircraftDisplayState actAtt = new AtopAircraftDisplayState(conflict.Active.GetAtopState());
                labelConflictData.Text += conflict.Intruder?.Callsign.PadRight(7) + " ".ToString().PadRight(3) + intAtt.ConflictAttitudeFlag.Value
                    + " ".ToString().PadRight(5) + conflict.Active.Callsign.PadRight(7) + " ".ToString().PadRight(3) + actAtt.ConflictAttitudeFlag.Value
                    + " ".ToString().PadRight(10) + AtopAircraftDisplayState.GetConflictSymbol(conflict) + " ".ToString().PadRight(5)
                    + conflict.EarliestLos.ToString("HHmm") + " ".ToString().PadRight(8) + conflict.ConflictEnd.ToString("HHmm") + "\n";
            }
            if (ConflictProbe.ConflictDatas.Count > 0)
            {
                this.Show();
            }
            else
            {
                this.Close();
            }
            Label conflictLabel = new Label();
            conflictLabel.MouseDown += (sender, e) =>
            {
                SelectedConflict = new ConflictData();

                if (e.Button == MouseButtons.Left)
                {
                    ConflictReportWindow window = new ConflictReportWindow();
                    window.Show(ActiveForm);
                }
            };

        }
    }
}