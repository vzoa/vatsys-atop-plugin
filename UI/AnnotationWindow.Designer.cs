namespace vatsys_atop_plugin.UI
{
    partial class AnnotationWindow
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
        private void InitializeComponent()
        {
            this.fld_annot = new vatsys.TextField();
            this.btn_cancel = new vatsys.GenericButton();
            this.btn_apply = new vatsys.GenericButton();
            this.btn_clear = new vatsys.GenericButton();
            this.lbl_remark = new vatsys.TextLabel();
            this.SuspendLayout();
            // 
            // fld_annot
            // 
            this.fld_annot.AcceptsReturn = true;
            this.fld_annot.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.fld_annot.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.fld_annot.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.fld_annot.BackColor = System.Drawing.SystemColors.ControlDark;
            this.fld_annot.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.fld_annot.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.fld_annot.Location = new System.Drawing.Point(2, 24);
            this.fld_annot.MaximumSize = new System.Drawing.Size(500, 100);
            this.fld_annot.MaxLength = 192;
            this.fld_annot.MinimumSize = new System.Drawing.Size(300, 50);
            this.fld_annot.Multiline = true;
            this.fld_annot.Name = "fld_annot";
            this.fld_annot.NumericCharOnly = false;
            this.fld_annot.OctalOnly = false;
            this.fld_annot.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.fld_annot.Size = new System.Drawing.Size(471, 50);
            this.fld_annot.TabIndex = 58;
            // 
            // btn_cancel
            // 
            this.btn_cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_cancel.FlatAppearance.BorderSize = 5;
            this.btn_cancel.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_cancel.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_cancel.Location = new System.Drawing.Point(396, 80);
            this.btn_cancel.Name = "btn_cancel";
            this.btn_cancel.Padding = new System.Windows.Forms.Padding(3);
            this.btn_cancel.Size = new System.Drawing.Size(68, 30);
            this.btn_cancel.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_cancel.SubText = "";
            this.btn_cancel.TabIndex = 59;
            this.btn_cancel.Text = "Cancel";
            this.btn_cancel.UseVisualStyleBackColor = false;
            this.btn_cancel.Click += new System.EventHandler(this.btn_cancel_Click);
            // 
            // btn_apply
            // 
            this.btn_apply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_apply.FlatAppearance.BorderSize = 5;
            this.btn_apply.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_apply.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_apply.Location = new System.Drawing.Point(12, 80);
            this.btn_apply.Name = "btn_apply";
            this.btn_apply.Padding = new System.Windows.Forms.Padding(3);
            this.btn_apply.Size = new System.Drawing.Size(68, 30);
            this.btn_apply.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_apply.SubText = "";
            this.btn_apply.TabIndex = 60;
            this.btn_apply.Text = "Apply";
            this.btn_apply.UseVisualStyleBackColor = false;
            this.btn_apply.Click += new System.EventHandler(this.btn_apply_Click);
            // 
            // btn_clear
            // 
            this.btn_clear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_clear.FlatAppearance.BorderSize = 5;
            this.btn_clear.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_clear.Font = new System.Drawing.Font("Terminus (TTF)", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_clear.Location = new System.Drawing.Point(202, 80);
            this.btn_clear.Name = "btn_clear";
            this.btn_clear.Padding = new System.Windows.Forms.Padding(3);
            this.btn_clear.Size = new System.Drawing.Size(68, 30);
            this.btn_clear.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_clear.SubText = "";
            this.btn_clear.TabIndex = 61;
            this.btn_clear.Text = "Clear";
            this.btn_clear.UseVisualStyleBackColor = false;
            this.btn_clear.Click += new System.EventHandler(this.btn_clear_Click);
            // 
            // lbl_remark
            // 
            this.lbl_remark.BackColor = System.Drawing.SystemColors.Control;
            this.lbl_remark.Font = new System.Drawing.Font("Terminus (TTF)", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbl_remark.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.lbl_remark.HasBorder = false;
            this.lbl_remark.InteractiveText = true;
            this.lbl_remark.Location = new System.Drawing.Point(8, -3);
            this.lbl_remark.Name = "lbl_remark";
            this.lbl_remark.Size = new System.Drawing.Size(174, 24);
            this.lbl_remark.TabIndex = 62;
            this.lbl_remark.Text = "Annotate Remark:";
            this.lbl_remark.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // AnnotationWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(476, 114);
            this.Controls.Add(this.lbl_remark);
            this.Controls.Add(this.btn_clear);
            this.Controls.Add(this.btn_apply);
            this.Controls.Add(this.btn_cancel);
            this.Controls.Add(this.fld_annot);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.HasCloseButton = false;
            this.HasMinimizeButton = false;
            this.HideOnClose = true;
            this.Name = "AnnotationWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private vatsys.TextField fld_annot;
        private vatsys.GenericButton btn_cancel;
        private vatsys.GenericButton btn_apply;
        private vatsys.GenericButton btn_clear;
        private vatsys.TextLabel lbl_remark;
    }
}