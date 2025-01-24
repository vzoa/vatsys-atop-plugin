using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

namespace vatsys_atop_plugin.UI
{
    public partial class AnnotationWindow : BaseForm
    {
        private object source;
        public AnnotationWindow(FDP2.FDR sourcefdr)
        {
            InitializeComponent();
            this.source = (object)sourcefdr;
            this.Text = ((FDP2.FDR)this.source).Callsign.ToUpper() + " - " + ((FDP2.FDR)this.source).AircraftType;
            this.StartPosition = FormStartPosition.Manual;
            Point cursorPosition = Cursor.Position;
            this.Location = cursorPosition;
            TextFieldState();
        }
        public static void Handle(CustomLabelItemMouseClickEventArgs eventArgs)
        {

            AnnotationWindow window = new AnnotationWindow(eventArgs.Track.GetFDR());

            if (eventArgs.Button == CustomLabelItemMouseButton.Right)
            {
                MMI.InvokeOnGUI(window.Show);
            }
            eventArgs.Handled = true;
        }

        private void TextFieldState()
        {
            fld_annot.Text = ((FDP2.FDR)this.source).LocalOpData;
        }
        private void btn_apply_Click(object sender, EventArgs e)
        {
            ((FDP2.FDR)this.source).LocalOpData = fld_annot.Text;
            Close();
        }

        private void btn_clear_Click(object sender, EventArgs e)
        {
            fld_annot.Clear();
        }

        private void btn_cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
