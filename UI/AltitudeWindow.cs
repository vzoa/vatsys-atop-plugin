using AtopPlugin.Conflict;
using AtopPlugin.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;
using static AtopPlugin.Conflict.ConflictProbe;

namespace vatsys_atop_plugin.UI
{

    public partial class AltitudeWindow : BaseForm
    {
        private ConflictData Selected;
        public AltitudeWindow()
        {
            InitializeComponent();            
            //UpdateSearchButtonState();
            ControlButtonState();
            PopulateAltitudeListView();
        }

        public static void Handle(CustomLabelItemMouseClickEventArgs eventArgs)
        {

            AltitudeWindow window = new AltitudeWindow();

            MMI.InvokeOnGUI(window.Show);

            eventArgs.Handled = true;
        }


        private void PopulateAltitudeListView()
        {
            // Add a column if not already added
            if (lvw_altitudes.Columns.Count == 0)
            {
                lvw_altitudes.Columns.Add("Altitude", 100, HorizontalAlignment.Center);
            }
            
            var listViewItems = new List<ListViewItem>();
            // Loop through the altitude range
            for (int altitude = 0; altitude <= 600; altitude += 10)
            {
                // Format altitude
                var altitudeText = new ListViewItem((altitude == 600) ? "600" : altitude.ToString("D3"));

                // Add to ListView
                listViewItems.Add(altitudeText);
                
            }
            //lvw_altitudes.Items.AddRange(listViewItems.ToArray());
        }

        private void MessageScroll_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                this.scrollBar1.Value -= scrollBar1.Change;
            }
            else
            {
                if (e.Delta >= 0)
                    return;
                this.scrollBar1.Value += scrollBar1.Change;
            }
        }

        private void scr_altitudes_Scroll(object sender, EventArgs e)
        {
            lvw_altitudes.SetScrollPosVert(scrollBar1.PercentageValue);
        }

        //public LevelMenu(FDP2.FDR source, int initialLevel)
        //{
        //    this.sourcefdr = source;
        //    this.InitLevel = initialLevel;
        //}

        private void UpdateSearchButtonState()
        {
           btn_search.Enabled = sourcefdr.IsTrackedByMe;                                        
        }

        private void ControlButtonState()
        {
            if(!sourcefdr.IsTrackedByMe)
            {
                btn_probe.Text = "Override";
                btn_response.Text = "CONTROL";
                btn_response.BackColor = Color.Yellow;
                btn_send.Enabled = false;
                btn_cancel.Enabled = false;
                btn_vhf.Enabled = false;
            };
        }

        private void btn_probe_Click(object sender, EventArgs e)
        {
            ConflictProbe.Probe(sourcefdr);
            MMI.ShowGraphicRoute(dataBlock);

            if (Selected.ConflictStatus is ConflictStatus.Imminent or ConflictStatus.Actual)
            {
                btn_probe.Text = "Override";
                btn_response.BackColor = Color.Red;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "ALERT";
            }

            else if (Selected.ConflictStatus is ConflictStatus.Advisory)
            {
                btn_probe.Text = "Override";
                btn_response.BackColor = Color.Orange;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "WARN";
            }

            else if (Selected.ConflictStatus == ConflictStatus.None)
            {
                btn_response.BackColor = Color.Green;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "OK";
            }

        }

        private void btn_send_Click(object sender, EventArgs e)
        {
            if (lvw_altitudes.SelectedItems.Count > 0)
            {
                // Get the selected item
                ListViewItem selectedItem = lvw_altitudes.SelectedItems[0];

                // Parse the selected altitude text to an integer
                if (int.TryParse(selectedItem.Text, out int selectedAltitude))
                {
                    // Set the CFL for the sourcefdr using the selected altitude
                    FDP2.SetCFL(sourcefdr, selectedAltitude.ToString());                    
                }
            }
        }

        private void btn_vhf_Click(object sender, EventArgs e)
        {
            if (lvw_altitudes.SelectedItems.Count > 0)
            {
                // Get the selected item
                ListViewItem selectedItem = lvw_altitudes.SelectedItems[0];

                // Parse the selected altitude text to an integer
                if (int.TryParse(selectedItem.Text, out int selectedAltitude))
                {
                    // Set the CFL for the sourcefdr using the selected altitude
                    FDP2.SetCFL(sourcefdr, selectedAltitude.ToString());
                }
            }
        }

        private void btn_close_Click(object sender, EventArgs e)
        {
            Close();         
        }
    }
}
