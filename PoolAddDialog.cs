using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TMB_Switcher
{
    public partial class PoolAddDialog : Form
    {
        public PoolAddDialog()
        {
            InitializeComponent();
        }

        private void okayButton_Click(object sender, EventArgs e)
        {
            // Force user to fill in value for all fields
            if (string.IsNullOrEmpty(urlText.Text) ||
                string.IsNullOrEmpty(userText.Text) ||
                string.IsNullOrEmpty(passText.Text) ||
                string.IsNullOrEmpty(nameText.Text) ||
                string.IsNullOrEmpty(descText.Text) ||
                string.IsNullOrEmpty(algorithmCombo.Text))
            {
                MessageBox.Show("You must fill in a value for all fields.");
                return;
            }

            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
            Close();
        }
    }
}
