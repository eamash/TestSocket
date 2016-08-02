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

        /// <summary>
        /// Создание сокета.
        /// Связывание сокета с локальной конечной точкой.
        /// Начинаем асинхронную операцию принятия попытки входящего подключения. 
        /// </summary>
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

        /// <summary>
        /// Открываем семафор для следующей попытки подключения.
        /// Принимаем попытку входящего подключения.
        /// Сохраняем параметры соединения.
        /// Начинаем выполнение асинхронного приема данных с подключенного объекта.
        /// </summary>
        /// <param name="ar">сокет подключившегося объекта</param>
        private static void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                allDone.Set();

                Socket server = (Socket)ar.AsyncState;
                Socket handler = server.EndAccept(ar);

                Console.WriteLine("Socket connected to " + handler.RemoteEndPoint.ToString());

                if (!stateObject.clients.Contains(handler))
                {
                    stateObject.clients.Add(handler);
                }

                Client client = new Client();
                client.socket = handler;
                handler.BeginReceive(client.buffer, 0, Client.bufferSize, 0, new AsyncCallback(ReadCallback), client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Завершает отложенное асинхронное чтение.
        /// Если данные не пусты, то начинаем их расшифровку.
        /// Снова начинаем выполнение асинхронного приема данных с подключенного объекта.
        /// </summary>
        /// <param name="ar">объект для чтения данных с объекта</param>
        private static void ReadCallback(IAsyncResult ar)
        {
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

        /// <summary>
        /// Расшифровываем информацию с объекта, в первом байте приходит команда:
        /// 0 - остановка счетчика
        /// 1 - запуск счетчика
        /// 2 - закрытие объекта
        /// В зависимости от команды, останавливаем, запускаем счетчик или удаляем объект из списка рассылки сообщений
        /// </summary>
        /// <param name="handler">пришедший сокет объекта</param>
        /// <param name="byteData">прочитанные данные с объекта</param>
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

        /// <summary>
        /// Формируем данные для передачи:
        /// первый байт - состояние счетчика (остановлен/запущен),
        /// остальные 4 байла - значение счетчика.
        /// Начинаем асинхронную передачу ханных всем подключившимся объе6ктам
        /// </summary>
        /// <param name="enabled">состояние счетчика (остановлен/запущен)</param>
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

        /// <summary>
        /// Увеличиваем счетчик и запускаем передачу данных
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private static void timerTick(Object source, ElapsedEventArgs e)
        {
            stateObject.timerValue++;
            onTimerTick(stateObject.timer.Enabled);
        }

        /// <summary>
        /// Завершаем отложенную операцию асинхронной передачи.
        /// </summary>
        /// <param name="ar">сокет для передачи данных</param>
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

        /// <summary>
        /// инициализируем занные.
        /// </summary>
        /// <param name="args"></param>
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
