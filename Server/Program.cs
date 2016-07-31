using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Server
{
    class Program
    { 
        public class Client
        {
            public Socket socket = null;
            public const int bufferSize = 2;
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

        public static void StartListening()
        {
            IPAddress ipAddress = IPAddress.Parse("192.168.1.13");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 8888);

            
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                server.Bind(localEndPoint);
                server.Listen(100);

                while (true)
                {
                    allDone.Reset();

                    Console.WriteLine("Waiting for a connection...");
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
            handler.BeginReceive(client.buffer, 0, Client.bufferSize, 0,new AsyncCallback(ReadCallback), client);
        }

        private static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
            Client client = (Client)ar.AsyncState;
            Socket handler = client.socket;



            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                parsData(handler, client.buffer);
                Console.WriteLine(Encoding.ASCII.GetString(client.buffer, 0, bytesRead));

                //client.sb.Append(Encoding.ASCII.GetString(client.buffer, 0, bytesRead));

                //content = client.sb.ToString();
                //if (content.IndexOf("<EOF>") > -1)
                //{
                //    parsData(handler, content);
                //}
                //else
                //{
                //    handler.BeginReceive(client.buffer, 0, Client.bufferSize, 0, new AsyncCallback(ReadCallback), client);
                //}
            }
            handler.BeginReceive(client.buffer, 0, Client.bufferSize, 0, new AsyncCallback(ReadCallback), client);
        }

        private static void parsData(Socket handler, byte[] byteData)
        {
            //byte[] byteData = Encoding.ASCII.GetBytes(content);

            

            if (byteData[0] == 1)
            {
                stateObject.timer.Enabled = true;
            }else
            {
                stateObject.timer.Enabled = false;
                onTimerTick(stateObject.timer.Enabled);
            }
        }
        private static byte[] byteData = new byte[2];
        private static void onTimerTick(bool enabled)
        {
            byteData[0] = (byte)(enabled ? 1 : 0);
            byteData[1] = (byte)stateObject.timerValue;

            Console.WriteLine("byteData " + byteData[0] + " " + byteData[1]);

            foreach (Socket socket in stateObject.clients)
            {
                socket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), socket);
            }
        }

        private static void timerTick(Object source, ElapsedEventArgs e)
        {
            if (stateObject.timerValue < 255)
            {
                stateObject.timerValue++;
            }else
            {
                stateObject.timerValue = 0;
            }
            onTimerTick(stateObject.timer.Enabled);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client {1}", bytesSent, handler.RemoteEndPoint.ToString());

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

            stateObject.timer = new System.Timers.Timer(1000);
            stateObject.timer.Elapsed += new ElapsedEventHandler(timerTick);
            stateObject.timer.Enabled = false;

            stateObject.timerValue = 0;

            StartListening();
        }
    }
}
