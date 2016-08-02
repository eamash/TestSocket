﻿using Newtonsoft.Json;
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
            public const int bufferSize = 5;
            public byte[] buffer = new byte[bufferSize];
        }

        Socket client;
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        byte[] outData = new byte[ClientObject.bufferSize];
        private static String response = String.Empty;

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
                MainForm.Title = "Client " + client.LocalEndPoint.ToString();
            }
            catch (Exception e)
            {
                lbLog.Items.Add("Connect: " + e.ToString());
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                connectDone.Set();

                Socket clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndConnect(ar);
                lbLog.Dispatcher.Invoke(new Action(delegate ()
                {
                    lbLog.Items.Add("Connected");
                }));
                Receive(clientSocket);
            }
            catch (Exception e)
            {
                lbLog.Dispatcher.Invoke(new Action(delegate ()
                {
                    lbLog.Items.Add("ConnectCallback: " + e.ToString());
                }));
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
                lbLog.Items.Add("Receive: " + e.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                ClientObject state = (ClientObject)ar.AsyncState;
                Socket client = state.socket;

                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 1)
                {
                    parseData(state.buffer);
                }
                client.BeginReceive(state.buffer, 0, ClientObject.bufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                lbLog.Dispatcher.Invoke(new Action(delegate ()
                {
                    lbLog.Items.Add("ReceiveCallback: " + e.ToString());
                }));
            }
        }

        private void parseData(byte[] buffer)
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
                lblTimer.Content = BitConverter.ToInt32(buffer, 1).ToString();
            }));
        }

        private void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
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
            lbLog.Items.Add("Send: " + (outData[0] == 1 ? "Start" : "Stop"));
            try
            {
                client.BeginSend(outData, 0, outData.Length, 0, new AsyncCallback(SendCallback), client);
            }
            catch (Exception ex)
            {
                lbLog.Dispatcher.Invoke(new Action(delegate ()
                {
                    lbLog.Items.Add("btnStartStop_Click: " + ex.ToString());
                }));
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = (Socket)ar.AsyncState;
                int bytesSent = clientSocket.EndSend(ar);
                lbLog.Dispatcher.Invoke(new Action(delegate ()
                {
                    lbLog.Items.Add("Sended message");
                }));
            }
            catch (Exception e)
            {
                lbLog.Dispatcher.Invoke(new Action(delegate ()
                {
                    lbLog.Items.Add("SendCallback: " + e.ToString());
                }));
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            outData[0] = 2;
            try
            {
                client.BeginSend(outData, 0, outData.Length, 0, new AsyncCallback(SendCallback), client);
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
            catch (Exception ex)
            {
                lbLog.Dispatcher.Invoke(new Action(delegate ()
                {
                    lbLog.Items.Add("Window_Closed: " + ex.ToString());
                }));
            }
        }
    }
}
