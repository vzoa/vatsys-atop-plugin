using AtopPlugin;
using AtopPlugin.Logic;
using AtopPlugin.Models;
using AtopPlugin.State;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;
using static AtopPlugin.Conflict.ConflictProbe;
using static vatsys.FDP2;

namespace vatsys_atop_plugin.UI
{

    public partial class AltitudeWindow : BaseForm
    {
        private object conflict;

        private object source;

        private object datablock;

        private static AltitudeWindow instance;

        AtopAircraftDisplayState display;
        private AltitudeWindow(FDP2.FDR sourcefdr, Track dataBlock)
        {
            InitializeComponent();
            //this.conflict = (object)inConflict;
            this.datablock = (object)dataBlock;
            this.source = (object)sourcefdr;
            //PopulateAltitudeListView();
            UpdateSearchButtonState();
            ControlButtonState();
            AltitudeListViewState();
            ClimbByCheckState();
            lbl_call.Text = ((FDP2.FDR)this.source).Callsign.ToUpper();
            this.StartPosition = FormStartPosition.Manual;
            Point cursorPosition = Cursor.Position;
            this.Location = cursorPosition;
        }

        public static AltitudeWindow GetInstance(FDP2.FDR sourcefdr, Track dataBlock)
        {
            if (instance == null || instance.IsDisposed)
            {
                instance = new AltitudeWindow(sourcefdr, dataBlock);
                instance.TopMost = true;
            }
            else
            {
                //instance.BringToFront(); // Bring existing instance to front
                instance.TopMost = true;
            }
            return instance;
        }

        private void PopulateAltitudeListView()
        {
            // Add a column if not already added
            if (lvw_altitudes.Columns.Count == 0)
            {
                lvw_altitudes.Columns.Add("Altitude", 100, HorizontalAlignment.Center);
            }


            try
            {
                // Loop through the altitude range
                for (int altitude = 0; altitude <= 600; altitude += 10)
                {
                    // Format altitude
                    var altitudeText = (altitude == 600) ? "600" : altitude.ToString("D3");

                    // Add to ListView                   
                    lvw_altitudes.Items.Add(altitudeText);
                }
            }

            catch (Exception)
            {
                //ignored - unknown error populating
            }
        }

        public static void Handle(CustomLabelItemMouseClickEventArgs eventArgs)
        {
            AltitudeWindow window = GetInstance(eventArgs.Track.GetFDR(), eventArgs.Track);

            if (eventArgs.Button == CustomLabelItemMouseButton.Right)
            {
                MMI.InvokeOnGUI(window.Show);
            }
            eventArgs.Handled = true;
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
            //lvw_altitudes.SetScrollPosVert(scrollBar1.PercentageValue);
        }

        private void UpdateSearchButtonState()
        {
            if (((FDP2.FDR)this.source).IsTrackedByMe && ((FDP2.FDR)this.source).ControllingSector.Name is "OA")
            {
                btn_search.Enabled = true;
            }

        }


        private void AltitudeListViewState()
        {
            string cflText = ((FDP2.FDR)this.source).CFLString;
            foreach (ListViewItem item in lvw_altitudes.Items)
            {
                if (((FDP2.FDR)this.source).CFLUpper != -1)
                {
                    if (cflText.Contains("B"))
                    {
                        var parts = cflText.Split('B');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int low) && int.TryParse(parts[1], out int high))
                        {
                            int altitude = int.Parse(item.Text);
                            if (altitude >= low && altitude <= high)
                            {
                                item.Selected = true;
                                item.Focused = true;
                                lvw_altitudes.EnsureVisible(item.Index);
                                lvw_altitudes.FocusedItem = item;
                            }
                        }
                    }
                    else if (item.Text == cflText)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        lvw_altitudes.EnsureVisible(item.Index);
                        lvw_altitudes.FocusedItem = item;
                        fld_time.Enabled = false;
                    }
                }
                else if (((FDP2.FDR)this.source).CFLUpper == -1 && item.Text == (((FDP2.FDR)this.source).RFL / 100).ToString())
                {
                    item.Selected = true;
                    item.Focused = true;
                    lvw_altitudes.EnsureVisible(item.Index);
                    lvw_altitudes.FocusedItem = item;
                }
            }
        }
        private void ControlButtonState()
        {
            if (!((FDP2.FDR)this.source).IsTrackedByMe)
            {
                btn_probe.Text = "Override";
                btn_response.Text = "CONTROL";
                btn_response.BackColor = Color.Yellow;
                btn_send.Enabled = false;
                btn_cancel.Enabled = false;
                btn_vhf.Enabled = false;
            }
            else
            {
                btn_response.Text = "-";
                btn_cancel.Enabled = false;
            }
            //ControlPaint.DrawBorder3D(pe.Graphics, this.ClientRectangle, Border3DStyle.Sunken);
        }

        private void ClimbByCheckState()
        {
            fld_time.Enabled = false;
            fld_time.Text = DateTime.UtcNow.AddMinutes(20).ToString("HHmm");
        }

        private void btn_probe_Click(object sender, EventArgs e)
        {
            if (lvw_altitudes.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = lvw_altitudes.SelectedItems[0];
                int lowest = int.Parse(lvw_altitudes.SelectedItems[0].Text) * 100;
                int highest = int.Parse(lvw_altitudes.SelectedItems[0].Text) * 100;

                foreach (ListViewItem item in lvw_altitudes.SelectedItems)
                {
                    int value = int.Parse(item.Text) * 100;
                    if (value < lowest)
                    {
                        lowest = value;
                    }
                    else if (value > highest)
                    {
                        highest = value;
                    }
                    try
                    {
                        if (lvw_altitudes.SelectedItems.Count >= 1 && selectedItem.Text == ((FDP2.FDR)this.source).CFLString)
                        {
                            fld_time.Enabled = false;
                            climbByCheck.Enabled = false;
                            ManualProbe((FDP2.FDR)this.source);
                        }

                        if (lvw_altitudes.SelectedItems.Count > 1)
                        {
                            ManualProbe((FDP2.FDR)this.source);
                            FDP2.SetPRL(GetFDRs.FirstOrDefault(s => s.Callsign == "*" + ((FDP2.FDR)this.source).Callsign), ((FDP2.FDR)this.source).PRL);
                            FDP2.SetCFL(GetFDRs.FirstOrDefault(s => s.Callsign == "*" + ((FDP2.FDR)this.source).Callsign), lowest, highest, false, true);
                        }

                        else if (lvw_altitudes.SelectedItems.Count == 1 && int.TryParse(selectedItem.Text, out int selectedAltitude))
                        {
                            ManualProbe((FDP2.FDR)this.source);
                            FDP2.SetPRL(GetFDRs.FirstOrDefault(s => s.Callsign == "*" + ((FDP2.FDR)this.source).Callsign), ((FDP2.FDR)this.source).PRL);
                            FDP2.SetCFL(GetFDRs.FirstOrDefault(s => s.Callsign == "*" + ((FDP2.FDR)this.source).Callsign), selectedAltitude.ToString());
                        }

                    }

                    catch
                    {
                        btn_response.Text = "ERROR";
                        btn_response.BackColor = Color.Red;
                        btn_response.ForeColor = Color.Yellow;
                    }

                }
            }

            var badLogic = ((FDP2.FDR)source)?.PerformanceData.MaxAltitude < Convert.ToInt32(lvw_altitudes.SelectedItems[0].Text);
            var conflictType = ((FDP2.FDR)source)?.GetConflicts();

            if (conflictType.Equals(ConflictStatus.Imminent) || conflictType.Equals(ConflictStatus.Actual))
            {
                btn_probe.Text = "Override";
                btn_response.BackColor = Color.Red;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "ALERT";
                btn_send.Enabled = false;
                btn_vhf.Enabled = false;
            }

            else if (conflictType.Equals(ConflictStatus.Advisory))
            {
                btn_probe.Text = "Override";
                btn_response.BackColor = Color.Orange;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "WARN";
                btn_send.Enabled = false;
                btn_vhf.Enabled = false;
            }

            else if (badLogic)
            {
                btn_response.BackColor = Color.Yellow;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "LOGIC";
                btn_send.Enabled = false;
                btn_vhf.Enabled = false;
            }

            else
            {
                btn_response.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(247)))), ((int)(((byte)(57)))));
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "OK";
            }

            btn_probe.Enabled = false;
            btn_cancel.Enabled = true;

        }

        private void btn_send_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (btn_send.Text == "Send")
                {
                    btn_send.Text = "HF";
                }
                else
                {
                    btn_send.Text = "Send";
                }
            }
        }

        private void btn_send_Click(object sender, EventArgs e)
        {
            btn_vhf_Click(sender, e);

            try
            {
                var cs = ((FDP2.FDR)source).Callsign;
                var prl = ((FDP2.FDR)this.source).PRL;
                var uppercfl = ((FDP2.FDR)this.source).CFLUpper;
                var lowercfl = ((FDP2.FDR)this.source).CFLLower;
                var listAlt = lvw_altitudes.SelectedItems[0].Text;
                var timeInputValid = false;
                if (!string.IsNullOrEmpty(fld_time.Text))
                {
                    var byTime = TimeSpan.Parse(fld_time.Text);

                    var byTimeDate = DateTime.Today.Add(byTime);

                    timeInputValid = byTimeDate >= DateTime.UtcNow;
                }



                Type networkType = typeof(Network);

                if (networkType != null)
                {
                    object networkInstance = networkType.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); //Activator.CreateInstance(networkType);

                    MethodInfo sendTextMessageMethod = networkType.GetMethod("SendTextMessage", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (sendTextMessageMethod != null && FlightDataCalculator.GetCalculatedFlightData((FDP2.FDR)source).Cpdlc && btn_send.Text != "HF")
                    {

                        if (climbByCheck.Checked && timeInputValid && prl > uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " DESCEND TO REACH " + "F" + listAlt + " BY " + fld_time.Text + " REPORT LEVEL " + "F" + listAlt });
                        }
                        else if (climbByCheck.Checked && timeInputValid && prl < uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " CLIMB TO REACH " + "F" + listAlt + " BY " + fld_time.Text + " REPORT LEVEL " + "F" + listAlt });
                        }
                        else if (lowercfl != -1 && uppercfl >= prl && prl >= lowercfl && lowercfl != uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " MAINTAIN BLOCK " + "F" + listAlt });
                        }
                        else if (lowercfl != -1 && prl < lowercfl && lowercfl != uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " CLIMB TO AND MAINTAIN BLOCK " + listAlt });
                        }
                        else if (lowercfl != -1 && prl > uppercfl && lowercfl != uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " DESCEND TO AND MAINTAIN BLOCK " + listAlt });
                        }
                        else if (prl > uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " DESCEND TO AND MAINTAIN " + "F" + listAlt + " REPORT LEVEL " + "F" + listAlt });
                        }
                        else if (prl < uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " CLIMB TO AND MAINTAIN " + "F" + listAlt + " REPORT LEVEL " + "F" + listAlt });
                        }
                        else
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " MAINTAIN " + "F" + listAlt });
                        }
                    }
                }
                if (!Network.PrimaryFrequencySet)
                {
                    Errors.Add(new Exception("No primary frequency set for CPDLC")
                    {
                        Source = "CPDLC"
                    });
                    return;
                }
                else if (btn_send.Text == "HF" || !FlightDataCalculator.GetCalculatedFlightData((FDP2.FDR)source).Cpdlc)
                {
                    if (climbByCheck.Checked && timeInputValid && prl > uppercfl)
                    {
                        Network.SendRadioMessage(cs + " ATCC DESCEND TO REACH " + "F" + listAlt + " BY " + fld_time.Text + " REPORT LEVEL " + "F" + listAlt);
                    }
                    else if (climbByCheck.Checked && timeInputValid && prl < uppercfl)
                    {
                        Network.SendRadioMessage(cs + " ATCC CLIMB TO REACH " + "F" + listAlt + " BY " + fld_time.Text + " REPORT LEVEL " + "F" + listAlt);
                    }
                    else if (lowercfl != -1 && uppercfl >= prl && prl >= lowercfl && lowercfl != uppercfl)
                    {
                        Network.SendRadioMessage(cs + " ATCC MAINTAIN BLOCK " + "F" + listAlt);
                    }
                    else if (lowercfl != -1 && prl < lowercfl && lowercfl != uppercfl)
                    {
                        Network.SendRadioMessage(cs + " ATCC CLIMB TO AND MAINTAIN BLOCK " + listAlt);
                    }
                    else if (lowercfl != -1 && prl > uppercfl && lowercfl != uppercfl)
                    {
                        Network.SendRadioMessage(cs + " ATCC DESCEND TO AND MAINTAIN BLOCK " + listAlt);
                    }
                    else if (prl > uppercfl)
                    {
                        Network.SendRadioMessage(cs + " ATCC DESCEND TO AND MAINTAIN " + "F" + listAlt + " REPORT LEVEL " + "F" + listAlt);
                    }
                    else if (prl < uppercfl)
                    {
                        Network.SendRadioMessage(cs + " ATCC CLIMB TO AND MAINTAIN " + "F" + listAlt + " REPORT LEVEL " + "F" + listAlt);
                    }
                    else
                    {
                        Network.SendRadioMessage(cs + " ATCC MAINTAIN " + "F" + listAlt);
                    }
                }
            }
            catch
            {
                btn_response.Text = "ERROR";
                btn_response.BackColor = Color.Red;
                btn_response.ForeColor = Color.Yellow;
            }
        }

        private void btn_vhf_Click(object sender, EventArgs e)
        {
            if (lvw_altitudes.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = lvw_altitudes.SelectedItems[0];
                int lowest = int.Parse(lvw_altitudes.SelectedItems[0].Text) * 100;
                int highest = int.Parse(lvw_altitudes.SelectedItems[0].Text) * 100;

                foreach (ListViewItem item in lvw_altitudes.SelectedItems)
                {
                    int value = int.Parse(item.Text) * 100;
                    if (value < lowest)
                    {
                        lowest = value;
                    }
                    else if (value > highest)
                    {
                        highest = value;
                    }
                    try
                    {
                        if (lvw_altitudes.SelectedItems.Count > 1) FDP2.SetCFL(((FDP2.FDR)this.source), lowest, highest, false, true);

                        else if (lvw_altitudes.SelectedItems.Count == 1 && int.TryParse(selectedItem.Text, out int selectedAltitude))
                        {
                            FDP2.SetCFL(((FDP2.FDR)this.source), selectedAltitude.ToString());
                        }
                        btn_probe_Click(sender, e);
                    }

                    catch
                    {
                        btn_response.Text = "ERROR";
                        btn_response.BackColor = Color.Red;
                        btn_response.ForeColor = Color.Yellow;
                    }

                }
            }
            Close();
            Dispose();
        }

        private void btn_cancel_Click(object sender, EventArgs e)
        {
            MMI.HideGraphicRoute((Track)this.datablock);
            btn_probe.Enabled = true;
            btn_response.Text = "-";

            foreach (FDR item in GetFDRs.Where((FDR c) => c.Callsign == "*" + ((FDP2.FDR)source).Callsign))
            {
                FDP2.DeleteFDR(item);
            }
        }
        private void lvw_altitudes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvw_altitudes.SelectedItems.Count == 1)
            {
                fld_level.Text = lvw_altitudes.SelectedItems[0].Text;
            }
            else if (lvw_altitudes.SelectedItems.Count > 1)
            {
                int lowest = int.Parse(lvw_altitudes.SelectedItems[0].Text);
                int highest = int.Parse(lvw_altitudes.SelectedItems[0].Text);

                foreach (ListViewItem item in lvw_altitudes.SelectedItems)
                {
                    int value = int.Parse(item.Text);
                    if (value < lowest)
                    {
                        lowest = value;
                    }
                    else if (value > highest)
                    {
                        highest = value;
                    }
                    item.Selected = true;
                    item.Focused = true;
                    lvw_altitudes.EnsureVisible(item.Index);
                    lvw_altitudes.FocusedItem = item;
                }

                fld_level.Text = lowest.ToString() + "B" + highest.ToString();
            }
            else
            {
                fld_level.Text = string.Empty;
            }
        }

        //private void lvw_altitudes_Leave(object sender, EventArgs e)
        //{
        //    lvw_altitudes.SelectedItems.Clear();
        //}

        private void fld_level_TextChanged(object sender, EventArgs e)
        {
            string text = fld_level.Text.ToUpper();

            if (text.Contains("B"))
            {
                var parts = text.Split('B');
                if (parts.Length == 2 && int.TryParse(parts[0], out int low) && int.TryParse(parts[1], out int high))
                {
                    foreach (ListViewItem item in lvw_altitudes.Items)
                    {
                        int altitude = int.Parse(item.Text);
                        if (altitude >= low && altitude <= high)
                        {
                            item.Selected = true;
                        }
                    }
                }
            }
            else
            {
                foreach (ListViewItem item in lvw_altitudes.Items)
                {
                    if (item.Text == text)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        lvw_altitudes.EnsureVisible(item.Index);
                        lvw_altitudes.FocusedItem = item;
                    }
                }
            }
        }

        private void fld_level_Enter(object sender, EventArgs e)
        {
            lvw_altitudes.SelectedItems.Clear();
        }


        private void climbByCheck_CheckStateChanged(object sender, EventArgs e)
        {
            if (climbByCheck.Checked)
            {
                fld_time.Enabled = true;
                climbByCheck.ForeColor = Color.Red;
            }
            else fld_time.Enabled = false;

        }

        private void btn_close_Click(object sender, EventArgs e)
        {
            Close();
            Dispose();
        }
    }
}