// Decompiled with JetBrains decompiler
// Type: vatsys_atop_plugin.UI.AltitudeWindow
// Assembly: vatsys-atop-plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53F96C82-A187-47C4-B3F9-41A57159448E
// Assembly location: C:\Users\domyn\Downloads\vatsys-atop-plugin.dll

using AtopPlugin;
using AtopPlugin.Conflict;
using AtopPlugin.Models;
using AtopPlugin.State;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

#nullable disable
namespace vatsys_atop_plugin.UI
{
  public class AltitudeWindow : BaseForm
  {
    private object conflict;
    private object source;
    private object datablock;
    private static AltitudeWindow instance;
    private AtopAircraftDisplayState display;
    private IContainer components = (IContainer) null;
    private FDP2.FDR sourcefdr;
    private Track dataBlock;
    private CheckBox climbByCheck;
    private TextBox byTime;
    private VATSYSControls.ScrollBar scrollBar1;
    private GenericButton btn_close;
    private GenericButton btn_cancel;
    private GenericButton btn_vhf;
    private GenericButton btn_send;
    private GenericButton btn_probe;
    private TextField fld_level;
    private TextField fld_time;
    private GenericButton btn_search;
    private TextLabel lbl_call;
    private ListView lvw_altitudes;
    private GenericButton btn_response;

    private AltitudeWindow(FDP2.FDR sourcefdr, Track dataBlock)
    {
      this.InitializeComponent();
      this.datablock = (object) dataBlock;
      this.source = (object) sourcefdr;
      this.UpdateSearchButtonState();
      this.ControlButtonState();
      this.AltitudeListViewState();
      this.ClimbByCheckState();
      this.lbl_call.Text = ((FDP2.FDR) this.source).Callsign.ToUpper();
      this.StartPosition = FormStartPosition.Manual;
      this.Location = Cursor.Position;
    }

    public static AltitudeWindow GetInstance(FDP2.FDR sourcefdr, Track dataBlock)
    {
      if (AltitudeWindow.instance == null || AltitudeWindow.instance.IsDisposed)
      {
        AltitudeWindow.instance = new AltitudeWindow(sourcefdr, dataBlock);
        AltitudeWindow.instance.TopMost = true;
      }
      else
        AltitudeWindow.instance.TopMost = true;
      return AltitudeWindow.instance;
    }

    private void PopulateAltitudeListView()
    {
      if (this.lvw_altitudes.Columns.Count == 0)
        this.lvw_altitudes.Columns.Add("Altitude", 100, HorizontalAlignment.Center);
      try
      {
        for (int index = 0; index <= 600; index += 10)
          this.lvw_altitudes.Items.Add(index == 600 ? "600" : index.ToString("D3"));
      }
      catch (Exception ex)
      {
      }
    }

    public static void Handle(CustomLabelItemMouseClickEventArgs eventArgs)
    {
      AltitudeWindow instance = AltitudeWindow.GetInstance(eventArgs.Track.GetFDR(), eventArgs.Track);
      if (eventArgs.Button == CustomLabelItemMouseButton.Right)
        MMI.InvokeOnGUI(new MethodInvoker(((Control) instance).Show));
      eventArgs.Handled = true;
    }

    private void MessageScroll_MouseWheel(object sender, MouseEventArgs e)
    {
      if (e.Delta > 0)
      {
        this.scrollBar1.Value -= this.scrollBar1.Change;
      }
      else
      {
        if (e.Delta >= 0)
          return;
        this.scrollBar1.Value += this.scrollBar1.Change;
      }
    }

    private void scr_altitudes_Scroll(object sender, EventArgs e)
    {
    }

    private void UpdateSearchButtonState()
    {
      if (!((FDP2.FDR) this.source).IsTrackedByMe || !(((FDP2.FDR) this.source).ControllingSector.Name == "OA"))
        return;
      this.btn_search.Enabled = true;
    }

    private void AltitudeListViewState()
    {
      string cflString = ((FDP2.FDR) this.source).CFLString;
      foreach (ListViewItem listViewItem in this.lvw_altitudes.Items)
      {
        if (((FDP2.FDR) this.source).CFLUpper != -1)
        {
          if (cflString.Contains("B"))
          {
            string[] strArray = cflString.Split('B');
            int result1;
            int result2;
            if (strArray.Length == 2 && int.TryParse(strArray[0], out result1) && int.TryParse(strArray[1], out result2))
            {
              int num = int.Parse(listViewItem.Text);
              if (num >= result1 && num <= result2)
              {
                listViewItem.Selected = true;
                listViewItem.Focused = true;
                this.lvw_altitudes.EnsureVisible(listViewItem.Index);
                this.lvw_altitudes.FocusedItem = listViewItem;
              }
            }
          }
          else if (listViewItem.Text == cflString)
          {
            listViewItem.Selected = true;
            listViewItem.Focused = true;
            this.lvw_altitudes.EnsureVisible(listViewItem.Index);
            this.lvw_altitudes.FocusedItem = listViewItem;
            this.fld_time.Enabled = false;
          }
        }
        else if (((FDP2.FDR) this.source).CFLUpper == -1 && listViewItem.Text == (((FDP2.FDR) this.source).RFL / 100).ToString())
        {
          listViewItem.Selected = true;
          listViewItem.Focused = true;
          this.lvw_altitudes.EnsureVisible(listViewItem.Index);
          this.lvw_altitudes.FocusedItem = listViewItem;
        }
      }
    }

    private void ControlButtonState()
    {
      if (!((FDP2.FDR) this.source).IsTrackedByMe)
      {
        this.btn_probe.Text = "Override";
        this.btn_response.Text = "CONTROL";
        this.btn_response.BackColor = Color.Yellow;
        this.btn_send.Enabled = false;
        this.btn_cancel.Enabled = false;
        this.btn_vhf.Enabled = false;
      }
      else
      {
        this.btn_response.Text = "-";
        this.btn_cancel.Enabled = false;
      }
    }

    private void ClimbByCheckState()
    {
      this.fld_time.Enabled = false;
      TextField fldTime = this.fld_time;
      DateTime dateTime = DateTime.UtcNow;
      dateTime = dateTime.AddMinutes(20.0);
      string str = dateTime.ToString("HHmm");
      fldTime.Text = str;
    }

    private void btn_probe_Click(object sender, EventArgs e)
    {
      if (this.lvw_altitudes.SelectedItems.Count > 0)
      {
        ListViewItem selectedItem1 = this.lvw_altitudes.SelectedItems[0];
        int lowerLevel = int.Parse(this.lvw_altitudes.SelectedItems[0].Text) * 100;
        int upperLevel = int.Parse(this.lvw_altitudes.SelectedItems[0].Text) * 100;
        foreach (ListViewItem selectedItem2 in this.lvw_altitudes.SelectedItems)
        {
          int num = int.Parse(selectedItem2.Text) * 100;
          if (num < lowerLevel)
            lowerLevel = num;
          else if (num > upperLevel)
            upperLevel = num;
          try
          {
            if (this.lvw_altitudes.SelectedItems.Count >= 1 && selectedItem1.Text == ((FDP2.FDR) this.source).CFLString)
            {
              this.fld_time.Enabled = false;
              this.climbByCheck.Enabled = false;
            }
            if (this.lvw_altitudes.SelectedItems.Count > 1)
            {
              FDP2.SetCFL((FDP2.FDR) this.source, lowerLevel, upperLevel, false);
            }
            else
            {
              int result;
              if (this.lvw_altitudes.SelectedItems.Count == 1 && int.TryParse(selectedItem1.Text, out result))
                FDP2.SetCFL((FDP2.FDR) this.source, result.ToString());
            }
            ConflictProbe.ManualProbe((FDP2.FDR) this.source);
          }
          catch
          {
            this.btn_response.Text = "ERROR";
            this.btn_response.BackColor = Color.Red;
            this.btn_response.ForeColor = Color.Yellow;
          }
        }
      }
      int? maxAltitude = ((FDP2.FDR) this.source)?.PerformanceData.MaxAltitude;
      int int32 = Convert.ToInt32(this.lvw_altitudes.SelectedItems[0].Text);
      bool flag = maxAltitude.GetValueOrDefault() < int32 & maxAltitude.HasValue;
      FDP2.FDR source = (FDP2.FDR) this.source;
      ConflictProbe.Conflicts? nullable = source != null ? source.GetConflicts() : new ConflictProbe.Conflicts?();
      if (nullable.Equals((object) ConflictStatus.Imminent) || nullable.Equals((object) ConflictStatus.Actual))
      {
        this.btn_probe.Text = "Override";
        this.btn_response.BackColor = Color.Red;
        this.btn_response.ForeColor = Color.Black;
        this.btn_response.Text = "ALERT";
        this.btn_send.Enabled = false;
        this.btn_vhf.Enabled = false;
      }
      else if (nullable.Equals((object) ConflictStatus.Advisory))
      {
        this.btn_probe.Text = "Override";
        this.btn_response.BackColor = Color.Orange;
        this.btn_response.ForeColor = Color.Black;
        this.btn_response.Text = "WARN";
        this.btn_send.Enabled = false;
        this.btn_vhf.Enabled = false;
      }
      else if (flag)
      {
        this.btn_response.BackColor = Color.Yellow;
        this.btn_response.ForeColor = Color.Black;
        this.btn_response.Text = "LOGIC";
        this.btn_send.Enabled = false;
        this.btn_vhf.Enabled = false;
      }
      else
      {
        this.btn_response.BackColor = Color.FromArgb(70, 247, 57);
        this.btn_response.ForeColor = Color.Black;
        this.btn_response.Text = "OK";
      }
      this.btn_probe.Enabled = false;
      this.btn_cancel.Enabled = true;
    }

    private void btn_send_Click(object sender, EventArgs e)
    {
      string callsign = ((FDP2.FDR) this.source).Callsign;
      int prl = ((FDP2.FDR) this.source).PRL;
      int cflUpper = ((FDP2.FDR) this.source).CFLUpper;
      int cflLower = ((FDP2.FDR) this.source).CFLLower;
      string text = this.lvw_altitudes.SelectedItems[0].Text;
      bool flag = DateTime.Today.Add(TimeSpan.Parse(this.fld_time.Text)) >= DateTime.UtcNow;
      if (((MouseEventArgs) e).Button == MouseButtons.Right)
        this.btn_send.Text = "HF";
      this.btn_vhf_Click(sender, e);
      if (!Network.PrimaryFrequencySet)
      {
        Errors.Add(new Exception("No primary frequency set for CPDLC")
        {
          Source = "CPDLC"
        });
      }
      else
      {
        try
        {
          if (this.climbByCheck.Checked & flag && prl > cflUpper)
            Network.SendRadioMessage(callsign + " DESCEND TO REACH F" + text + " BY " + this.fld_time.Text + " REPORT LEVEL F" + text);
          else if (this.climbByCheck.Checked & flag && prl < cflUpper)
            Network.SendRadioMessage(callsign + " CLIMB TO REACH F" + text + " BY " + this.fld_time.Text + " REPORT LEVEL F" + text);
          else if (cflLower != -1 && cflUpper >= prl && prl >= cflLower && cflLower != cflUpper)
            Network.SendRadioMessage(callsign + " MAINTAIN BLOCK F" + text);
          else if (cflLower != -1 && prl < cflLower && cflLower != cflUpper)
            Network.SendRadioMessage(callsign + " CLIMB TO AND MAINTAIN BLOCK " + text);
          else if (cflLower != -1 && prl > cflUpper && cflLower != cflUpper)
            Network.SendRadioMessage(callsign + " DESCEND TO AND MAINTAIN BLOCK " + text);
          else if (prl > cflUpper)
            Network.SendRadioMessage(callsign + " DESCEND TO AND MAINTAIN F" + text + " REPORT LEVEL F" + text);
          else if (prl < cflUpper)
            Network.SendRadioMessage(callsign + " CLIMB TO AND MAINTAIN F" + text + " REPORT LEVELF" + text);
          else
            Network.SendRadioMessage(callsign + " MAINTAIN F" + text);
        }
        catch
        {
          this.btn_response.Text = "ERROR";
          this.btn_response.BackColor = Color.Red;
          this.btn_response.ForeColor = Color.Yellow;
        }
      }
    }

    private void btn_vhf_Click(object sender, EventArgs e)
    {
      if (this.lvw_altitudes.SelectedItems.Count > 0)
      {
        ListViewItem selectedItem1 = this.lvw_altitudes.SelectedItems[0];
        int lowerLevel = int.Parse(this.lvw_altitudes.SelectedItems[0].Text) * 100;
        int upperLevel = int.Parse(this.lvw_altitudes.SelectedItems[0].Text) * 100;
        foreach (ListViewItem selectedItem2 in this.lvw_altitudes.SelectedItems)
        {
          int num = int.Parse(selectedItem2.Text) * 100;
          if (num < lowerLevel)
            lowerLevel = num;
          else if (num > upperLevel)
            upperLevel = num;
          try
          {
            if (this.lvw_altitudes.SelectedItems.Count > 1)
            {
              FDP2.SetCFL((FDP2.FDR) this.source, lowerLevel, upperLevel, false);
            }
            else
            {
              int result;
              if (this.lvw_altitudes.SelectedItems.Count == 1 && int.TryParse(selectedItem1.Text, out result))
                FDP2.SetCFL((FDP2.FDR) this.source, result.ToString());
            }
            this.btn_probe_Click(sender, e);
          }
          catch
          {
            this.btn_response.Text = "ERROR";
            this.btn_response.BackColor = Color.Red;
            this.btn_response.ForeColor = Color.Yellow;
          }
        }
      }
      this.Close();
      this.Dispose();
    }

    private void btn_cancel_Click(object sender, EventArgs e)
    {
      MMI.HideGraphicRoute((Track) this.datablock);
      this.btn_probe.Enabled = true;
      this.btn_response.Text = "-";
      foreach (FDP2.FDR fdr in FDP2.GetFDRs.Where<FDP2.FDR>((Func<FDP2.FDR, bool>) (c => "*" + c.Callsign == ((FDP2.FDR) this.source).Callsign)))
        FDP2.DeleteFDR(fdr);
    }

    private void lvw_altitudes_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (this.lvw_altitudes.SelectedItems.Count == 1)
        this.fld_level.Text = this.lvw_altitudes.SelectedItems[0].Text;
      else if (this.lvw_altitudes.SelectedItems.Count > 1)
      {
        int num1 = int.Parse(this.lvw_altitudes.SelectedItems[0].Text);
        int num2 = int.Parse(this.lvw_altitudes.SelectedItems[0].Text);
        foreach (ListViewItem selectedItem in this.lvw_altitudes.SelectedItems)
        {
          int num3 = int.Parse(selectedItem.Text);
          if (num3 < num1)
            num1 = num3;
          else if (num3 > num2)
            num2 = num3;
          selectedItem.Selected = true;
          selectedItem.Focused = true;
          this.lvw_altitudes.EnsureVisible(selectedItem.Index);
          this.lvw_altitudes.FocusedItem = selectedItem;
        }
        this.fld_level.Text = num1.ToString() + "B" + num2.ToString();
      }
      else
        this.fld_level.Text = string.Empty;
    }

    private void fld_level_TextChanged(object sender, EventArgs e)
    {
      string upper = this.fld_level.Text.ToUpper();
      if (upper.Contains("B"))
      {
        string[] strArray = upper.Split('B');
        int result1;
        int result2;
        if (strArray.Length != 2 || !int.TryParse(strArray[0], out result1) || !int.TryParse(strArray[1], out result2))
          return;
        foreach (ListViewItem listViewItem in this.lvw_altitudes.Items)
        {
          int num = int.Parse(listViewItem.Text);
          if (num >= result1 && num <= result2)
            listViewItem.Selected = true;
        }
      }
      else
      {
        foreach (ListViewItem listViewItem in this.lvw_altitudes.Items)
        {
          if (listViewItem.Text == upper)
          {
            listViewItem.Selected = true;
            listViewItem.Focused = true;
            this.lvw_altitudes.EnsureVisible(listViewItem.Index);
            this.lvw_altitudes.FocusedItem = listViewItem;
          }
        }
      }
    }

    private void fld_level_Enter(object sender, EventArgs e)
    {
      this.lvw_altitudes.SelectedItems.Clear();
    }

    private void climbByCheck_CheckStateChanged(object sender, EventArgs e)
    {
      if (this.climbByCheck.Checked)
      {
        this.fld_time.Enabled = true;
        this.climbByCheck.ForeColor = Color.Red;
      }
      else
        this.fld_time.Enabled = false;
    }

    private void btn_close_Click(object sender, EventArgs e)
    {
      this.Close();
      this.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      ListViewItem listViewItem1 = new ListViewItem("600");
      ListViewItem listViewItem2 = new ListViewItem("590");
      ListViewItem listViewItem3 = new ListViewItem("580");
      ListViewItem listViewItem4 = new ListViewItem("570");
      ListViewItem listViewItem5 = new ListViewItem("560");
      ListViewItem listViewItem6 = new ListViewItem("550");
      ListViewItem listViewItem7 = new ListViewItem("540");
      ListViewItem listViewItem8 = new ListViewItem("530");
      ListViewItem listViewItem9 = new ListViewItem("520");
      ListViewItem listViewItem10 = new ListViewItem("510");
      ListViewItem listViewItem11 = new ListViewItem("500");
      ListViewItem listViewItem12 = new ListViewItem("490");
      ListViewItem listViewItem13 = new ListViewItem("480");
      ListViewItem listViewItem14 = new ListViewItem("470");
      ListViewItem listViewItem15 = new ListViewItem("460");
      ListViewItem listViewItem16 = new ListViewItem("450");
      ListViewItem listViewItem17 = new ListViewItem("440");
      ListViewItem listViewItem18 = new ListViewItem("430");
      ListViewItem listViewItem19 = new ListViewItem("420");
      ListViewItem listViewItem20 = new ListViewItem("410");
      ListViewItem listViewItem21 = new ListViewItem("400");
      ListViewItem listViewItem22 = new ListViewItem("390");
      ListViewItem listViewItem23 = new ListViewItem("380");
      ListViewItem listViewItem24 = new ListViewItem("370");
      ListViewItem listViewItem25 = new ListViewItem("360");
      ListViewItem listViewItem26 = new ListViewItem("350");
      ListViewItem listViewItem27 = new ListViewItem("340");
      ListViewItem listViewItem28 = new ListViewItem("330");
      ListViewItem listViewItem29 = new ListViewItem("320");
      ListViewItem listViewItem30 = new ListViewItem("310");
      ListViewItem listViewItem31 = new ListViewItem("300");
      ListViewItem listViewItem32 = new ListViewItem("290");
      ListViewItem listViewItem33 = new ListViewItem("280");
      ListViewItem listViewItem34 = new ListViewItem("270");
      ListViewItem listViewItem35 = new ListViewItem("260");
      ListViewItem listViewItem36 = new ListViewItem("250");
      ListViewItem listViewItem37 = new ListViewItem("240");
      ListViewItem listViewItem38 = new ListViewItem("230");
      ListViewItem listViewItem39 = new ListViewItem("220");
      ListViewItem listViewItem40 = new ListViewItem("210");
      ListViewItem listViewItem41 = new ListViewItem("200");
      ListViewItem listViewItem42 = new ListViewItem("190");
      ListViewItem listViewItem43 = new ListViewItem("180");
      ListViewItem listViewItem44 = new ListViewItem("170");
      ListViewItem listViewItem45 = new ListViewItem("160");
      ListViewItem listViewItem46 = new ListViewItem("150");
      ListViewItem listViewItem47 = new ListViewItem("140");
      ListViewItem listViewItem48 = new ListViewItem("130");
      ListViewItem listViewItem49 = new ListViewItem("120");
      ListViewItem listViewItem50 = new ListViewItem("110");
      ListViewItem listViewItem51 = new ListViewItem("100");
      ListViewItem listViewItem52 = new ListViewItem("090");
      ListViewItem listViewItem53 = new ListViewItem("080");
      ListViewItem listViewItem54 = new ListViewItem("070");
      ListViewItem listViewItem55 = new ListViewItem("060");
      ListViewItem listViewItem56 = new ListViewItem("050");
      ListViewItem listViewItem57 = new ListViewItem("040");
      ListViewItem listViewItem58 = new ListViewItem("030");
      ListViewItem listViewItem59 = new ListViewItem("025");
      ListViewItem listViewItem60 = new ListViewItem("020");
      ListViewItem listViewItem61 = new ListViewItem("015");
      ListViewItem listViewItem62 = new ListViewItem("010");
      ListViewItem listViewItem63 = new ListViewItem("005");
      ListViewItem listViewItem64 = new ListViewItem("000");
      this.climbByCheck = new CheckBox();
      this.byTime = new TextBox();
      this.scrollBar1 = new VATSYSControls.ScrollBar();
      this.btn_close = new GenericButton();
      this.btn_response = new GenericButton();
      this.btn_cancel = new GenericButton();
      this.btn_vhf = new GenericButton();
      this.btn_send = new GenericButton();
      this.btn_probe = new GenericButton();
      this.btn_search = new GenericButton();
      this.fld_level = new TextField();
      this.fld_time = new TextField();
      this.lbl_call = new TextLabel();
      this.lvw_altitudes = new ListView();
      ColumnHeader columnHeader = new ColumnHeader();
      this.SuspendLayout();
      this.climbByCheck.AutoSize = true;
      this.climbByCheck.FlatStyle = FlatStyle.Popup;
      this.climbByCheck.Location = new Point(5, 215);
      this.climbByCheck.Name = "climbByCheck";
      this.climbByCheck.Size = new Size(18, 17);
      this.climbByCheck.TabIndex = 7;
      this.climbByCheck.UseVisualStyleBackColor = true;
      this.climbByCheck.CheckStateChanged += new EventHandler(this.climbByCheck_CheckStateChanged);
      this.byTime.BackColor = SystemColors.Control;
      this.byTime.BorderStyle = BorderStyle.None;
      this.byTime.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.byTime.Location = new Point(23, 213);
      this.byTime.Margin = new Padding(0);
      this.byTime.Name = "byTime";
      this.byTime.Size = new Size(90, 27);
      this.byTime.TabIndex = 8;
      this.byTime.Text = "BY TIME";
      this.scrollBar1.ActualHeight = 8;
      this.scrollBar1.Change = 1;
      this.scrollBar1.ForeColor = SystemColors.Control;
      this.scrollBar1.Location = new Point(85, 25);
      this.scrollBar1.MinimumSize = new Size(0, -4);
      this.scrollBar1.Name = "scrollBar1";
      this.scrollBar1.Orientation = ScrollOrientation.VerticalScroll;
      this.scrollBar1.PercentageValue = 0.0f;
      this.scrollBar1.PreferredHeight = 8;
      this.scrollBar1.Size = new Size(23, 144);
      this.scrollBar1.TabIndex = 49;
      this.scrollBar1.Value = 0;
      this.scrollBar1.Visible = false;
      this.scrollBar1.Scroll += new EventHandler(this.scr_altitudes_Scroll);
      this.scrollBar1.Scrolling += new EventHandler(this.scr_altitudes_Scroll);
      this.scrollBar1.MouseWheel += new MouseEventHandler(this.MessageScroll_MouseWheel);
      this.btn_close.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_close.FlatAppearance.BorderSize = 5;
      this.btn_close.FlatStyle = FlatStyle.Popup;
      this.btn_close.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_close.Location = new Point(114, 176);
      this.btn_close.Name = "btn_close";
      this.btn_close.Padding = new Padding(3);
      this.btn_close.Size = new Size(77, 24);
      this.btn_close.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_close.SubText = "";
      this.btn_close.TabIndex = 50;
      this.btn_close.Text = "Close";
      this.btn_close.UseVisualStyleBackColor = false;
      this.btn_close.Click += new EventHandler(this.btn_close_Click);
      this.btn_response.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_response.FlatAppearance.BorderColor = SystemColors.ActiveCaptionText;
      this.btn_response.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_response.Location = new Point(114, 55);
      this.btn_response.Margin = new Padding(3, 3, 0, 0);
      this.btn_response.Name = "btn_response";
      this.btn_response.Padding = new Padding(3);
      this.btn_response.Size = new Size(77, 24);
      this.btn_response.SubFont = new Font("Microsoft Sans Serif", 8f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_response.SubText = "";
      this.btn_response.TabIndex = 51;
      this.btn_response.Text = "-";
      this.btn_response.UseVisualStyleBackColor = false;
      this.btn_cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_cancel.FlatAppearance.BorderSize = 5;
      this.btn_cancel.FlatStyle = FlatStyle.Popup;
      this.btn_cancel.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_cancel.Location = new Point(114, 115);
      this.btn_cancel.Name = "btn_cancel";
      this.btn_cancel.Padding = new Padding(3);
      this.btn_cancel.Size = new Size(77, 24);
      this.btn_cancel.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_cancel.SubText = "";
      this.btn_cancel.TabIndex = 52;
      this.btn_cancel.Text = "Cancel";
      this.btn_cancel.UseVisualStyleBackColor = false;
      this.btn_cancel.Click += new EventHandler(this.btn_cancel_Click);
      this.btn_vhf.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_vhf.FlatAppearance.BorderSize = 5;
      this.btn_vhf.FlatStyle = FlatStyle.Popup;
      this.btn_vhf.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_vhf.Location = new Point(114, 145);
      this.btn_vhf.Name = "btn_vhf";
      this.btn_vhf.Padding = new Padding(3);
      this.btn_vhf.Size = new Size(77, 24);
      this.btn_vhf.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_vhf.SubText = "";
      this.btn_vhf.TabIndex = 53;
      this.btn_vhf.Text = "VHF";
      this.btn_vhf.UseVisualStyleBackColor = false;
      this.btn_vhf.Click += new EventHandler(this.btn_vhf_Click);
      this.btn_send.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_send.FlatAppearance.BorderSize = 5;
      this.btn_send.FlatStyle = FlatStyle.Popup;
      this.btn_send.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_send.Location = new Point(114, 85);
      this.btn_send.Name = "btn_send";
      this.btn_send.Padding = new Padding(3);
      this.btn_send.Size = new Size(77, 24);
      this.btn_send.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_send.SubText = "";
      this.btn_send.TabIndex = 54;
      this.btn_send.Text = "Send";
      this.btn_send.UseVisualStyleBackColor = false;
      this.btn_send.Click += new EventHandler(this.btn_send_Click);
      this.btn_probe.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_probe.FlatAppearance.BorderSize = 5;
      this.btn_probe.FlatStyle = FlatStyle.Popup;
      this.btn_probe.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_probe.Location = new Point(114, 25);
      this.btn_probe.Name = "btn_probe";
      this.btn_probe.Padding = new Padding(3);
      this.btn_probe.Size = new Size(77, 24);
      this.btn_probe.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_probe.SubText = "";
      this.btn_probe.TabIndex = 55;
      this.btn_probe.Text = "Probe";
      this.btn_probe.UseVisualStyleBackColor = false;
      this.btn_probe.Click += new EventHandler(this.btn_probe_Click);
      this.btn_search.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_search.Enabled = false;
      this.btn_search.FlatAppearance.BorderSize = 5;
      this.btn_search.FlatStyle = FlatStyle.Popup;
      this.btn_search.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_search.Location = new Point(114, -2);
      this.btn_search.Name = "btn_search";
      this.btn_search.Padding = new Padding(3);
      this.btn_search.Size = new Size(77, 24);
      this.btn_search.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_search.SubText = "";
      this.btn_search.TabIndex = 56;
      this.btn_search.Text = "SEARCH";
      this.btn_search.UseVisualStyleBackColor = false;
      this.fld_level.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      this.fld_level.AutoCompleteMode = AutoCompleteMode.Append;
      this.fld_level.AutoCompleteSource = AutoCompleteSource.CustomSource;
      this.fld_level.BackColor = SystemColors.ControlDark;
      this.fld_level.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.fld_level.ForeColor = SystemColors.ControlDark;
      this.fld_level.Location = new Point(5, 177);
      this.fld_level.MaxLength = 7;
      this.fld_level.Name = "fld_level";
      this.fld_level.NumericCharOnly = false;
      this.fld_level.OctalOnly = false;
      this.fld_level.Size = new Size(103, 34);
      this.fld_level.TabIndex = 57;
      this.fld_level.TextChanged += new EventHandler(this.fld_level_TextChanged);
      this.fld_level.Enter += new EventHandler(this.fld_level_Enter);
      this.fld_time.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      this.fld_time.AutoCompleteMode = AutoCompleteMode.Append;
      this.fld_time.AutoCompleteSource = AutoCompleteSource.CustomSource;
      this.fld_time.BackColor = SystemColors.ControlDark;
      this.fld_time.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.fld_time.ForeColor = SystemColors.ControlDark;
      this.fld_time.Location = new Point(114, 206);
      this.fld_time.MaxLength = 4;
      this.fld_time.Name = "fld_time";
      this.fld_time.NumericCharOnly = true;
      this.fld_time.OctalOnly = false;
      this.fld_time.Size = new Size(51, 34);
      this.fld_time.TabIndex = 59;
      this.lbl_call.BackColor = SystemColors.Control;
      this.lbl_call.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.lbl_call.ForeColor = SystemColors.ControlDark;
      this.lbl_call.HasBorder = false;
      this.lbl_call.InteractiveText = true;
      this.lbl_call.Location = new Point(13, -2);
      this.lbl_call.Name = "lbl_call";
      this.lbl_call.Size = new Size(72, 24);
      this.lbl_call.TabIndex = 60;
      this.lbl_call.Text = "callsign";
      this.lbl_call.TextAlign = ContentAlignment.MiddleLeft;
      this.lvw_altitudes.BackColor = SystemColors.Control;
      this.lvw_altitudes.Columns.AddRange(new ColumnHeader[1]
      {
        columnHeader
      });
      this.lvw_altitudes.FullRowSelect = true;
      this.lvw_altitudes.HeaderStyle = ColumnHeaderStyle.None;
      this.lvw_altitudes.HideSelection = false;
      this.lvw_altitudes.Items.AddRange(new ListViewItem[64]
      {
        listViewItem1,
        listViewItem2,
        listViewItem3,
        listViewItem4,
        listViewItem5,
        listViewItem6,
        listViewItem7,
        listViewItem8,
        listViewItem9,
        listViewItem10,
        listViewItem11,
        listViewItem12,
        listViewItem13,
        listViewItem14,
        listViewItem15,
        listViewItem16,
        listViewItem17,
        listViewItem18,
        listViewItem19,
        listViewItem20,
        listViewItem21,
        listViewItem22,
        listViewItem23,
        listViewItem24,
        listViewItem25,
        listViewItem26,
        listViewItem27,
        listViewItem28,
        listViewItem29,
        listViewItem30,
        listViewItem31,
        listViewItem32,
        listViewItem33,
        listViewItem34,
        listViewItem35,
        listViewItem36,
        listViewItem37,
        listViewItem38,
        listViewItem39,
        listViewItem40,
        listViewItem41,
        listViewItem42,
        listViewItem43,
        listViewItem44,
        listViewItem45,
        listViewItem46,
        listViewItem47,
        listViewItem48,
        listViewItem49,
        listViewItem50,
        listViewItem51,
        listViewItem52,
        listViewItem53,
        listViewItem54,
        listViewItem55,
        listViewItem56,
        listViewItem57,
        listViewItem58,
        listViewItem59,
        listViewItem60,
        listViewItem61,
        listViewItem62,
        listViewItem63,
        listViewItem64
      });
      this.lvw_altitudes.Location = new Point(5, 25);
      this.lvw_altitudes.Margin = new Padding(3, 3, 20, 3);
      this.lvw_altitudes.Name = "lvw_altitudes";
      this.lvw_altitudes.Size = new Size(103, 143);
      this.lvw_altitudes.TabIndex = 2;
      this.lvw_altitudes.UseCompatibleStateImageBehavior = false;
      this.lvw_altitudes.View = View.Details;
      this.lvw_altitudes.SelectedIndexChanged += new EventHandler(this.lvw_altitudes_SelectedIndexChanged);
      this.lvw_altitudes.MouseWheel += new MouseEventHandler(this.MessageScroll_MouseWheel);
      this.BackColor = SystemColors.Control;
      this.ClientSize = new Size(194, 238);
      this.Controls.Add((Control) this.lvw_altitudes);
      this.Controls.Add((Control) this.lbl_call);
      this.Controls.Add((Control) this.fld_time);
      this.Controls.Add((Control) this.fld_level);
      this.Controls.Add((Control) this.btn_search);
      this.Controls.Add((Control) this.btn_probe);
      this.Controls.Add((Control) this.btn_send);
      this.Controls.Add((Control) this.btn_vhf);
      this.Controls.Add((Control) this.btn_cancel);
      this.Controls.Add((Control) this.btn_response);
      this.Controls.Add((Control) this.btn_close);
      this.Controls.Add((Control) this.scrollBar1);
      this.Controls.Add((Control) this.byTime);
      this.Controls.Add((Control) this.climbByCheck);
      this.ForeColor = SystemColors.ActiveCaptionText;
      this.FormBorderStyle = FormBorderStyle.Fixed3D;
      this.HasCloseButton = false;
      this.HasMinimizeButton = false;
      this.HideOnClose = true;
      this.Name = nameof (AltitudeWindow);
      this.StartPosition = FormStartPosition.Manual;
      this.TopMost = true;
      this.ResumeLayout(false);
      this.PerformLayout();
    }
  }
}
