using System;

namespace AtopPlugin.UI
{
    partial class DebugLogWindow
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

        private static DebugLogWindow _instance;

        public DebugLogWindow()
        {
            InitializeComponent();
            _instance = this;
        }

        public static void Log(string message)
        {
            if (_instance == null || _instance.IsDisposed) return;

            if (_instance.InvokeRequired)
            {
                _instance.Invoke(new Action(() => Log(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("hh:mm:ss");
            _instance.logTextBox.AppendText($"[{ timestamp}] {message}{Environment.NewLine}");
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.logTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // logTextBox
            // 
            this.logTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logTextBox.Location = new System.Drawing.Point(0, 0);
            this.logTextBox.Multiline = true;
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.ReadOnly = true;
            this.logTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.logTextBox.Size = new System.Drawing.Size(744, 241);
            this.logTextBox.TabIndex = 0;
            // 
            // DebugLogWindow
            // 
            this.AccessibleName = "Debug Window";
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(744, 241);
            this.Controls.Add(this.logTextBox);
            this.ForeColor = System.Drawing.SystemColors.Control;
            this.Name = "DebugLogWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Debug Window";
            this.TitleTextColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox logTextBox;
    }
}