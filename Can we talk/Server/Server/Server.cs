using System;//lib for Console
using System.Collections.Concurrent;//lib for ConcurrentQueue
using System.Collections.Generic;//lib for List
using System.Net;//lib for IPEndPoint
using System.Net.Sockets;//lib for Socket
using System.Text;//lib for Encoding
using System.Threading;//lib for Thread use
using System.Threading.Tasks;//lib for Task use
using System.Web.Script.Serialization;//lib for JSON
using System.Windows.Forms;//lib to use windows forms

namespace Server
{
    public partial class Server : Form
    {
        private bool active = false;// this is used to check if the server is active or not
        private Thread listener = null;//this is used to create a thread for the server
        private long id = 0;//this is used to create a unique id for each client
        private struct MyClient//client struct
        {
            public long id;//this is the id of the client
            public StringBuilder username;//this is the username of the client
            public TcpClient client;//this is the client via tcp
            public NetworkStream stream;//this is the stream of the client
            public byte[] buffer;//this is the buffer of the client
            public StringBuilder data;//this is the data of the client
            public EventWaitHandle handle;//this is the handle of the client
        };
        private ConcurrentDictionary<long, MyClient> clients = new ConcurrentDictionary<long, MyClient>();//this is the list of clients
        private Task send = null;//this is the task for sending data
        private Thread disconnect = null;//this is the thread for disconnecting clients
        private bool exit = false;//this is used to check if the server is exiting or not

        public Server()//server constructor
        {
            InitializeComponent();// this is used to initialize the form
        }

        private void Log(string msg = "") // clear the log if message is not supplied or is empty
        {
            if (!exit)//checks if the server is exiting or not
            {
                logTextBox.Invoke((MethodInvoker)delegate// this is used to invoke the logtextbox
                {
                    if (msg.Length > 0)//if theres a message
                    {//do this
                        logTextBox.AppendText(string.Format("[ {0} ] {1}{2}", DateTime.Now.ToString("HH:mm"), msg, Environment.NewLine));
                    }//showing via concat the time, message and new line
                    else//if theres no message
                    {//do this
                        logTextBox.Clear();//clear the log
                    }//clear the log
                });
            }
        }

        private string ErrorMsg(string msg)//this is used to show error messages
        {//since its a string method, it returns a string
            return string.Format("ERROR: {0}", msg);//and formats the string to show the error message
        }

        private string SystemMsg(string msg)//this is used to show system messages
        {//since its a string method, it returns a string
            return string.Format("SYSTEM: {0}", msg);//and formats the string to show the system message
        }

        private void Active(bool status)//this is a method to modify the active boolean that we made, also we have a bool parameter
        {
            if (!exit)//first we check if the server is exiting or not
            {
                startButton.Invoke((MethodInvoker)delegate//then we invoke the startbutton
                {
                    active = status;//we use the bool parameter that we sent to the method to set the active to status
                    if (status)//if server is active
                    {
                        addrTextBox.Enabled = false;//we disable the ip address textbox
                        portTextBox.Enabled = false;//and the port textbox
                        usernameTextBox.Enabled = false;//we wont let the user change the username
                        keyTextBox.Enabled = false;//as well as the key
                        startButton.Text = "Stop";//our button will now say stop
                        Log(SystemMsg("Server has started"));//and we log that the server has started
                    }
                    else//if server is inactive
                    {//we'll let the user to change the ip address, port, username and key 
                        addrTextBox.Enabled = true;
                        portTextBox.Enabled = true;
                        usernameTextBox.Enabled = true;
                        keyTextBox.Enabled = true;
                        startButton.Text = "Start";//also we change the button text to start so we know that the server is inactive
                        Log(SystemMsg("Server has stopped"));//and finally we log that the server has stopped
                    }
                });
            }
        }

        private void AddToGrid(long id, string name)//this is for the online clients gridview on the right side
        {
            if (!exit)//firs we check if the server is exiting or not
            {
                clientsDataGridView.Invoke((MethodInvoker)delegate//now we'll work with the dgv
                {
                    string[] row = new string[] { id.ToString(), name };//we set a string array with the id and name of each client
                    clientsDataGridView.Rows.Add(row);//and we add the row to the dgv
                    totalLabel.Text = string.Format("Total clients: {0}", clientsDataGridView.Rows.Count);//now we update the total clients label
                });
            }
        }

        private void RemoveFromGrid(long id)//disconnected client will be removed off the clientdgv
        {
            if (!exit)//if we're not exiting
            {
                clientsDataGridView.Invoke((MethodInvoker)delegate//we can work with the client dgv
                {
                    foreach (DataGridViewRow row in clientsDataGridView.Rows)//we check on each row of the dgv
                    {
                        if (row.Cells["identifier"].Value.ToString() == id.ToString())//if the id of the client is the same as the id of the row
                        {
                            clientsDataGridView.Rows.RemoveAt(row.Index);//we remove that specific row
                            break;//and we break the loop
                        }
                    }
                    totalLabel.Text = string.Format("Total clients: {0}", clientsDataGridView.Rows.Count);//we update the total clients label
                });
            }
        }

        private void Read(IAsyncResult result)//this is the way we read data from the client
        {
            MyClient obj = (MyClient)result.AsyncState;//this is the client that we're reading from
            int bytes = 0;//and this is to check if we have bytes to read
            if (obj.client.Connected)//if our client is connected
            {
                try
                {
                    bytes = obj.stream.EndRead(result);//we set the value of bytes to the value of the stream
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));//if there's an error, we log it with our previous error msg method
                }
            }
            if (bytes > 0)//then if the client stream had data, we changed our buytes value, so if bytes is more than 0:
            {//we do thiw
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));//this is where we append the data to the client data
                try//now if there's no problem
                {
                    if (obj.stream.DataAvailable)//if stream data is available
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), obj);//we begin to read the data (newtork stream lib is used here)
                    }
                    else//if not
                    {
                        string msg = string.Format("{0}: {1}", obj.username, obj.data);//we format our message with the username and data of the client
                        Log(msg);//we log the msg
                        Send(msg, obj.id);//and we send the msg to all the clients using the client id as msg sender
                        obj.data.Clear();//we clear the data of the client
                        obj.handle.Set();//this is to continue any subprocess we paused
                        obj.handle.Set();//and finally we set the handle
                    }
                }
                catch (Exception ex)
                {//if there's a problem
                    obj.data.Clear();//we clear the object
                    Log(ErrorMsg(ex.Message));//and we log the error
                    obj.handle.Set();//this is to continue any subprocess we paused

                }
            }
            else//if disconnected
            {
                obj.client.Close();//we close the client
                obj.handle.Set();//this is to continue any subprocess we paused

            }
        }

        private void ReadAuth(IAsyncResult result)//this is the way we authorize the client when we sat a key for the server

        {
            MyClient obj = (MyClient)result.AsyncState;//same way as the read method, but we're reading the auth data
            int bytes = 0;//we set the byte value to 0 so we can use it for the if statement ahead
            if (obj.client.Connected)//if client is connected
            {
                try
                {
                    bytes = obj.stream.EndRead(result);//we set the bytes value to the client stream value
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));//error handling
                }
            }
            if (bytes > 0)//if theres info to read
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));//append format client data
                try
                {
                    if (obj.stream.DataAvailable)//if the data is available
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(ReadAuth), obj);//we use the networkstream beginread method so we can read this
                    }
                    else
                    {
                        JavaScriptSerializer json = new JavaScriptSerializer(); // we use this json serializer, it helps a lot
                        Dictionary<string, string> data = json.Deserialize<Dictionary<string, string>>(obj.data.ToString());//and we deserialize the stream data on this dictionary
                        if (!data.ContainsKey("username") || data["username"].Length < 1 || !data.ContainsKey("key") || !data["key"].Equals(keyTextBox.Text))//now we use this to check if key is correct
                        {
                            obj.client.Close();//if the keys are different we close the object since is not authorized
                        }
                        else
                        {
                            obj.username.Append(data["username"].Length > 200 ? data["username"].Substring(0, 200) : data["username"]);//we append the client data
                            Send("{\"status\": \"authorized\"}", obj);//and we send the authorized status back
                        }
                        obj.data.Clear();//we clear the object data
                        obj.handle.Set();//this is to continue any subprocess we paused
                    }
                }
                catch (Exception ex)//if theres a problem 
                {
                    obj.data.Clear();//we clear the object
                    Log(ErrorMsg(ex.Message));//we log the error message
                    obj.handle.Set();//we let other processes to keep going
                }
            }
            else
            {
                obj.client.Close();//now if the client is disconnected we close the object
                obj.handle.Set();//and we let other processes to keep going
            }
        }

        private bool Authorize(MyClient obj)//this is how we authorize the client, it is a bool methot that well use ahead
        {
            bool success = false;//it begins with false value
            while (obj.client.Connected)//we use a while loop related to the connection status of the client
            {
                try
                {
                    obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(ReadAuth), obj);//here we use the previous method to check if the client is authorized
                    obj.handle.WaitOne();//blocks for a while
                    if (obj.username.Length > 0)//remember that if it is not authorized, the object is clear so we check if the username is not empty
                    {
                        success = true;//if it is not empty, we set the success value to true
                        break;//and we break the loop
                    }
                }
                catch (Exception ex)//error handling
                {
                    Log(ErrorMsg(ex.Message));//we log the error
                }
            }
            return success;//we return the bool, if it is true, the client is authorized, if not, it is not
        }

        private void Connection(MyClient obj)//this is the connection method
        {
            if (Authorize(obj))//if theres a key, well handle if client is authorized or not
            {
                clients.TryAdd(obj.id, obj);//this is how we add the client to the list of clients to the thread safe dictionary
                AddToGrid(obj.id, obj.username.ToString());//once the client is authorized, we add it to the grid we previously set
                string msg = string.Format("{0} has connected", obj.username);// we format the message whhen we conect
                Log(SystemMsg(msg));//and we log the message
                Send(SystemMsg(msg), obj.id);//also we send the message to all the clients
                while (obj.client.Connected)//while the client is conected
                {
                    try
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), obj);//we use the networkstring beginread method to read the data
                        obj.handle.WaitOne();//and we pause the process
                    }
                    catch (Exception ex)//error handling
                    {
                        Log(ErrorMsg(ex.Message));// if theres an error we log it
                    }
                }
                obj.client.Close();
                clients.TryRemove(obj.id, out MyClient tmp);
                RemoveFromGrid(tmp.id);
                msg = string.Format("{0} has disconnected", tmp.username);
                Log(SystemMsg(msg));
                Send(msg, tmp.id);
            }
        }

        private void Listener(IPAddress ip, int port)
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(ip, port);
                listener.Start();
                Active(true);
                while (active)
                {
                    if (listener.Pending())
                    {
                        try
                        {
                            MyClient obj = new MyClient();
                            obj.id = id;
                            obj.username = new StringBuilder();
                            obj.client = listener.AcceptTcpClient();
                            obj.stream = obj.client.GetStream();
                            obj.buffer = new byte[obj.client.ReceiveBufferSize];
                            obj.data = new StringBuilder();
                            obj.handle = new EventWaitHandle(false, EventResetMode.AutoReset);
                            Thread th = new Thread(() => Connection(obj))
                            {
                                IsBackground = true
                            };
                            th.Start();
                            id++;
                        }
                        catch (Exception ex)
                        {
                            Log(ErrorMsg(ex.Message));
                        }
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
                Active(false);
            }
            catch (Exception ex)
            {
                Log(ErrorMsg(ex.Message));
            }
            finally
            {
                if (listener != null)
                {
                    listener.Server.Close();
                }
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (active)
            {
                active = false;
            }
            else if (listener == null || !listener.IsAlive)
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
                    listener = new Thread(() => Listener(ip, port))
                    {
                        IsBackground = true
                    };
                    listener.Start();
                }
            }
        }

        private void Write(IAsyncResult result)
        {
            MyClient obj = (MyClient)result.AsyncState;
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

        private void BeginWrite(string msg, MyClient obj) // send the message to a specific client
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(Write), obj);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
        }

        private void BeginWrite(string msg, long id = -1) // send the message to everyone except the sender or set ID to lesser than zero to send to everyone
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            foreach (KeyValuePair<long, MyClient> obj in clients)
            {
                if (id != obj.Value.id && obj.Value.client.Connected)
                {
                    try
                    {
                        obj.Value.stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(Write), obj.Value);
                    }
                    catch (Exception ex)
                    {
                        Log(ErrorMsg(ex.Message));
                    }
                }
            }
        }

        private void Send(string msg, MyClient obj)
        {
            if (send == null || send.IsCompleted)
            {
                send = Task.Factory.StartNew(() => BeginWrite(msg, obj));
            }
            else
            {
                send.ContinueWith(antecendent => BeginWrite(msg, obj));
            }
        }

        private void Send(string msg, long id = -1)
        {
            if (send == null || send.IsCompleted)
            {
                send = Task.Factory.StartNew(() => BeginWrite(msg, id));
            }
            else
            {
                send.ContinueWith(antecendent => BeginWrite(msg, id));
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
                    Log(string.Format("{0} (You): {1}", usernameTextBox.Text.Trim(), msg));
                    Send(string.Format("{0}: {1}", usernameTextBox.Text.Trim(), msg));
                }
            }
        }

        private void Disconnect(long id = -1) // disconnect everyone if ID is not supplied or is lesser than zero
        {
            if (disconnect == null || !disconnect.IsAlive)
            {
                disconnect = new Thread(() =>
                {
                    if (id >= 0)
                    {
                        clients.TryGetValue(id, out MyClient obj);
                        obj.client.Close();
                        RemoveFromGrid(obj.id);
                    }
                    else
                    {
                        foreach (KeyValuePair<long, MyClient> obj in clients)
                        {
                            obj.Value.client.Close();
                            RemoveFromGrid(obj.Value.id);
                        }
                    }
                })
                {
                    IsBackground = true
                };
                disconnect.Start();
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            Disconnect();
        }

        private void Server_FormClosing(object sender, FormClosingEventArgs e)
        {
            exit = true;
            active = false;
            Disconnect();
        }

        private void ClientsDataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == clientsDataGridView.Columns["dc"].Index)
            {
                long.TryParse(clientsDataGridView.Rows[e.RowIndex].Cells["identifier"].Value.ToString(), out long id);
                Disconnect(id);
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
    }
}
