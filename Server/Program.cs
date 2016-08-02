using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TestSocket.Settings;

namespace Server
{
    class Program
    { 
        public class Client
        {
            public Socket socket = null;
            public const int bufferSize = 5;
            public byte[] buffer = new byte[bufferSize];
            public StringBuilder sb = new StringBuilder();
        }
        public class StateObject
        {
            public System.Timers.Timer timer;
            public int timerValue;
            public List<Socket> clients;
        }
        
        public static StateObject stateObject;
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private static byte[] byteData = new byte[Client.bufferSize];

        public static void StartListening()
        {
            Settings ss = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("settings.json"));


            IPAddress ipAddress = IPAddress.Parse(ss.Ip);
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, ss.Port);

            
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine("Waiting for a connection...");
            try
            {
                server.Bind(localEndPoint);
                server.Listen(100);

                while (true)
                {
                    allDone.Reset();

                    server.BeginAccept(new AsyncCallback(AcceptCallback), server);

                    allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                allDone.Set();

                Socket server = (Socket)ar.AsyncState;
                Socket handler = server.EndAccept(ar);

                if (!stateObject.clients.Contains(handler))
                {
                    stateObject.clients.Add(handler);
                }

                Console.WriteLine("Socket connected to " + handler.RemoteEndPoint.ToString());
                Client client = new Client();
                client.socket = handler;
                handler.BeginReceive(client.buffer, 0, Client.bufferSize, 0, new AsyncCallback(ReadCallback), client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
}

        private static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
            Client client = (Client)ar.AsyncState;
            Socket handler = client.socket;
            try { 
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    parsData(handler, client.buffer);
                }
                handler.BeginReceive(client.buffer, 0, Client.bufferSize, 0, new AsyncCallback(ReadCallback), client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void parsData(Socket handler, byte[] byteData)
        {
            switch (byteData[0])
            {
                case 0:
                    stateObject.timer.Enabled = false;
                    onTimerTick(stateObject.timer.Enabled);
                    Console.WriteLine("Client {0} send \"Stop\"", handler.RemoteEndPoint.ToString());
                    break;
                case 1:
                    stateObject.timer.Enabled = true;
                    Console.WriteLine("Client {0} send \"Start\"", handler.RemoteEndPoint.ToString());
                    break;
                case 2:
                    stateObject.timer.Enabled = false;
                    stateObject.clients.Remove(handler);
                    Console.WriteLine("Client {0} send \"Close\"", handler.RemoteEndPoint.ToString());
                    if (stateObject.clients.Count != 0)
                    {
                        stateObject.timer.Enabled = true;
                    }
                    break;
            }
        }

        private static void onTimerTick(bool enabled)
        {
            byteData[0] = (byte)(enabled ? 1 : 0);

            byte[] byteTimer = BitConverter.GetBytes(stateObject.timerValue);
            byteTimer.CopyTo(byteData, 1);

            try
            {
                foreach (Socket socket in stateObject.clients)
                {
                    socket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), socket);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void timerTick(Object source, ElapsedEventArgs e)
        {
            stateObject.timerValue++;
            onTimerTick(stateObject.timer.Enabled);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytesSent = handler.EndSend(ar);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void Main(string[] args)
        {
            stateObject = new StateObject();

            stateObject.clients = new List<Socket>();

            stateObject.timer = new System.Timers.Timer(100);
            stateObject.timer.Elapsed += new ElapsedEventHandler(timerTick);
            stateObject.timer.Enabled = false;

            stateObject.timerValue = 0;

            StartListening();
        }
    }
}
