// Decompiled with JetBrains decompiler
// Type: vatsys_atop_plugin.UI.AnnotationWindow
// Assembly: vatsys-atop-plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53F96C82-A187-47C4-B3F9-41A57159448E
// Assembly location: C:\Users\domyn\Downloads\vatsys-atop-plugin.dll

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

#nullable disable
namespace vatsys_atop_plugin.UI
{
  public class AnnotationWindow : BaseForm
  {
    private object source;
    private IContainer components = (IContainer) null;
    private TextField fld_annot;
    private GenericButton btn_cancel;
    private GenericButton btn_apply;
    private GenericButton btn_clear;
    private TextLabel lbl_remark;

    public AnnotationWindow(FDP2.FDR sourcefdr)
    {
      this.InitializeComponent();
      this.source = (object) sourcefdr;
      this.Text = ((FDP2.FDR) this.source).Callsign.ToUpper() + " - " + ((FDP2.FDR) this.source).AircraftType;
      this.StartPosition = FormStartPosition.Manual;
      Size size = Screen.PrimaryScreen.WorkingArea.Size;
      this.Location = new Point(size.Width / 2 - this.Width / 2, size.Height / 2 - this.Height / 2);
      this.TextFieldState();
    }

    public static void Handle(CustomLabelItemMouseClickEventArgs eventArgs)
    {
      AnnotationWindow annotationWindow = new AnnotationWindow(eventArgs.Track.GetFDR());
      if (eventArgs.Button == CustomLabelItemMouseButton.Right)
        MMI.InvokeOnGUI(new MethodInvoker(((Control) annotationWindow).Show));
      eventArgs.Handled = true;
    }

    private void TextFieldState() => this.fld_annot.Text = ((FDP2.FDR) this.source).LocalOpData;

    private void btn_apply_Click(object sender, EventArgs e)
    {
      ((FDP2.FDR) this.source).LocalOpData = this.fld_annot.Text;
      this.Close();
    }

    private void btn_clear_Click(object sender, EventArgs e) => this.fld_annot.Clear();

    private void btn_cancel_Click(object sender, EventArgs e) => this.Close();

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      this.fld_annot = new TextField();
      this.btn_cancel = new GenericButton();
      this.btn_apply = new GenericButton();
      this.btn_clear = new GenericButton();
      this.lbl_remark = new TextLabel();
      this.SuspendLayout();
      this.fld_annot.AcceptsReturn = true;
      this.fld_annot.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      this.fld_annot.AutoCompleteMode = AutoCompleteMode.Append;
      this.fld_annot.AutoCompleteSource = AutoCompleteSource.CustomSource;
      this.fld_annot.BackColor = SystemColors.ControlDark;
      this.fld_annot.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.fld_annot.ForeColor = SystemColors.ControlDark;
      this.fld_annot.Location = new Point(2, 24);
      this.fld_annot.MaximumSize = new Size(500, 100);
      this.fld_annot.MaxLength = 192;
      this.fld_annot.MinimumSize = new Size(300, 50);
      this.fld_annot.Multiline = true;
      this.fld_annot.Name = "fld_annot";
      this.fld_annot.NumericCharOnly = false;
      this.fld_annot.OctalOnly = false;
      this.fld_annot.ScrollBars = ScrollBars.Vertical;
      this.fld_annot.Size = new Size(471, 50);
      this.fld_annot.TabIndex = 58;
      this.btn_cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_cancel.FlatAppearance.BorderSize = 5;
      this.btn_cancel.FlatStyle = FlatStyle.Popup;
      this.btn_cancel.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_cancel.Location = new Point(396, 80);
      this.btn_cancel.Name = "btn_cancel";
      this.btn_cancel.Padding = new Padding(3);
      this.btn_cancel.Size = new Size(68, 30);
      this.btn_cancel.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_cancel.SubText = "";
      this.btn_cancel.TabIndex = 59;
      this.btn_cancel.Text = "Cancel";
      this.btn_cancel.UseVisualStyleBackColor = false;
      this.btn_cancel.Click += new EventHandler(this.btn_cancel_Click);
      this.btn_apply.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_apply.FlatAppearance.BorderSize = 5;
      this.btn_apply.FlatStyle = FlatStyle.Popup;
      this.btn_apply.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_apply.Location = new Point(12, 80);
      this.btn_apply.Name = "btn_apply";
      this.btn_apply.Padding = new Padding(3);
      this.btn_apply.Size = new Size(68, 30);
      this.btn_apply.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_apply.SubText = "";
      this.btn_apply.TabIndex = 60;
      this.btn_apply.Text = "Apply";
      this.btn_apply.UseVisualStyleBackColor = false;
      this.btn_apply.Click += new EventHandler(this.btn_apply_Click);
      this.btn_clear.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
      this.btn_clear.FlatAppearance.BorderSize = 5;
      this.btn_clear.FlatStyle = FlatStyle.Popup;
      this.btn_clear.Font = new Font("Terminus (TTF)", 12f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.btn_clear.Location = new Point(202, 80);
      this.btn_clear.Name = "btn_clear";
      this.btn_clear.Padding = new Padding(3);
      this.btn_clear.Size = new Size(68, 30);
      this.btn_clear.SubFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point, (byte) 0);
      this.btn_clear.SubText = "";
      this.btn_clear.TabIndex = 61;
      this.btn_clear.Text = "Clear";
      this.btn_clear.UseVisualStyleBackColor = false;
      this.btn_clear.Click += new EventHandler(this.btn_clear_Click);
      this.lbl_remark.BackColor = SystemColors.Control;
      this.lbl_remark.Font = new Font("Terminus (TTF)", 10f, FontStyle.Bold, GraphicsUnit.Point, (byte) 0);
      this.lbl_remark.ForeColor = SystemColors.ControlDark;
      this.lbl_remark.HasBorder = false;
      this.lbl_remark.InteractiveText = true;
      this.lbl_remark.Location = new Point(8, -3);
      this.lbl_remark.Name = "lbl_remark";
      this.lbl_remark.Size = new Size(174, 24);
      this.lbl_remark.TabIndex = 62;
      this.lbl_remark.Text = "Annotate Remark:";
      this.lbl_remark.TextAlign = ContentAlignment.MiddleLeft;
      this.AutoScaleDimensions = new SizeF(8f, 17f);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.BackColor = SystemColors.Control;
      this.ClientSize = new Size(476, 114);
      this.Controls.Add((Control) this.lbl_remark);
      this.Controls.Add((Control) this.btn_clear);
      this.Controls.Add((Control) this.btn_apply);
      this.Controls.Add((Control) this.btn_cancel);
      this.Controls.Add((Control) this.fld_annot);
      this.FormBorderStyle = FormBorderStyle.Fixed3D;
      this.HasCloseButton = false;
      this.HasMinimizeButton = false;
      this.HideOnClose = true;
      this.Name = nameof (AnnotationWindow);
      this.StartPosition = FormStartPosition.CenterScreen;
      this.TopMost = true;
      this.ResumeLayout(false);
      this.PerformLayout();
    }
  }
}
