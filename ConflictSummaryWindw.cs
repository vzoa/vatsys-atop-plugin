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

namespace AuroraLabelItemsPlugin
{
    public partial class Form1 : Form
    {
        AuroraLabelItemsPlugin type = new AuroraLabelItemsPlugin();
        CPAR conflicts = new CPAR();
        public Form1() 
        {
            foreach (var segment in conflicts.Segments1)
            {
                segment.callsign.ToString();
                segment.startTime.ToString();
                segment.endTime.ToString(); 
            }
            InitializeComponent();
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }
    }
}
