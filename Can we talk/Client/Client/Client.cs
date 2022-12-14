using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Data.SqlClient;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Data;

namespace Client
{
    public partial class Client : Form
    {
        public Point mouseLocation;
        private bool connected = false;
        private bool loggedIn = false;
        private bool logging = false;
        private Thread client = null;
        private SqlConnection conn = new SqlConnection("Data Source=ELMERIOUWU;Initial Catalog=cwtdb;Integrated Security=True");
        private struct MyClient
        {
            public string username;
            public string key;
            public TcpClient client;
            public NetworkStream stream;
            public byte[] buffer;
            public StringBuilder data;
            public EventWaitHandle handle;
        };
        private MyClient obj;
        private Task send = null;
        private bool exit = false;

        public Client()
        {
            InitializeComponent();
            
        }
        public Client(string ip, string port, string username)
        {
            InitializeComponent();
            loggedIn = true;
            addrTextBox.Text = ip;
            portTextBox.Text = port;
            usernameTextBox.Text = username;
            if (loggedIn)
            {
                usernameTextBox.Enabled = false;
            }
            else
            {
                usernameTextBox.Enabled = true;
            }
        }

        private void Log(string msg = "") // clear the log if message is not supplied or is empty
        {
            if (!exit)
            {
                logTextBox.Invoke((MethodInvoker)delegate
                {
                    if (msg.Length > 0)
                    {
                        logTextBox.AppendText(string.Format("[ {0} ] {1}{2}", DateTime.Now.ToString("HH:mm"), msg, Environment.NewLine));
                    }
                    else
                    {
                        logTextBox.Clear();
                    }
                });
            }
        }

        private string ErrorMsg(string msg)
        {
            return string.Format("ERROR: {0}", msg);
        }

        private string SystemMsg(string msg)
        {
            return string.Format("SYSTEM: {0}", msg);
        }

        private void Connected(bool status)
        {
            if (!exit)
            {
                connectButton.Invoke((MethodInvoker)delegate
                {
                    connected = status;
                    if (status)
                    {
                        addrTextBox.Enabled = false;
                        portTextBox.Enabled = false;
                        usernameTextBox.Enabled = false;
                        keyTextBox.Enabled = false;
                        connectButton.BackgroundImage = Properties.Resources.off;
                        lblConect.Text = "Disconnect";
                        Log(SystemMsg("You are now connected"));
                    }
                    else
                    {
                        addrTextBox.Enabled = true;
                        portTextBox.Enabled = true;
                        usernameTextBox.Enabled = true;
                        keyTextBox.Enabled = true;
                        connectButton.BackgroundImage = Properties.Resources.on;
                        lblConect.Text = "Connect";
                        Log(SystemMsg("You are now disconnected"));
                    }
                });
            }
        }

        private void Read(IAsyncResult result)
        {
            int bytes = 0;
            if (obj.client.Connected)
            {
                try
                {
                    bytes = obj.stream.EndRead(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
            if (bytes > 0)
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));
                try
                {
                    if (obj.stream.DataAvailable)
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), null);
                    }
                    else
                    {
                        Log(obj.data.ToString());
                        obj.data.Clear();
                        obj.handle.Set();
                    }
                }
                catch (Exception ex)
                {
                    obj.data.Clear();
                    Log(ErrorMsg(ex.Message));
                    obj.handle.Set();
                }
            }
            else
            {
                obj.client.Close();
                obj.handle.Set();
            }
        }

        private void ReadAuth(IAsyncResult result)
        {
            int bytes = 0;
            if (obj.client.Connected)
            {
                try
                {
                    bytes = obj.stream.EndRead(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
            if (bytes > 0)
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));
                try
                {
                    if (obj.stream.DataAvailable)
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(ReadAuth), null);
                    }
                    else
                    {
                        JavaScriptSerializer json = new JavaScriptSerializer(); // feel free to use JSON serializer
                        Dictionary<string, string> data = json.Deserialize<Dictionary<string, string>>(obj.data.ToString());
                        if (data.ContainsKey("status") && data["status"].Equals("authorized"))
                        {
                            Connected(true);
                        }
                        obj.data.Clear();
                        obj.handle.Set();
                    }
                }
                catch (Exception ex)
                {
                    obj.data.Clear();
                    Log(ErrorMsg(ex.Message));
                    obj.handle.Set();
                }
            }
            else
            {
                obj.client.Close();
                obj.handle.Set();
            }
        }

        private bool Authorize()
        {
            bool success = false;
            Dictionary<string, string> data = new Dictionary<string, string>();
            data.Add("username", obj.username);
            data.Add("key", obj.key);
            JavaScriptSerializer json = new JavaScriptSerializer(); // feel free to use JSON serializer
            Send(json.Serialize(data));
            while (obj.client.Connected)
            {
                try
                {
                    obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(ReadAuth), null);
                    obj.handle.WaitOne();
                    if (connected)
                    {
                        success = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
            if (!connected)
            {
                Log(SystemMsg("Unauthorized"));
            }
            return success;
        }

        private void Connection(IPAddress ip, int port, string username, string key)
        {
            try
            {
                obj = new MyClient();
                obj.username = username;
                obj.key = key;
                obj.client = new TcpClient();
                obj.client.Connect(ip, port);
                obj.stream = obj.client.GetStream();
                obj.buffer = new byte[obj.client.ReceiveBufferSize];
                obj.data = new StringBuilder();
                obj.handle = new EventWaitHandle(false, EventResetMode.AutoReset);
                if (Authorize())
                {
                    while (obj.client.Connected)
                    {
                        try
                        {
                            obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), null);
                            obj.handle.WaitOne();
                        }
                        catch (Exception ex)
                        {
                            Log(ErrorMsg(ex.Message));
                        }
                    }
                    obj.client.Close();
                    Connected(false);
                }
            }
            catch (Exception ex)
            {
                Log(ErrorMsg(ex.Message));
            }
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                obj.client.Close();
            }
            else if (client == null || !client.IsAlive)
            {
                string address = addrTextBox.Text.Trim();
                string number = portTextBox.Text.Trim();
                string username = usernameTextBox.Text.Trim();
                bool error = false;
                IPAddress ip = null;
                if (address.Length < 1)
                {
                    error = true;
                    Log(SystemMsg("Address is required"));
                }
                else
                {
                    try
                    {
                        ip = Dns.Resolve(address).AddressList[0];
                    }
                    catch
                    {
                        error = true;
                        Log(SystemMsg("Address is not valid"));
                    }
                }
                int port = -1;
                if (number.Length < 1)
                {
                    error = true;
                    Log(SystemMsg("Port number is required"));
                }
                else if (!int.TryParse(number, out port))
                {
                    error = true;
                    Log(SystemMsg("Port number is not valid"));
                }
                else if (port < 0 || port > 65535)
                {
                    error = true;
                    Log(SystemMsg("Port number is out of range"));
                }
                if (username.Length < 1)
                {
                    error = true;
                    Log(SystemMsg("Username is required"));
                }
                if (!error)
                {
                    // encryption key is optional
                    client = new Thread(() => Connection(ip, port, username, keyTextBox.Text))
                    {
                        IsBackground = true
                    };
                    client.Start();
                }
            }
        }

        private void Write(IAsyncResult result)
        {
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.EndWrite(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
        }

        private void BeginWrite(string msg)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(Write), null);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
        }

        private void Send(string msg)
        {
            if (send == null || send.IsCompleted)
            {
                send = Task.Factory.StartNew(() => BeginWrite(msg));
            }
            else
            {
                send.ContinueWith(antecendent => BeginWrite(msg));
            }
        }

        private void SendTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (sendTextBox.Text.Length > 0)
                {
                    string msg = sendTextBox.Text;
                    sendTextBox.Clear();
                    Log(string.Format("{0} (You): {1}", obj.username, msg));
                    if (connected)
                    {
                        Send(msg);
                    }
                }
            }
        }

        private void Client_FormClosing(object sender, FormClosingEventArgs e)
        {
            exit = true;
            if (connected)
            {
                obj.client.Close();
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            Log();
        }

        private void CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (keyTextBox.PasswordChar == '*')
            {
                keyTextBox.PasswordChar = '\0';
            }
            else
            {
                keyTextBox.PasswordChar = '*';
            }
        }

        private void buttonMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void butonClose_Click(object sender, EventArgs e)
        {
            if (logging)
            {
                HideLoginStuff();
                ShowEverything();
                logging = false;
            }
            else
            {
                Application.Exit();
            }
        }

        private void Client_MouseDown(object sender, MouseEventArgs e)
        {
            mouseLocation = new Point(-e.X, -e.Y);

        }

        private void Client_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mousePosition = Control.MousePosition;
                mousePosition.Offset(mouseLocation.X, mouseLocation.Y);
                Location = mousePosition;
            }
        }

        private void Client_Paint(object sender, PaintEventArgs e)
        {
            IntPtr ptr = NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, 20, 20);
            this.Region = System.Drawing.Region.FromHrgn(ptr);
            NativeMethods.DeleteObject(ptr);
        }

        private void buttonRegister_Click(object sender, EventArgs e)
        {
            this.Hide();
            Register register = new Register();
            register.ShowDialog();
            register.Dispose();
            this.Show();
        }

        private void buttonLogin_Click(object sender, EventArgs e)
        {
            if (loggedIn)
            {
                MessageBox.Show("Already Logged In!");
                buttonLogin.Enabled = false;
            }
            else
            {
                logging = true;
                HideEverything();
                ShowLoginStuff();
            }
        }
        private void HideEverything()
        {
            this.Size = new Size(400, 130);
            connectButton.Hide();
            lblConect.Hide();
            helplabel.Hide();
            buttonHelp.Hide();
            localaddrLabel.Hide();
            portLabel.Hide();
            sendLabel.Hide();
            label2.Hide();
            label3.Hide();
            label4.Hide();
            label5.Hide();
            keyLabel.Hide();
            addrTextBox.Hide();
            buttonLogin.Hide();
            buttonRegister.Hide();

        }
        private void ShowEverything()
        {
            this.Size = new Size(594, 527);
            connectButton.Show();
            lblConect.Show();
            helplabel.Show();
            buttonHelp.Show();
            localaddrLabel.Show();
            portLabel.Show();
            sendLabel.Show();
            label2.Show();
            label3.Show();
            label4.Show();
            label5.Show();
            keyLabel.Show();
            addrTextBox.Show();
            buttonLogin.Show();
            buttonRegister.Show();
        }
        private void ShowLoginStuff()
        {   //190,77 usrnmlbl
            //269,74 usrnmtxt
            label1.Show();
            usernameLabel.Location = new Point(23, 66);
            usernameTextBox.Location = new Point(102, 63);
            buttonMinimize.Location = new Point(322, 12);
            butonClose.Location = new Point(362, 12);
            button3.Show();
            label6.Show();
            passwordtxtbox.Show();
        }
        private void HideLoginStuff()
        {
            label1.Hide();
            usernameTextBox.Location = new Point(269, 74);
            usernameLabel.Location = new Point(190, 77);
            label6.Hide();
            passwordtxtbox.Hide();
            button3.Hide();
            buttonMinimize.Location = new Point(513, 8);
            butonClose.Location = new Point(553, 8);
        }
        private void buttonHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show("En brevedad se te pondrá en contacto con el Administrador para solucionar el problema","Ayuda");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            conn.Open();
            string query = "SELECT username FROM usuario WHERE username = '" + usernameTextBox.Text + "' AND password = '" + passwordtxtbox.Text + "'";
            SqlCommand cmd = new SqlCommand(query, conn);
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                conn.Close();
                HideLoginStuff();
                ShowEverything();
                usernameTextBox.Enabled = false;
                loggedIn = true;
                logging = false;
            }
            else
            {
                conn.Close();
                MessageBox.Show("Usuario o contraseña incorrectos");
            }
        }
    }
}
