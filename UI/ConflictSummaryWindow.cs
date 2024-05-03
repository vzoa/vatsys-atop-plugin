using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AtopPlugin.Conflict;
using vatsys;
using System.Drawing;

namespace AtopPlugin.UI
{
    public partial class ConflictSummaryTable : BaseForm
    {
        public static List<ConflictProbe.ConflictData> cpars = new List<ConflictProbe.ConflictData>();

        public ConflictSummaryTable()
        {
            InitializeComponent();
            this.Shown += new EventHandler(this.ConflictWindow_Shown);

            UpdateConflicts();

        }

        private void UpdateConflicts()
        {

            listView1.Items.Clear();
            foreach (var cpar in cpars)
            {
                AddConflict(cpar);
            }

            AddConflict(new ConflictProbe.ConflictData()
            {
                
                ConflictType = Models.ConflictType.SameDirection,
                EarliestLos = new DateTime(),
                LatestLos = new DateTime()

            });
        }

        public void AddConflict(ConflictProbe.ConflictData cpar)
        {
            //cpar.ConflictType.Equals.Models.ConflictType.SameDirection ? AtopPlugin.Symbols.SameDirection : default;
            
            cpars.Add(cpar);
            ListViewItem item = new ListViewItem(cpar.Fdr2?.Callsign);
            item.SubItems.Add(cpar.ConflictType.ToString());
            item.SubItems.Add(cpar.EarliestLos.ToString("HH:mm"));
            item.SubItems.Add(cpar.LatestLos.ToString("HH:mm"));

            listView1.Items.Add(item);
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