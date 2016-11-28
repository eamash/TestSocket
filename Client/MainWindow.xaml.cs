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
            public const int bufferSize = 5;
            public byte[] buffer = new byte[bufferSize];
        }

        Socket client;
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        byte[] outData = new byte[ClientObject.bufferSize];
        private static String response = String.Empty;
        private Boolean connected = false;

        /// <summary>
        /// Инициализируем компоненты формы, запускаем начало соединения.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            //Connect();
        }

        /// <summary>
        /// Создаем сокет для соединения.
        /// Начинаем выполнение асинхронного запроса для подключения к удаленному узлу.
        /// </summary>
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

        /// <summary>
        /// Завершаем ожидающий асинхронный запрос на подключение.
        /// Запускаем прием данных с подключенного сокета.
        /// </summary>
        /// <param name="ar">сокет для подключения</param>
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

        /// <summary>
        /// Начинаем выполнение асинхронного приема данных с подключенного сокета.
        /// </summary>
        /// <param name="client">сокет для подключения</param>
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

        /// <summary>
        /// Завершаем отложенное асинхронное чтение.
        /// Если прочитанные данные не пусты, расшифровываем их.
        /// Снова запускаем выполнение асинхронного приема данных с подключенного сокета.
        /// </summary>
        /// <param name="ar">сокет подключения</param>
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
                client.BeginReceive(state.buffer, 0, ClientObject.bufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                lbLog.Dispatcher.Invoke(new Action(delegate ()
                {
                    lbLog.Items.Add("ReceiveCallback: " + e.ToString());
                }));
            }
        }

        /// <summary>
        /// Расшифровываем информацию с объекта, в первом байте приходит команда:
        /// 0 - произошла остановка счетчика
        /// 1 - произошел запуск счетчика
        /// В зависимости от команды, меняем назначение кнопки запуска/остановки счетчика
        /// Меняем значение счетчика.
        /// </summary>
        /// <param name="buffer"></param>
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

        /// <summary>
        /// В зависимости от текущего назначения кнопки, формируем данные для отправки.
        /// Начинаем асинхронную передачу данных на подключенный сокет.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Завершаем асинхронную передачу данных на подключенный сокет.
        /// </summary>
        /// <param name="ar">сокет подключения</param>
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = (Socket)ar.AsyncState;
                int bytesSent = clientSocket.EndSend(ar);
                sendDone.Set();

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

        /// <summary>
        /// При закрытии формы формируем данные, содержащие команду закрытия формы.
        /// Начинаем асинхронную передачу данных на подключенный сокет.
        /// После передачи данных, блокируем передачу и получение данных для сокета.
        /// Закрываем соединение.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            if (connected)
            {
                stopConnect();
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (connected)
            {
                // текущее состояние - подключено
                btnConnect.Dispatcher.Invoke(new Action(delegate()
                {
                    btnConnect.Content = "Connect";
                }));
                btnStartStop.Dispatcher.Invoke(new Action(delegate()
                {
                    btnStartStop.IsEnabled = false;
                }));
                connected = false;
                stopConnect();
            }else
            {
                // текущее состояние - отключено
                btnConnect.Dispatcher.Invoke(new Action(delegate()
                {
                    btnConnect.Content = "Disconnect";
                }));
                btnStartStop.Dispatcher.Invoke(new Action(delegate()
                {
                    btnStartStop.IsEnabled = true;
                }));
                connected = true;
                Connect();
            }
        }

        private void stopConnect()
        {
            outData[0] = 2;
            try
            {
                client.BeginSend(outData, 0, outData.Length, 0, new AsyncCallback(SendCallback), client);
                sendDone.WaitOne();

                client.Shutdown(SocketShutdown.Both);
                client.Close();

                lbLog.Dispatcher.Invoke(new Action(delegate()
                {
                    lbLog.Items.Add("Disconnected");
                }));

            }
            catch (Exception ex)
            {
                lbLog.Dispatcher.Invoke(new Action(delegate()
                {
                    lbLog.Items.Add("stopConnect: " + ex.ToString());
                }));
            }
            
        }
    }
}
