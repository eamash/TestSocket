using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using TestSocket.Settings;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        partial class ClientObject
        {
            public Socket socket = null;
            public const int bufferSize = 2;
            public byte[] buffer = new byte[bufferSize];
        }

        Socket client;
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        byte[] outData = new byte[2];

        public MainWindow()
        {
            InitializeComponent();

            Connect();
            
        }

        private void Connect()
        {
            try
            {
                Settings ss = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("settings.json"));

                IPAddress ipAddress = IPAddress.Parse(ss.Ip);
                IPEndPoint endPoint = new IPEndPoint(ipAddress, ss.Port);

                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                client.BeginConnect(endPoint, new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();

                lbLog.Items.Add("WaitOne");

                Receive(client);

                lbLog.Items.Add("Receive(client)");
            }
            catch (Exception e)
            {
                lbLog.Items.Add(e.ToString());
            }
        }

        private void Receive(Socket client)
        {
            try
            {
                ClientObject state = new ClientObject();
                state.socket = client;
                client.BeginReceive(state.buffer, 0, ClientObject.bufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                lbLog.Items.Add(e.ToString());
            }
        }
        private static String response = String.Empty;

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                ClientObject state = (ClientObject)ar.AsyncState;
                Socket client = state.socket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 1)
                {
                    // There might be more data, so store the data received so far.
                    //state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Get the rest of the data.
                    //lbLog.Items.Add("state.buffer " + state.buffer[0] + " " + state.buffer[1]);
                    parsData(state.buffer);
                    


                }
                client.BeginReceive(state.buffer, 0, ClientObject.bufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                //else
                //{
                //    // All the data has arrived; put it in response.
                //    if (state.sb.Length > 1)
                //    {
                //        //lbLog.Items.Add(state.sb.ToString());
                //        parsData(state.sb.ToString());
                //    }
                //    // Signal that all bytes have been received.
                //}
            }
            catch (Exception e)
            {
                //lbLog.Items.Add(e.ToString());
            }
        }

        private void parsData(byte[] buffer)
        {
            if (buffer[0] == 1)
            {
                btnStartStop.Dispatcher.Invoke(new Action(delegate ()
                {
                    btnStartStop.Content = "Stop";
                })); 
            }
            else
            {
                btnStartStop.Dispatcher.Invoke(new Action(delegate ()
                {
                    btnStartStop.Content = "Start";
                }));
            }

            lblTimer.Dispatcher.Invoke(new Action(delegate ()
            {
                lblTimer.Content = buffer[1].ToString();
            }));
        }

        

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                connectDone.Set();
                Socket clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndConnect(ar);
                //lbLog.Items.Add("Socket connected to " + clientSocket.RemoteEndPoint.ToString());
                
            }
            catch (Exception e)
            {
                //lbLog.Items.Add(e.ToString());
            }
        }

        private void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            //byteData = new byte[2];
            if (btnStartStop.Content.ToString() == "Start")
            {
                outData[0] = 1;
                btnStartStop.Content = "Stop";
            }
            else
            {
                outData[0] = 0;
                btnStartStop.Content = "Start";
            }
            //client.Send(byteData);
            client.BeginSend(outData, 0, outData.Length, 0, new AsyncCallback(SendCallback), client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket clientSocket = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = clientSocket.EndSend(ar);
                //lbLog.Items.Add("Sent " + bytesSent + " bytes to server.");

            }
            catch (Exception e)
            {
                //lbLog.Items.Add(e.ToString());
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }
    }
}
