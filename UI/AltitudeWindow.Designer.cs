using System.Windows.Forms;
using vatsys;

namespace vatsys_atop_plugin.UI
{
    partial class AltitudeWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>

        #endregion
        private void InitializeComponent()
        {
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.byTime = new System.Windows.Forms.TextBox();
            this.scrollBar1 = new VATSYSControls.ScrollBar();
            this.btn_close = new vatsys.GenericButton();
            this.btn_response = new vatsys.GenericButton();
            this.btn_cancel = new vatsys.GenericButton();
            this.btn_vhf = new vatsys.GenericButton();
            this.btn_send = new vatsys.GenericButton();
            this.btn_probe = new vatsys.GenericButton();
            this.btn_search = new vatsys.GenericButton();
            this.fld_level = new vatsys.TextField();
            this.textField1 = new vatsys.TextField();
            this.lbl_call = new vatsys.TextLabel();
            this.lvw_altitudes = new vatsys.ListViewEx();
            this.SuspendLayout();
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.checkBox1.Location = new System.Drawing.Point(5, 215);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(18, 17);
            this.checkBox1.TabIndex = 7;
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // byTime
            // 
            this.byTime.BackColor = System.Drawing.SystemColors.ControlDark;
            this.byTime.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.byTime.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.byTime.Location = new System.Drawing.Point(23, 213);
            this.byTime.Margin = new System.Windows.Forms.Padding(0);
            this.byTime.Name = "byTime";
            this.byTime.Size = new System.Drawing.Size(90, 27);
            this.byTime.TabIndex = 8;
            this.byTime.Text = "BY TIME";
            // 
            // scrollBar1
            // 
            this.scrollBar1.ActualHeight = 8;
            this.scrollBar1.Change = 1;
            this.scrollBar1.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.scrollBar1.Location = new System.Drawing.Point(85, 25);
            this.scrollBar1.MinimumSize = new System.Drawing.Size(0, -4);
            this.scrollBar1.Name = "scrollBar1";
            this.scrollBar1.Orientation = System.Windows.Forms.ScrollOrientation.VerticalScroll;
            this.scrollBar1.PercentageValue = 0F;
            this.scrollBar1.PreferredHeight = 8;
            this.scrollBar1.Size = new System.Drawing.Size(23, 144);
            this.scrollBar1.TabIndex = 49;
            this.scrollBar1.Value = 0;
            this.scrollBar1.Scroll += new System.EventHandler(this.scr_altitudes_Scroll);
            this.scrollBar1.Scrolling += new System.EventHandler(this.scr_altitudes_Scroll);
            this.scrollBar1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.MessageScroll_MouseWheel);
            // 
            // btn_close
            // 
            this.btn_close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_close.FlatAppearance.BorderSize = 5;
            this.btn_close.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_close.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_close.Location = new System.Drawing.Point(114, 176);
            this.btn_close.Name = "btn_close";
            this.btn_close.Padding = new System.Windows.Forms.Padding(3);
            this.btn_close.Size = new System.Drawing.Size(77, 24);
            this.btn_close.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_close.SubText = "";
            this.btn_close.TabIndex = 50;
            this.btn_close.Text = "Close";
            this.btn_close.UseVisualStyleBackColor = false;
            this.btn_close.Click += new System.EventHandler(this.btn_close_Click);
            // 
            // btn_response
            // 
            this.btn_response.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_response.FlatAppearance.BorderSize = 5;
            this.btn_response.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_response.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_response.Location = new System.Drawing.Point(114, 55);
            this.btn_response.Margin = new System.Windows.Forms.Padding(3, 3, 0, 0);
            this.btn_response.Name = "btn_response";
            this.btn_response.Padding = new System.Windows.Forms.Padding(3);
            this.btn_response.Size = new System.Drawing.Size(77, 24);
            this.btn_response.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_response.SubText = "";
            this.btn_response.TabIndex = 51;
            this.btn_response.Text = "-";
            this.btn_response.UseVisualStyleBackColor = false;
            // 
            // btn_cancel
            // 
            this.btn_cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_cancel.FlatAppearance.BorderSize = 5;
            this.btn_cancel.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_cancel.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_cancel.Location = new System.Drawing.Point(114, 115);
            this.btn_cancel.Name = "btn_cancel";
            this.btn_cancel.Padding = new System.Windows.Forms.Padding(3);
            this.btn_cancel.Size = new System.Drawing.Size(77, 24);
            this.btn_cancel.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_cancel.SubText = "";
            this.btn_cancel.TabIndex = 52;
            this.btn_cancel.Text = "Cancel";
            this.btn_cancel.UseVisualStyleBackColor = false;
            // 
            // btn_vhf
            // 
            this.btn_vhf.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_vhf.FlatAppearance.BorderSize = 5;
            this.btn_vhf.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_vhf.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_vhf.Location = new System.Drawing.Point(114, 145);
            this.btn_vhf.Name = "btn_vhf";
            this.btn_vhf.Padding = new System.Windows.Forms.Padding(3);
            this.btn_vhf.Size = new System.Drawing.Size(77, 24);
            this.btn_vhf.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_vhf.SubText = "";
            this.btn_vhf.TabIndex = 53;
            this.btn_vhf.Text = "VHF";
            this.btn_vhf.UseVisualStyleBackColor = false;
            this.btn_vhf.Click += new System.EventHandler(this.btn_vhf_Click);
            // 
            // btn_send
            // 
            this.btn_send.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_send.FlatAppearance.BorderSize = 5;
            this.btn_send.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_send.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_send.Location = new System.Drawing.Point(114, 85);
            this.btn_send.Name = "btn_send";
            this.btn_send.Padding = new System.Windows.Forms.Padding(3);
            this.btn_send.Size = new System.Drawing.Size(77, 24);
            this.btn_send.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_send.SubText = "";
            this.btn_send.TabIndex = 54;
            this.btn_send.Text = "Send";
            this.btn_send.UseVisualStyleBackColor = false;
            this.btn_send.Click += new System.EventHandler(this.btn_send_Click);
            // 
            // btn_probe
            // 
            this.btn_probe.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_probe.FlatAppearance.BorderSize = 5;
            this.btn_probe.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_probe.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_probe.Location = new System.Drawing.Point(114, 25);
            this.btn_probe.Name = "btn_probe";
            this.btn_probe.Padding = new System.Windows.Forms.Padding(3);
            this.btn_probe.Size = new System.Drawing.Size(77, 24);
            this.btn_probe.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_probe.SubText = "";
            this.btn_probe.TabIndex = 55;
            this.btn_probe.Text = "Probe";
            this.btn_probe.UseVisualStyleBackColor = false;
            this.btn_probe.Click += new System.EventHandler(this.btn_probe_Click);
            // 
            // btn_search
            // 
            this.btn_search.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_search.Enabled = false;
            this.btn_search.FlatAppearance.BorderSize = 5;
            this.btn_search.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_search.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_search.Location = new System.Drawing.Point(114, -2);
            this.btn_search.Name = "btn_search";
            this.btn_search.Padding = new System.Windows.Forms.Padding(3);
            this.btn_search.Size = new System.Drawing.Size(77, 24);
            this.btn_search.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_search.SubText = "";
            this.btn_search.TabIndex = 56;
            this.btn_search.Text = "SEARCH";
            this.btn_search.UseVisualStyleBackColor = false;
            // 
            // fld_level
            // 
            this.fld_level.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.fld_level.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.fld_level.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.fld_level.BackColor = System.Drawing.SystemColors.ControlDark;
            this.fld_level.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.fld_level.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.fld_level.Location = new System.Drawing.Point(5, 177);
            this.fld_level.MaxLength = 7;
            this.fld_level.Name = "fld_level";
            this.fld_level.NumericCharOnly = false;
            this.fld_level.OctalOnly = false;
            this.fld_level.Size = new System.Drawing.Size(103, 34);
            this.fld_level.TabIndex = 57;
            // 
            // textField1
            // 
            this.textField1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textField1.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.textField1.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.textField1.BackColor = System.Drawing.SystemColors.ControlDark;
            this.textField1.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textField1.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.textField1.Location = new System.Drawing.Point(114, 206);
            this.textField1.MaxLength = 4;
            this.textField1.Name = "textField1";
            this.textField1.NumericCharOnly = false;
            this.textField1.OctalOnly = false;
            this.textField1.Size = new System.Drawing.Size(51, 34);
            this.textField1.TabIndex = 59;
            // 
            // lbl_call
            // 
            this.lbl_call.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.lbl_call.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbl_call.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.lbl_call.HasBorder = false;
            this.lbl_call.InteractiveText = true;
            this.lbl_call.Location = new System.Drawing.Point(2, -2);
            this.lbl_call.Name = "lbl_call";
            this.lbl_call.Size = new System.Drawing.Size(72, 24);
            this.lbl_call.TabIndex = 60;
            this.lbl_call.Text = "callsign";
            this.lbl_call.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lvw_altitudes
            // 
            this.lvw_altitudes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvw_altitudes.BackColor = System.Drawing.SystemColors.ControlDark;
            this.lvw_altitudes.Font = new System.Drawing.Font("Terminus (TTF)", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.lvw_altitudes.FullRowSelect = true;
            this.lvw_altitudes.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvw_altitudes.HideSelection = false;
            this.lvw_altitudes.Location = new System.Drawing.Point(5, 25);
            this.lvw_altitudes.Name = "lvw_altitudes";
            this.lvw_altitudes.OwnerDraw = true;
            this.lvw_altitudes.ShowGroups = false;
            this.lvw_altitudes.Size = new System.Drawing.Size(79, 144);
            this.lvw_altitudes.TabIndex = 3;
            this.lvw_altitudes.UseCompatibleStateImageBehavior = false;
            this.lvw_altitudes.View = System.Windows.Forms.View.List;
            this.lvw_altitudes.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.MessageScroll_MouseWheel);
            // 
            // AltitudeWindow
            // 
            this.BackColor = System.Drawing.SystemColors.ControlDark;
            this.ClientSize = new System.Drawing.Size(194, 238);
            this.Controls.Add(this.lvw_altitudes);
            this.Controls.Add(this.lbl_call);
            this.Controls.Add(this.textField1);
            this.Controls.Add(this.fld_level);
            this.Controls.Add(this.btn_search);
            this.Controls.Add(this.btn_probe);
            this.Controls.Add(this.btn_send);
            this.Controls.Add(this.btn_vhf);
            this.Controls.Add(this.btn_cancel);
            this.Controls.Add(this.btn_response);
            this.Controls.Add(this.btn_close);
            this.Controls.Add(this.scrollBar1);
            this.Controls.Add(this.byTime);
            this.Controls.Add(this.checkBox1);
            this.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.HasCloseButton = false;
            this.HasMinimizeButton = false;
            this.HideOnClose = true;
            this.Name = "AltitudeWindow";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }        
        private FDP2.FDR sourcefdr;
        private Track dataBlock;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.TextBox byTime;
        private VATSYSControls.ScrollBar scrollBar1;
        private GenericButton btn_close;
        private GenericButton btn_response;
        private GenericButton btn_cancel;
        private GenericButton btn_vhf;
        private GenericButton btn_send;
        private GenericButton btn_probe;
        private TextField fld_level;
        private TextField textField1;
        private GenericButton btn_search;
        private TextLabel lbl_call;
        private ListViewEx lvw_altitudes;
    }
}