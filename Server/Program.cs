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

        const int countTimer = 100;
        
        public static List<Socket> clientsList;

        public static Counter counter;
        private static System.Timers.Timer timer;

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

                if (!clientsList.Contains(handler))
                {
                    clientsList.Add(handler);
                }

                onTimerTick(counter.TimerState);

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
                    counter.TimerState = false;
                    timer.Enabled = false;
                    onTimerTick(counter.TimerState);
                    Console.WriteLine("Client {0} send \"Stop\"", handler.RemoteEndPoint.ToString());
                    break;
                case 1:
                    timer.Enabled = true;
                    counter.TimerState = true;
                    Console.WriteLine("Client {0} send \"Start\"", handler.RemoteEndPoint.ToString());
                    break;
                case 2:
                    clientsList.Remove(handler);
                    Console.WriteLine("Client {0} send \"Close\"", handler.RemoteEndPoint.ToString());
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

            byte[] byteTimer = BitConverter.GetBytes(counter.TimerValue);
            byteTimer.CopyTo(byteData, 1);

            try
            {
                foreach (Socket socket in clientsList)
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
        /// запускаем передачу данных
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private static void timerTick(Object source, ElapsedEventArgs e)
        {
            onTimerTick(counter.TimerState);
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
        /// инициализируем данные.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            clientsList = new List<Socket>();
            
            counter = new Counter(countTimer);

            timer = new System.Timers.Timer(countTimer);
            timer.Elapsed += new ElapsedEventHandler(timerTick);
            timer.Enabled = false;

            StartListening();
        }
    }
}
