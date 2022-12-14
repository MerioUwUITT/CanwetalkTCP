using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace Client
{
    public partial class Login : Form
    {
        public string actualip, actualport;
        //set connection object
        SqlConnection con = new SqlConnection(@"Data Source=ELMERIOUWU;Initial Catalog=cwtdb;Integrated Security=True");

        public Login(string ip, string port)
        {
            InitializeComponent();
            actualip = ip;
            actualport = port;
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void minimize()
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //check if user exists and validate their password
            con.Open();
            SqlDataAdapter sda = new SqlDataAdapter("SELECT COUNT(*) FROM usuario WHERE username = '" + textBox1.Text + "' AND password = '" + textBox2.Text + "'", con);
            DataTable dt = new DataTable();
            sda.Fill(dt);
            if (dt.Rows[0][0].ToString() == "1")
            {
                //if user exists, open the main form
                MessageBox.Show("Login successful");
                con.Close();
                this.Close();
            }
            else
            {
                //if user does not exist, display error message
                MessageBox.Show("Invalid username or password");
                con.Close();
            }



        }

        private void button2_Click(object sender, EventArgs e)
        {
            minimize();
        }
    }
}
