using AtopPlugin;
using AtopPlugin.Display;
using AtopPlugin.Helpers;
using AtopPlugin.Logic;
using AtopPlugin.Models;
using AtopPlugin.State;
using System;
using System.Collections.Generic;
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
        private int _itemHeight = 0;

        private int GetItemHeight()
        {
            if (_itemHeight > 0) return _itemHeight;
            if (lvw_altitudes.Items.Count == 0) return 18;
            // Scroll fully to top so item 0 is on-screen, then measure its height.
            lvw_altitudes.TopItem = lvw_altitudes.Items[0];
            var rect = lvw_altitudes.GetItemRect(0, ItemBoundsPortion.Entire);
            _itemHeight = rect.Height > 0 ? rect.Height : 18;
            return _itemHeight;
        }

        // Updates the scrollbar thumb size to reflect the visible/total item ratio.
        // ActualHeight is a 0-10 value where 10 = everything visible (no scroll).
        private void UpdateScrollbarThumb()
        {
            if (lvw_altitudes.Items.Count == 0 || !IsHandleCreated) return;
            int h = GetItemHeight();
            if (h <= 0) return;
            int visible = Math.Max(1, lvw_altitudes.ClientSize.Height / h);
            int total = lvw_altitudes.Items.Count;
            int thumbSize = Math.Max(1, Math.Min(9, (int)Math.Round(10.0 * visible / total)));
            if (scrollBar1.ActualHeight != thumbSize)
                scrollBar1.ActualHeight = thumbSize;
        }

        private void SetListViewTopItem(int topIndex)
        {
            if (lvw_altitudes.Items.Count == 0) return;
            topIndex = Math.Max(0, Math.Min(topIndex, lvw_altitudes.Items.Count - 1));
            lvw_altitudes.TopItem = lvw_altitudes.Items[topIndex];
        }

        private object conflict;

        private object source;

        private object datablock;
        private bool _virtualProbePending;
        private bool _overrideActive;
        private bool _conflictBlocked;
        private bool _searchInProgress;
        private Queue<int> _pendingSearchLevels = new();
        private int? _currentSearchLevel;
        private bool _openedFromCommIcon;
        private int? _replyDownlinkMessageId;

        private static AltitudeWindow instance;

        AtopAircraftDisplayState display;
        private AltitudeWindow(FDP2.FDR sourcefdr, Track dataBlock)
        {
            InitializeComponent();
            ApplyVatSysTheme();
            BindToSource(sourcefdr, dataBlock);
            this.StartPosition = FormStartPosition.Manual;
            Point cursorPosition = Cursor.Position;
            this.Location = cursorPosition;

            // Use vatSys scrollbar; disable the ListView's native scrollbar
            scrollBar1.Visible = true;
            scrollBar1.ActualHeight = 1; // ~12% visible (8 of 64 items); updated dynamically in UpdateScrollbarThumb()
            scrollBar1.BringToFront();

            // Wire right-click override on the probe button
            btn_probe.MouseDown += btn_probe_MouseDown;

            VirtualProbeResultsReceived += OnVirtualProbeResultsReceived;
        }

        private void ApplyVatSysTheme()
        {
            var background = Colours.GetColour(Colours.Identities.WindowBackground);
            var interactive = Colours.GetColour(Colours.Identities.InteractiveText);
            var nonInteractive = Colours.GetColour(Colours.Identities.NonInteractiveText);

            BackColor = background;
            Font = MMI.eurofont_sml ?? Font;

            lbl_call.BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            lbl_call.ForeColor = interactive;

            byTime.BackColor = background;
            byTime.ForeColor = nonInteractive;

            fld_level.BackColor = Color.White;
            fld_level.ForeColor = interactive;
            fld_time.BackColor = Color.White;
            fld_time.ForeColor = interactive;

            lvw_altitudes.BackColor = Color.White;
            lvw_altitudes.ForeColor = interactive;

            foreach (var button in new[] { btn_close, btn_cancel, btn_vhf, btn_send, btn_probe, btn_search, btn_unable, btn_response })
            {
                button.BackColor = background;
                button.ForeColor = interactive;
                button.Font = MMI.eurofont_sml ?? button.Font;
            }
        }

        public static AltitudeWindow GetInstance(FDP2.FDR sourcefdr, Track dataBlock, bool openedFromCommIcon = false, int? replyDownlinkMessageId = null)
        {
            if (instance == null || instance.IsDisposed)
            {
                instance = new AltitudeWindow(sourcefdr, dataBlock);
                instance.BindToSource(sourcefdr, dataBlock, openedFromCommIcon, replyDownlinkMessageId);
                instance.TopMost = true;
            }
            else
            {
                instance.BindToSource(sourcefdr, dataBlock, openedFromCommIcon, replyDownlinkMessageId);
                instance.TopMost = true;
            }
            return instance;
        }

        private void BindToSource(FDP2.FDR sourcefdr, Track dataBlock, bool openedFromCommIcon = false, int? replyDownlinkMessageId = null)
        {
            this.datablock = (object)dataBlock;
            this.source = (object)sourcefdr;

            _virtualProbePending = false;
            _overrideActive = false;
            _conflictBlocked = false;
            _searchInProgress = false;
            _pendingSearchLevels.Clear();
            _currentSearchLevel = null;
            _openedFromCommIcon = openedFromCommIcon;
            _replyDownlinkMessageId = ResolveReplyDownlinkMessageId(sourcefdr.Callsign, replyDownlinkMessageId);

            InitializeAltitudeItems();
            ResetSearchResults();

            UpdateSearchButtonState();
            ControlButtonState();
            AltitudeListViewState();
            ClimbByCheckState();
            lbl_call.Text = sourcefdr.Callsign.ToUpper();
            btn_probe.Text = "Probe";
            btn_response.Text = "-";
            btn_unable.Visible = _openedFromCommIcon && _replyDownlinkMessageId.HasValue;
            btn_unable.Enabled = btn_unable.Visible;
            btn_search.Visible = !btn_unable.Visible;
            SyncCancelButtonState();
        }

        private static string FormatAltitude(int altitude)
        {
            return altitude == 600 ? "600" : altitude.ToString("D3");
        }

        private static bool TryParseAltitude(string text, out int altitude)
        {
            var digits = new string((text ?? string.Empty).Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out altitude);
        }

        private void InitializeAltitudeItems()
        {
            foreach (ListViewItem item in lvw_altitudes.Items)
            {
                if (item.Tag is int) continue;

                if (TryParseAltitude(item.Text, out int altitude))
                {
                    item.Tag = altitude;
                    item.Text = FormatAltitude(altitude);
                }
            }
        }

        private static int GetAltitudeValue(ListViewItem item)
        {
            if (item.Tag is int altitude)
                return altitude;

            return TryParseAltitude(item.Text, out altitude) ? altitude : 0;
        }

        private static int? ResolveReplyDownlinkMessageId(string callsign, int? explicitReplyDownlinkMessageId)
        {
            if (explicitReplyDownlinkMessageId.HasValue)
                return explicitReplyDownlinkMessageId;

            return CpdlcPluginBridge.GetOpenDownlinkDetails(callsign)
                .OrderByDescending(d => d.Received)
                .Select(d => (int?)d.MessageId)
                .FirstOrDefault();
        }

        private void ResetSearchResults()
        {
            foreach (ListViewItem item in lvw_altitudes.Items)
            {
                var altitude = GetAltitudeValue(item);
                item.Text = FormatAltitude(altitude);
                item.ForeColor = SystemColors.WindowText;
            }
        }

        private void SetSearchMarker(int altitude, bool available)
        {
            foreach (ListViewItem item in lvw_altitudes.Items)
            {
                if (GetAltitudeValue(item) != altitude) continue;

                item.Text = $"{(available ? "*" : "X")} {FormatAltitude(altitude)}";
                item.ForeColor = available ? Color.Green : Color.Red;
                break;
            }
        }

        private void SetButtonsEnabledForSearch(bool enabled)
        {
            btn_probe.Enabled = enabled;
            btn_send.Enabled = enabled && !((btn_probe.Text == "Override" || _conflictBlocked) && !_overrideActive);
            btn_vhf.Enabled = enabled && !((btn_probe.Text == "Override" || _conflictBlocked) && !_overrideActive);
            btn_close.Enabled = enabled;
            btn_search.Enabled = enabled;
            btn_unable.Enabled = enabled && btn_unable.Visible;
            SyncCancelButtonState();
        }

        private bool HasSharedProbeState()
        {
            if (source is not FDP2.FDR sourceFdr)
                return false;

            return ProposedProfileBridge.GetVisualState(sourceFdr.Callsign) != StripProfileVisualState.None;
        }

        private void SyncCancelButtonState()
        {
            btn_cancel.Enabled = _searchInProgress || _virtualProbePending || HasSharedProbeState();
        }

        private void UpdateSearchButtonState()
        {
            bool searchEligible = source is FDP2.FDR sourceFdr
                && sourceFdr.IsTrackedByMe
                && sourceFdr.ControllingSector?.Name is "OA"
                && _conflictBlocked
                && !_searchInProgress
                && !_openedFromCommIcon;

            btn_search.Enabled = searchEligible;
        }

        private void RequestNextSearchProbe(FDP2.FDR sourceFdr)
        {
            if (_pendingSearchLevels.Count == 0)
            {
                _searchInProgress = false;
                _virtualProbePending = false;
                _currentSearchLevel = null;
                SetButtonsEnabledForSearch(true);
                UpdateSearchButtonState();
                SyncCancelButtonState();
                return;
            }

            _currentSearchLevel = _pendingSearchLevels.Dequeue();
            _virtualProbePending = true;
            RequestVirtualProbe(sourceFdr.Callsign, _currentSearchLevel.Value);
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
            try
            {
                AltitudeWindow window = GetInstance(eventArgs.Track.GetFDR(), eventArgs.Track);

                if (eventArgs.Button == CustomLabelItemMouseButton.Right)
                {
                    MMI.InvokeOnGUI(window.Show);
                }
                eventArgs.Handled = true;
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"AltitudeWindow.Handle: {ex.Message}", ex));
            }
        }


        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateScrollbarThumb();
        }

        private void MessageScroll_MouseWheel(object sender, MouseEventArgs e)
        {
            int h = GetItemHeight();
            if (h <= 0) return;
            int maxTop = Math.Max(0, lvw_altitudes.Items.Count - Math.Max(1, lvw_altitudes.ClientSize.Height / h));
            if (maxTop == 0) return;
            // Move 3 items per wheel notch
            float step = 3.0f / maxTop;
            scrollBar1.PercentageValue = Math.Max(0f, Math.Min(1f, scrollBar1.PercentageValue + (e.Delta > 0 ? -step : step)));
            scr_altitudes_Scroll(sender, EventArgs.Empty);
        }

        private void scr_altitudes_Scroll(object sender, EventArgs e)
        {
            if (lvw_altitudes.Items.Count == 0) return;
            UpdateScrollbarThumb();
            int itemHeight = GetItemHeight();
            int visibleCount = Math.Max(1, lvw_altitudes.ClientSize.Height / itemHeight);
            int maxTop = Math.Max(0, lvw_altitudes.Items.Count - visibleCount);
            if (maxTop == 0) return;
            int topIndex = (int)Math.Round(scrollBar1.PercentageValue * maxTop);
            topIndex = Math.Max(0, Math.Min(topIndex, maxTop));
            SetListViewTopItem(topIndex);
        }

        private void ScrollToItem(int index)
        {
            if (lvw_altitudes.Items.Count == 0) return;
            int itemHeight = GetItemHeight();
            int visibleCount = Math.Max(1, lvw_altitudes.ClientSize.Height / itemHeight);
            int maxTop = Math.Max(0, lvw_altitudes.Items.Count - visibleCount);
            int targetTop = Math.Max(0, Math.Min(index - visibleCount / 2, maxTop));
            if (maxTop > 0)
                scrollBar1.PercentageValue = (float)targetTop / maxTop;
            SetListViewTopItem(targetTop);
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
                            int altitude = GetAltitudeValue(item);
                            if (altitude >= low && altitude <= high)
                            {
                                item.Selected = true;
                                item.Focused = true;
                                ScrollToItem(item.Index);
                                lvw_altitudes.FocusedItem = item;
                            }
                        }
                    }
                    else if (TryParseAltitude(cflText, out int currentAltitude) && GetAltitudeValue(item) == currentAltitude)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        ScrollToItem(item.Index);
                        lvw_altitudes.FocusedItem = item;
                        fld_time.Enabled = false;
                    }
                }
                else if (((FDP2.FDR)this.source).CFLUpper == -1 && GetAltitudeValue(item) == (((FDP2.FDR)this.source).RFL / 100))
                {
                    item.Selected = true;
                    item.Focused = true;
                    ScrollToItem(item.Index);
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
            try
            {
                if (!(source is FDP2.FDR sourceFdr) || lvw_altitudes.SelectedItems.Count == 0)
                    return;

                if (string.Equals(btn_probe.Text, "Override", StringComparison.OrdinalIgnoreCase))
                {
                    _overrideActive = true;
                    btn_probe.Text = "Probe";
                    btn_send.Enabled = true;
                    btn_vhf.Enabled = true;
                    btn_response.Text = "OVERRIDE";
                    btn_response.BackColor = Color.Yellow;
                    btn_response.ForeColor = Color.Black;
                    return;
                }

                var selectedLevels = lvw_altitudes.SelectedItems
                    .Cast<ListViewItem>()
                    .Select(item => int.Parse(item.Text))
                    .ToList();

                int lowest = selectedLevels.Min();
                int highest = selectedLevels.Max();
                int currentFl = sourceFdr.PRL / 100;

                int proposedFl;
                if (selectedLevels.Count == 1)
                {
                    proposedFl = selectedLevels[0];
                }
                else if (currentFl < lowest)
                {
                    proposedFl = lowest;
                }
                else if (currentFl > highest)
                {
                    proposedFl = highest;
                }
                else
                {
                    proposedFl = currentFl;
                }

                if (sourceFdr.PerformanceData?.MaxAltitude < proposedFl)
                {
                    btn_response.BackColor = Color.Yellow;
                    btn_response.ForeColor = Color.Black;
                    btn_response.Text = "LOGIC";
                    btn_send.Enabled = false;
                    btn_vhf.Enabled = false;
                    return;
                }

                ResetSearchResults();
                _virtualProbePending = true;
                if (!ProposedProfileBridge.TryBeginProbe(sourceFdr.Callsign))
                {
                    _virtualProbePending = false;
                    SyncCancelButtonState();
                    return;
                }
                _overrideActive = false;
                _conflictBlocked = false;
                UpdateSearchButtonState();
                btn_probe.Enabled = false;
                SyncCancelButtonState();
                btn_response.BackColor = Color.LightGray;
                btn_response.ForeColor = Color.Black;

                ProbeRouteRenderer.ShowForTrack(datablock as Track);

                RequestVirtualProbe(sourceFdr.Callsign, proposedFl);
            }
            catch
            {
                _virtualProbePending = false;
                if (source is FDP2.FDR sourceFdr)
                    ProposedProfileBridge.CompleteProbe(sourceFdr.Callsign, null);
                SyncCancelButtonState();
                btn_response.Text = "ERROR";
                btn_response.BackColor = Color.Red;
                btn_response.ForeColor = Color.Yellow;
            }
        }

        private void OnVirtualProbeResultsReceived(string callsign, Conflicts conflicts)
        {
            if (!(source is FDP2.FDR sourceFdr)) return;
            if (!string.Equals(sourceFdr.Callsign, callsign, StringComparison.OrdinalIgnoreCase)) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnVirtualProbeResultsReceived(callsign, conflicts)));
                return;
            }

            if (_searchInProgress)
            {
                bool available = conflicts.ActualConflicts.Count == 0
                              && conflicts.ImminentConflicts.Count == 0
                              && conflicts.AdvisoryConflicts.Count == 0;

                if (_currentSearchLevel.HasValue)
                    SetSearchMarker(_currentSearchLevel.Value, available);

                RequestNextSearchProbe(sourceFdr);
                return;
            }

            if (!_virtualProbePending) return;

            _virtualProbePending = false;
            _overrideActive = false;

            bool hasAlert = conflicts.ActualConflicts.Count > 0 || conflicts.ImminentConflicts.Count > 0;
            bool hasWarn = conflicts.AdvisoryConflicts.Count > 0;

            if (hasAlert)
            {
                _conflictBlocked = true;
                btn_probe.Text = "Probe";
                btn_probe.Enabled = true;
                btn_response.BackColor = Color.Red;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "ALERT";
                btn_send.Enabled = false;
                btn_vhf.Enabled = false;
            }
            else if (hasWarn)
            {
                _conflictBlocked = true;
                btn_probe.Text = "Probe";
                btn_probe.Enabled = true;
                btn_response.BackColor = Color.Orange;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "WARN";
                btn_send.Enabled = false;
                btn_vhf.Enabled = false;
            }
            else
            {
                _conflictBlocked = false;
                btn_probe.Text = "Probe";
                btn_probe.Enabled = true;
                btn_response.BackColor = Color.FromArgb(70, 247, 57);
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "OK";
                btn_send.Enabled = true;
                btn_vhf.Enabled = true;
            }

            SyncCancelButtonState();
            UpdateSearchButtonState();
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
            try
            {
                bool cpdlcMessageSent = false;

                if ((btn_probe.Text == "Override" || _conflictBlocked) && !_overrideActive)
                {
                    btn_response.BackColor = Color.Yellow;
                    btn_response.ForeColor = Color.Black;
                    btn_response.Text = "OVERRIDE";
                    return;
                }

                var cs = ((FDP2.FDR)source).Callsign;
                var prl = ((FDP2.FDR)this.source).PRL;
                var uppercfl = ((FDP2.FDR)this.source).CFLUpper;
                var lowercfl = ((FDP2.FDR)this.source).CFLLower;
                if (lvw_altitudes.SelectedItems.Count == 0)
                    return;
                var listAlt = FormatAltitude(GetAltitudeValue(lvw_altitudes.SelectedItems[0]));
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
                            cpdlcMessageSent = true;
                        }
                        else if (climbByCheck.Checked && timeInputValid && prl < uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " CLIMB TO REACH " + "F" + listAlt + " BY " + fld_time.Text + " REPORT LEVEL " + "F" + listAlt });
                            cpdlcMessageSent = true;
                        }
                        else if (lowercfl != -1 && uppercfl >= prl && prl >= lowercfl && lowercfl != uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " MAINTAIN BLOCK " + "F" + listAlt });
                            cpdlcMessageSent = true;
                        }
                        else if (lowercfl != -1 && prl < lowercfl && lowercfl != uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " CLIMB TO AND MAINTAIN BLOCK " + listAlt });
                            cpdlcMessageSent = true;
                        }
                        else if (lowercfl != -1 && prl > uppercfl && lowercfl != uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " DESCEND TO AND MAINTAIN BLOCK " + listAlt });
                            cpdlcMessageSent = true;
                        }
                        else if (prl > uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " DESCEND TO AND MAINTAIN " + "F" + listAlt + " REPORT LEVEL " + "F" + listAlt });
                            cpdlcMessageSent = true;
                        }
                        else if (prl < uppercfl)
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " CLIMB TO AND MAINTAIN " + "F" + listAlt + " REPORT LEVEL " + "F" + listAlt });
                            cpdlcMessageSent = true;
                        }
                        else
                        {
                            sendTextMessageMethod.Invoke(networkInstance, new object[] { ((FDP2.FDR)source).Callsign, " MAINTAIN " + "F" + listAlt });
                            cpdlcMessageSent = true;
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

                if (cpdlcMessageSent)
                {
                    ProposedProfileBridge.MarkSentPendingReadback(cs);
                }

                SyncCancelButtonState();
            }
            catch
            {
                SyncCancelButtonState();
                btn_response.Text = "ERROR";
                btn_response.BackColor = Color.Red;
                btn_response.ForeColor = Color.Yellow;
            }
        }

        private void btn_vhf_Click(object sender, EventArgs e)
        {
            if ((btn_probe.Text == "Override" || _conflictBlocked) && !_overrideActive)
            {
                btn_response.BackColor = Color.Yellow;
                btn_response.ForeColor = Color.Black;
                btn_response.Text = "OVERRIDE";
                return;
            }

            if (source is not FDP2.FDR sourceFdr)
                return;

            if (lvw_altitudes.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = lvw_altitudes.SelectedItems[0];
                int lowest = GetAltitudeValue(lvw_altitudes.SelectedItems[0]) * 100;
                int highest = GetAltitudeValue(lvw_altitudes.SelectedItems[0]) * 100;

                foreach (ListViewItem item in lvw_altitudes.SelectedItems)
                {
                    int value = GetAltitudeValue(item) * 100;
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

                        _virtualProbePending = true;

                        var selectedLevels = lvw_altitudes.SelectedItems
                            .Cast<ListViewItem>()
                            .Select(GetAltitudeValue)
                            .ToList();
                        var proposedFl = selectedLevels.Count == 1
                            ? selectedLevels[0]
                            : sourceFdr.PRL / 100;

                        if (sourceFdr.PerformanceData?.MaxAltitude < proposedFl)
                        {
                            _virtualProbePending = false;
                            btn_response.BackColor = Color.Yellow;
                            btn_response.ForeColor = Color.Black;
                            btn_response.Text = "LOGIC";
                            btn_send.Enabled = false;
                            btn_vhf.Enabled = false;
                            SyncCancelButtonState();
                            return;
                        }

                        if (!ProposedProfileBridge.TryBeginProbe(sourceFdr.Callsign))
                        {
                            _virtualProbePending = false;
                            SyncCancelButtonState();
                            return;
                        }
                        _overrideActive = false;
                        btn_probe.Enabled = false;
                        SyncCancelButtonState();
                        btn_response.BackColor = Color.LightGray;
                        btn_response.ForeColor = Color.Black;

                        RequestVirtualProbe(sourceFdr.Callsign, proposedFl);
                    }

                    catch
                    {
                        _virtualProbePending = false;
                        ProposedProfileBridge.Clear(sourceFdr.Callsign);
                        SyncCancelButtonState();
                        btn_response.Text = "ERROR";
                        btn_response.BackColor = Color.Red;
                        btn_response.ForeColor = Color.Yellow;
                    }

                }
            }
        }

        private void btn_search_Click(object sender, EventArgs e)
        {
            if (!(source is FDP2.FDR sourceFdr) || _searchInProgress)
                return;

            if (HasSharedProbeState())
            {
                SyncCancelButtonState();
                return;
            }

            ResetSearchResults();
            _searchInProgress = true;
            _pendingSearchLevels = new Queue<int>(lvw_altitudes.Items.Cast<ListViewItem>().Select(GetAltitudeValue));
            _currentSearchLevel = null;
            btn_response.Text = "SEARCH";
            btn_response.BackColor = Color.LightGray;
            btn_response.ForeColor = Color.Black;
            SetButtonsEnabledForSearch(false);
            SyncCancelButtonState();
            RequestNextSearchProbe(sourceFdr);
        }

        private void btn_unable_Click(object sender, EventArgs e)
        {
            if (!(source is FDP2.FDR sourceFdr) || !_replyDownlinkMessageId.HasValue)
                return;

            try
            {
                CpdlcPluginBridge.SendUnable(_replyDownlinkMessageId.Value, sourceFdr.Callsign, "DUE TO TRAFFIC");
                _virtualProbePending = false;
                _overrideActive = false;
                btn_response.Text = "UNABLE";
                btn_response.BackColor = Color.Yellow;
                btn_response.ForeColor = Color.Black;
                btn_unable.Enabled = false;
            }
            catch
            {
                btn_response.Text = "ERROR";
                btn_response.BackColor = Color.Red;
                btn_response.ForeColor = Color.Yellow;
            }
        }

        private void btn_cancel_Click(object sender, EventArgs e)
        {
            ProbeRouteRenderer.HideForTrack(this.datablock as Track);
            if (source is FDP2.FDR sourceFdr)
                ProposedProfileBridge.Clear(sourceFdr.Callsign);
            _searchInProgress = false;
            _pendingSearchLevels.Clear();
            _currentSearchLevel = null;
            ResetSearchResults();
            btn_probe.Enabled = true;
            btn_probe.Text = "Probe";
            btn_response.Text = "-";
            _virtualProbePending = false;
            _overrideActive = false;
            _conflictBlocked = false;
            btn_send.Enabled = true;
            btn_vhf.Enabled = true;
            btn_close.Enabled = true;
            UpdateSearchButtonState();
            SyncCancelButtonState();
        }

        private void btn_probe_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _conflictBlocked && !_overrideActive)
            {
                _overrideActive = true;
                _conflictBlocked = false;
                btn_send.Enabled = true;
                btn_vhf.Enabled = true;
                btn_response.Text = "OVERRIDE";
                btn_response.BackColor = Color.Yellow;
                btn_response.ForeColor = Color.Black;
            }
        }
        private void lvw_altitudes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvw_altitudes.SelectedItems.Count == 1)
            {
                fld_level.Text = FormatAltitude(GetAltitudeValue(lvw_altitudes.SelectedItems[0]));
            }
            else if (lvw_altitudes.SelectedItems.Count > 1)
            {
                int lowest = GetAltitudeValue(lvw_altitudes.SelectedItems[0]);
                int highest = GetAltitudeValue(lvw_altitudes.SelectedItems[0]);

                foreach (ListViewItem item in lvw_altitudes.SelectedItems)
                {
                    int value = GetAltitudeValue(item);
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
                    ScrollToItem(item.Index);
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
                        int altitude = GetAltitudeValue(item);
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
                    if (TryParseAltitude(text, out int altitude) && GetAltitudeValue(item) == altitude)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        ScrollToItem(item.Index);
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

        // Crossing Arrow cursor while hovering over or dragging the title bar (ATOP cursor form 4).
        // WM_SETCURSOR fires whenever the cursor moves, including both hover and during the modal
        // move loop — unlike WM_ENTERSIZEMOVE which only reached the client-area Cursor property.
        protected override void WndProc(ref Message m)
        {
            const int WM_SETCURSOR = 0x0020;
            const int HTCAPTION = 2;
            if (m.Msg == WM_SETCURSOR && (m.LParam.ToInt32() & 0xFFFF) == HTCAPTION)
            {
                Cursor.Current = CursorManager.Move;
                m.Result = new IntPtr(1);
                return;
            }
            base.WndProc(ref m);
        }

        private void btn_close_Click(object sender, EventArgs e)
        {
            VirtualProbeResultsReceived -= OnVirtualProbeResultsReceived;
            Close();
            Dispose();
        }
    }
}