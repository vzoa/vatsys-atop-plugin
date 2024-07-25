using System;
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


namespace AtopPlugin.UI
{
    public partial class ConflictReportWindow : BaseForm
    {       

        public static ConflictData SelectedConflict;

        public static AtopAircraftState Fdr;
        public ConflictReportWindow()
        {
            InitializeComponent();
            SelectedConflict = ConflictSummaryWindow.SelectedConflict;


            ConflictProbe.ConflictsUpdated += UpdateConflicts;

       }


        private void UpdateConflicts(object sender, EventArgs e)
        {
            DisplayConflictDetails();
        }


        private void ConflictReportWindow_Load(object sender, EventArgs e)
        {
            DisplayConflictDetails();
        }


        private void ConflictWindow_Shown(object sender, EventArgs e)
        {
            DisplayConflictDetails();
        }

        private void listView1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            DisplayConflictDetails();
        }
        private void DisplayConflictDetails()
        {
            var conflict = new ConflictData();


                AtopAircraftDisplayState intAtt = new AtopAircraftDisplayState(conflict.Intruder.GetAtopState());
                AtopAircraftDisplayState actAtt = new AtopAircraftDisplayState(conflict.Active.GetAtopState());
                ConflictType.Text += conflict.ConflictType.ToString();
                Degrees.Text += conflict.TrkAngle.ToString("000.0") + "degrees";
                LOSTime.Text += conflict.LatestLos.ToString("HH:mm");
                LOSTime.BackColor = conflict.ConflictStatus == Models.ConflictStatus.Imminent 
                    ? Colours.GetColour(Colours.Identities.Emergency) : Colours.GetColour(Colours.Identities.Warning);
                RequiredSep.Text += conflict.LongTimesep + "minutes" + "(" + "" + conflict.LatSep + "" + "nm" + ")" + conflict.VerticalSep + "" + "ft";
                ActualSep.Text += conflict.LongTimeact + "" + "(" + "N/A" + ")" + conflict.VerticalAct + "" + "ft";
                INTcs.Text += conflict.Intruder.AircraftType + "\n" + conflict.Intruder.Callsign + "\n" + conflict.Intruder.TAS;
                IntAlt.Text += "F" + conflict.Intruder.CFLString;
                INTTOPdata.Text += conflict.ConflictType == Models.ConflictType.OppositeDirection 
                    ? conflict.Top.Position1.Latitude + "\n" + conflict.Top.Position1.Longitude + "\n" + conflict.Top.Time : null;
                INTconfstart.Text += conflict.ConflictSegmentData.StartLatlong + "\n" + conflict.ConflictSegmentData.StartTime;
                INTconfend.Text += conflict.ConflictSegmentData.EndLatlong + "\n" + conflict.ConflictSegmentData.EndTime;
                ACTcs.Text += conflict.Active.AircraftType + "\n" + conflict.Active.Callsign + "\n" + conflict.Active.TAS;
                ACTAlt.Text += "F" + conflict.Active.CFLString;
                ACTTOPdata.Text += conflict.ConflictType == Models.ConflictType.OppositeDirection
                    ? conflict.Top.Position1.Latitude + "\n" + conflict.Top.Position2.Longitude + "\n" + conflict.Top.Time : null;
                ACTconfstart.Text += conflict.ConflictSegmentData.StartLatlong + "\n" + conflict.ConflictSegmentData.StartTime;
                ACTconfend.Text += conflict.ConflictSegmentData.EndLatlong + "\n" + conflict.ConflictSegmentData.EndTime;

            

        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}