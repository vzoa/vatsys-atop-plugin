﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AtopPlugin.Conflict;
using vatsys;
using System.Drawing;
using System.Threading.Tasks;
using static vatsys.FDP2;
using AtopPlugin.State;
using System.Linq;
using static vatsys.CPDLC;
using System.Net.Http;
using System.Diagnostics;
using AtopPlugin.Models;


namespace AtopPlugin.UI;

public partial class ConflictReportWindow : BaseForm
{
    private ConflictData SelectedConflict;

    public ConflictReportWindow(ConflictData SelectedConflict)
    {
        InitializeComponent();
        this.SelectedConflict = SelectedConflict;


        ConflictProbe.ConflictsUpdated += UpdateConflicts;
    }


    private void UpdateConflicts(object sender, EventArgs e)
    {
        Invoke((Action)DisplayConflictDetails);
        //MessageBox.Show(@"Examined conflict situation has changed!");
    }


    private void ConflictReportWindow_Load(object sender, EventArgs e)
    {
    }


    public static string ConvertToArinc424(double latitude, double longitude)
    {
        // Convert latitude and longitude to degrees, minutes, and seconds
        var latDegrees = Math.Abs((int)Math.Floor(latitude));
        var latMinutes = Math.Abs((int)Math.Floor((latitude - latDegrees) * 60));
        var latSeconds = (latitude - latDegrees - latMinutes / 60.0) * 3600;

        var lonDegrees = Math.Abs((int)Math.Floor(longitude));
        var lonMinutes = Math.Abs((int)Math.Floor((longitude - lonDegrees) * 60));
        var lonSeconds = (longitude - lonDegrees - lonMinutes / 60.0) * 3600;

        // Format the values according to ARINC 424
        var latString = $"{latDegrees:D2}{latMinutes:D2}";
        var lonString = $"{lonDegrees:D3}{lonMinutes:D2}";
        var latHem = latitude > 0 ? $"N" : $"S";
        var lonHem = longitude > 0 ? $"E" : $"W";
        return $"{latString}{latHem}\n{lonString}{lonHem}";
    }

    public static string FormatDmsToArinc424(string dmsValue)
    {
        // Split the DMS value into its components
        string[] dmsComponents = dmsValue.Split(new char[] { '°', '\'', '"' }, StringSplitOptions.RemoveEmptyEntries);
        var degrees = int.Parse(dmsComponents[0]);
        var minutes = int.Parse(dmsComponents[1]);
        var seconds = double.Parse(dmsComponents[2]);

        // Format the values according to ARINC 424
        var degreesString = degrees.ToString("D2");
        var minutesString = minutes.ToString("D2");
        var secondsString = seconds.ToString("F2");

        return $"{degreesString}{minutesString}";
    }

    public void DisplayConflictDetails()
    {
        if (IsHandleCreated && Visible)
        {
            //AtopAircraftDisplayState intAtt = new AtopAircraftDisplayState(SelectedConflict.Intruder.GetAtopState());
            //AtopAircraftDisplayState actAtt = new AtopAircraftDisplayState(SelectedConflict.Active.GetAtopState());
            ConflictType.Text = SelectedConflict.ConflictType.Value.ToString().ToLower();
            Degrees.Text = SelectedConflict.TrkAngle.ToString("000.0") + " degrees";
            LOSTime.Text = SelectedConflict.LatestLos.ToString("HH:mm");
            LOSTime.BackColor = SelectedConflict.ConflictStatus == ConflictStatus.Imminent
                ? Colours.GetColour(Colours.Identities.Emergency)
                : Colours.GetColour(Colours.Identities.Warning);
            RequiredSep.Text = SelectedConflict.LongTimesep.ToString("mm") + " minutes" + " (" + " " +
                               SelectedConflict.LatSep + " " + "nm" + ") "
                               + SelectedConflict.VerticalSep + " " + "ft";
            ActualSep.Text = SelectedConflict.LongTimeact.ToString("mm") + " min " +
                             SelectedConflict.LongTimeact.ToString("ss") + " sec"
                             + " ( " + "N/A" + " ) " + SelectedConflict.VerticalAct + " " + "ft";
            INTcs.Text = SelectedConflict.Intruder.AircraftType + "\n" + SelectedConflict.Intruder.Callsign + "\n" +
                         SelectedConflict.Intruder.TAS;
            IntAlt.Text = "F" + AltitudeBlock.ExtractAltitudeBlock(SelectedConflict.Intruder);
            INTTOPdata.Text = SelectedConflict.ConflictType == Models.ConflictType.OppositeDirection
                ? ConvertToArinc424(SelectedConflict.Top.Position1.Latitude, SelectedConflict.Top.Position1.Longitude) +
                  "\n" + SelectedConflict.Top.Time.ToString("HHmm")
                : null;
            INTconfstart.Text = ConvertToArinc424(SelectedConflict.FirstConflictTime.StartLatlong.Latitude,
                                    SelectedConflict.FirstConflictTime.StartLatlong.Longitude)
                                + "\n" + SelectedConflict.FirstConflictTime.StartTime.ToString("HHmm");
            INTconfend.Text = ConvertToArinc424(SelectedConflict.FirstConflictTime.EndLatlong.Latitude,
                                  SelectedConflict.FirstConflictTime.EndLatlong.Longitude)
                              + "\n" + SelectedConflict.FirstConflictTime.EndTime.ToString("HHmm");
            ACTcs.Text = SelectedConflict.Active.AircraftType + "\n" + SelectedConflict.Active.Callsign + "\n" +
                         SelectedConflict.Active.TAS;
            ACTAlt.Text = "F" + AltitudeBlock.ExtractAltitudeBlock(SelectedConflict.Active);
            ACTTOPdata.Text = SelectedConflict.ConflictType == Models.ConflictType.OppositeDirection
                ? ConvertToArinc424(SelectedConflict.Top.Position1.Latitude, SelectedConflict.Top.Position2.Longitude) +
                  "\n" + SelectedConflict.Top.Time.ToString("HHmm")
                : null;
            ACTconfstart.Text = ConvertToArinc424(SelectedConflict.FirstConflictTime2.StartLatlong.Latitude,
                                    SelectedConflict.FirstConflictTime2.StartLatlong.Longitude)
                                + "\n" + SelectedConflict.FirstConflictTime2.StartTime.ToString("HHmm");
            ACTconfend.Text = ConvertToArinc424(SelectedConflict.FirstConflictTime2.EndLatlong.Latitude,
                                  SelectedConflict.FirstConflictTime2.EndLatlong.Longitude)
                              + "\n" + SelectedConflict.FirstConflictTime2.EndTime.ToString("HHmm");
        }

        if (!ConflictProbe.ConflictDatas.Any())
        {
            Close();
            Dispose();
        }
    }

    private void CloseButton_Click(object sender, EventArgs e)
    {
        Close();
        Dispose();
    }
}