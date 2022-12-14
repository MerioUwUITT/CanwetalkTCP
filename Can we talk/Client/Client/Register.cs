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
    public partial class Register : Form
    {
        SqlConnection conn = new SqlConnection("Data Source=ELMERIOUWU;Initial Catalog=cwtdb;Integrated Security=True");
        public Register()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            minimize();
        }
        private void minimize()
        {
            
            this.WindowState = FormWindowState.Minimized;
            
        }

        private void registerbtn_Click(object sender, EventArgs e)
        {
            //check in database if username is occupied, then check if both password are the same
            //if both are true, then insert into database
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT * FROM usuario WHERE username = '" + txtusrnm.Text + "'", conn);
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                MessageBox.Show("Username is already taken");
            }
            else
            {
                if (txtpswrd.Text == txtpswordc.Text && comboBox1.Text != "")
                {
                    conn.Close();
                    conn.Open();
                    SqlCommand cmd2 = new SqlCommand("INSERT INTO usuario (username,password,terapeuta) VALUES ('" + txtusrnm.Text + "', '" + txtpswrd.Text + "','" + comboBox1.Text + "')", conn);
                    cmd2.ExecuteNonQuery();
                    MessageBox.Show("Registration Successful");
                    conn.Close();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Password does not match");
                }
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
