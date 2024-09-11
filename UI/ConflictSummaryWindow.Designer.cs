namespace AtopPlugin.UI
{
    partial class ConflictSummaryWindow
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
            this.IntruderText = new System.Windows.Forms.TextBox();
            this.Att1Text = new System.Windows.Forms.TextBox();
            this.Att2Text = new System.Windows.Forms.TextBox();
            this.ActiveText = new System.Windows.Forms.TextBox();
            this.OvrdText = new System.Windows.Forms.TextBox();
            this.TypeText = new System.Windows.Forms.TextBox();
            this.StartTimeText = new System.Windows.Forms.TextBox();
            this.EndTimeText = new System.Windows.Forms.TextBox();
            this.conflictListView = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // IntruderText
            // 
            this.IntruderText.BackColor = System.Drawing.SystemColors.Control;
            this.IntruderText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.IntruderText.Font = new System.Drawing.Font("Terminus (TTF)", 9.749999F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.IntruderText.Location = new System.Drawing.Point(12, 12);
            this.IntruderText.Name = "IntruderText";
            this.IntruderText.Size = new System.Drawing.Size(71, 22);
            this.IntruderText.TabIndex = 11;
            this.IntruderText.Text = "Intruder";
            // 
            // Att1Text
            // 
            this.Att1Text.BackColor = System.Drawing.SystemColors.Control;
            this.Att1Text.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Att1Text.Font = new System.Drawing.Font("Terminus (TTF)", 9.749999F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Att1Text.Location = new System.Drawing.Point(89, 12);
            this.Att1Text.Name = "Att1Text";
            this.Att1Text.Size = new System.Drawing.Size(31, 22);
            this.Att1Text.TabIndex = 12;
            this.Att1Text.Text = "Att";
            // 
            // Att2Text
            // 
            this.Att2Text.BackColor = System.Drawing.SystemColors.Control;
            this.Att2Text.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Att2Text.Font = new System.Drawing.Font("Terminus (TTF)", 9.749999F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Att2Text.Location = new System.Drawing.Point(185, 12);
            this.Att2Text.Name = "Att2Text";
            this.Att2Text.Size = new System.Drawing.Size(31, 22);
            this.Att2Text.TabIndex = 13;
            this.Att2Text.Text = "Att";
            // 
            // ActiveText
            // 
            this.ActiveText.BackColor = System.Drawing.SystemColors.Control;
            this.ActiveText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.ActiveText.Font = new System.Drawing.Font("Terminus (TTF)", 9.749999F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ActiveText.Location = new System.Drawing.Point(126, 12);
            this.ActiveText.Name = "ActiveText";
            this.ActiveText.Size = new System.Drawing.Size(53, 22);
            this.ActiveText.TabIndex = 14;
            this.ActiveText.Text = "Active";
            // 
            // OvrdText
            // 
            this.OvrdText.BackColor = System.Drawing.SystemColors.Control;
            this.OvrdText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.OvrdText.Font = new System.Drawing.Font("Terminus (TTF)", 9.749999F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OvrdText.Location = new System.Drawing.Point(222, 12);
            this.OvrdText.Name = "OvrdText";
            this.OvrdText.Size = new System.Drawing.Size(41, 22);
            this.OvrdText.TabIndex = 15;
            this.OvrdText.Text = "Ovrd";
            // 
            // TypeText
            // 
            this.TypeText.BackColor = System.Drawing.SystemColors.Control;
            this.TypeText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.TypeText.Font = new System.Drawing.Font("Terminus (TTF)", 9.749999F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TypeText.Location = new System.Drawing.Point(269, 12);
            this.TypeText.Name = "TypeText";
            this.TypeText.Size = new System.Drawing.Size(41, 22);
            this.TypeText.TabIndex = 16;
            this.TypeText.Text = "Type";
            // 
            // StartTimeText
            // 
            this.StartTimeText.BackColor = System.Drawing.SystemColors.Control;
            this.StartTimeText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.StartTimeText.Font = new System.Drawing.Font("Terminus (TTF)", 9.749999F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StartTimeText.Location = new System.Drawing.Point(316, 12);
            this.StartTimeText.Name = "StartTimeText";
            this.StartTimeText.Size = new System.Drawing.Size(77, 22);
            this.StartTimeText.TabIndex = 17;
            this.StartTimeText.Text = "StartTime";
            // 
            // EndTimeText
            // 
            this.EndTimeText.BackColor = System.Drawing.SystemColors.Control;
            this.EndTimeText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.EndTimeText.Font = new System.Drawing.Font("Terminus (TTF)", 9.749999F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EndTimeText.Location = new System.Drawing.Point(399, 12);
            this.EndTimeText.Name = "EndTimeText";
            this.EndTimeText.Size = new System.Drawing.Size(67, 22);
            this.EndTimeText.TabIndex = 18;
            this.EndTimeText.Text = "EndTime";
            // 
            // conflictListView
            // 
            this.conflictListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.conflictListView.BackColor = System.Drawing.Color.Red;
            this.conflictListView.ForeColor = System.Drawing.SystemColors.Window;
            this.conflictListView.FullRowSelect = true;
            this.conflictListView.HideSelection = false;
            this.conflictListView.LabelWrap = false;
            this.conflictListView.Location = new System.Drawing.Point(11, 35);
            this.conflictListView.MultiSelect = false;
            this.conflictListView.Name = "conflictListView";
            this.conflictListView.Size = new System.Drawing.Size(444, 82);
            this.conflictListView.TabIndex = 19;
            this.conflictListView.UseCompatibleStateImageBehavior = false;
            // 
            // ConflictSummaryWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.AutoSize = true;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(468, 130);
            this.Controls.Add(this.EndTimeText);
            this.Controls.Add(this.TypeText);
            this.Controls.Add(this.StartTimeText);
            this.Controls.Add(this.OvrdText);
            this.Controls.Add(this.ActiveText);
            this.Controls.Add(this.Att2Text);
            this.Controls.Add(this.Att1Text);
            this.Controls.Add(this.IntruderText);
            this.Controls.Add(this.conflictListView);
            this.HasCloseButton = false;
            this.HasMinimizeButton = false;
            this.MaximumSize = new System.Drawing.Size(472, 1280);
            this.MinimumSize = new System.Drawing.Size(472, 158);
            this.Name = "ConflictSummaryWindow";
            this.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.Text = "CONFLICT SUMMARY";
            this.TitleTextColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.TopMost = true;
            this.Load += new System.EventHandler(this.ConflictSummaryWindow_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox IntruderText;
        private System.Windows.Forms.TextBox Att1Text;
        private System.Windows.Forms.TextBox Att2Text;
        private System.Windows.Forms.TextBox ActiveText;
        private System.Windows.Forms.TextBox OvrdText;
        private System.Windows.Forms.TextBox TypeText;
        private System.Windows.Forms.TextBox StartTimeText;
        private System.Windows.Forms.TextBox EndTimeText;
        private System.Windows.Forms.ListView conflictListView;
    }
}