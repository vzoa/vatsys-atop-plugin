using System.ComponentModel;

namespace AtopPlugin.UI;

partial class SettingsWindow
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private IContainer components = null;

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
        this.SuspendLayout();
        // 
        // SettingsWindow
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(146, 72);
        this.HideOnClose = true;
        this.Name = "SettingsWindow";
        this.Text = "ATOP Settings";
        this.TopMost = true;
        this.ResumeLayout(false);
    }

    #endregion
}