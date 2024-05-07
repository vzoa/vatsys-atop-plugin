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

namespace AtopPlugin.UI
{
    public partial class ConflictSummaryTable : BaseForm
    {
        public static List<ConflictProbe.Conflicts?> cpars = new List<ConflictProbe.Conflicts?>();

        public static AtopAircraftState Fdr;

        private Task _loopTask;

        public static int updateInterval = 10000;

        public ConflictSummaryTable()
        {
            InitializeComponent();
            this.Shown += new EventHandler(this.ConflictWindow_Shown);

            UpdateConflicts();
            _loopTask = Task.Run(async () => {
                for (; ; )
                {
                    await Task.Delay(updateInterval);
                    UpdateConflicts();
                }
            });
        }

        ~ConflictSummaryTable()
        {
            _loopTask.Dispose();
        }

        private void UpdateConflicts()
        {
                      
           
            //listView1.Items.Clear();
            foreach (var intruder in GetFDRs.OrderBy(time => time.GetConflicts()).ToArray())
            {
                AddConflict(intruder.GetConflicts());
            }

            //AddConflict(new 
            //
            //{
            //    
            //    ConflictType = Models.ConflictType.SameDirection,
            //    EarliestLos = new DateTime(),
            //    LatestLos = new DateTime()
            //
            //});
        }

        public void AddConflict(ConflictProbe.Conflicts? intruder)
        {
            //cpar.ConflictType.Equals.Models.ConflictType.SameDirection ? AtopPlugin.Symbols.SameDirection : default;

            for (var a = 0; a < intruder?.ActualConflicts.Count; a++)
            {
                cpars.Add(intruder);

                ListViewItem item = new ListViewItem(intruder?.ActualConflicts[a].Fdr2.Callsign.ToString());
                item.SubItems.Add(intruder?.ActualConflicts[a].ConflictType.ToString());
                item.SubItems.Add(intruder?.ActualConflicts[a].EarliestLos.ToString("HH:mm"));
                item.SubItems.Add(intruder?.ActualConflicts[a].LatestLos.ToString("HH:mm"));

                listView1.Items.Add(item);
            }
            for (var r = 0; r < intruder?.ImminentConflicts.Count; r++)
            {
                cpars.Add(intruder);

                ListViewItem item = new ListViewItem(intruder?.ActualConflicts[r].Fdr2.Callsign.ToString());
                item.SubItems.Add(intruder?.ActualConflicts[r].ConflictType.ToString());
                item.SubItems.Add(intruder?.ActualConflicts[r].EarliestLos.ToString("HH:mm"));
                item.SubItems.Add(intruder?.ActualConflicts[r].LatestLos.ToString("HH:mm"));

                listView1.Items.Add(item);
            }
            for (var o = 0; o < intruder?.AdvisoryConflicts.Count; o++)
            {
                cpars.Add(intruder);

                ListViewItem item = new ListViewItem(intruder?.ActualConflicts[o].Fdr2.Callsign.ToString());
                item.SubItems.Add(intruder?.ActualConflicts[o].ConflictType.ToString());
                item.SubItems.Add(intruder?.ActualConflicts[o].EarliestLos.ToString("HH:mm"));
                item.SubItems.Add(intruder?.ActualConflicts[o].LatestLos.ToString("HH:mm"));

                listView1.Items.Add(item);
            }


        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void ConflictSummaryTable_Load(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void ConflictWindow_Shown(object sender, EventArgs e) => this.Invalidate();

        private void listView1_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }


        // private void InitializeComponent()
        // {
        //     this.SuspendLayout();
        //     // 
        //     // ConflictSummaryTable
        //     // 
        //     this.ClientSize = new System.Drawing.Size(1080, 804);
        //     this.Name = "ConflictSummaryTable";
        //     this.ResumeLayout(false);
        //
        // }
    }
}