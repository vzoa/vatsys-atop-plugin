﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using vatsys;
using NATPlugin;

namespace PACOTSPlugin
{
    public partial class TDMWindow : BaseForm
    {
        public TDMWindow()
        {
            InitializeComponent();

            BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);
            
            // Setup ListView columns for Details view with horizontal scrolling
            tdmListView.Columns.Add("", -2); // -2 = auto-size to content
            tdmListView.FullRowSelect = true;
            tdmListView.HeaderStyle = ColumnHeaderStyle.None; // Hide column header

            Plugin.TracksUpdated += Plugin_TracksUpdated;
        }

        private void Plugin_TracksUpdated(object sender, EventArgs e)
        {
            DisplayTracks();
        }

        //private async void ButtonRefresh_Click(object sender, EventArgs e)
        //{
        //    _ = Plugin.GetTracks();
        //}

        private void NATWindow_Load(object sender, EventArgs e)
        {
            DisplayTracks();
        }

        private void DisplayTracks()
        {
            // Clear existing items before adding new ones
            if (InvokeRequired)
            {
                Invoke(new Action(DisplayTracks));
                return;
            }

            tdmListView.Items.Clear();
            
            foreach (var track in Plugin.Tracks.OrderBy(x => x.Id))
            {
                ListViewItem item = new ListViewItem($"TDM TRK {track.Id}) \r\n {track.Start.ToString("yyMMddHHmm")} - {track.End.ToString("yyMMddHHmm")} \r\n {track.RouteDisplay} \r\n");
                //LabelTDM.Text += $"TDM TRK {track.Id}\n \n {track.Start.ToString("yyMMddHHmm")} - {track.End.ToString("yyMMddHHmm")} \n \n {track.RouteDisplay} \n \n";
                tdmListView.Items.Add(item);
            }
            
            // Auto-resize column to fit content
            if (tdmListView.Columns.Count > 0)
            {
                tdmListView.Columns[0].Width = -2;
            }
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Hide();
        }
    }
}