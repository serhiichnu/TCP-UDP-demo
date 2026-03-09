using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    // Клас сервера, який слухає TCP та UDP порти
    // зберігає стан підключених TCP клієнтів і переадресовує повідомлення
    internal class Server
    {

        // TcpListener для прийому TCP-з’єднань
        private TcpListener? tcpListener;
        // UdpClient для безконтактних UDP-пакетів
        private UdpClient? udpListener;
        private bool running = false;
        // Словник станів TCP-клієнтів за ідентифікатором
        // словник зберігає стан кожного TCP-клієнта, ключ — числовий ID
        // Dictionary<int, TcpClientState> означає, що ключі типу int, значення — TcpClientState
        private Dictionary<int, TcpClientState> tcpClients = new Dictionary<int, TcpClientState>();
        private int nextClientId = 1;
        private object clientsLock = new object();

        // Внутрішній клас для збереження стану кожного TCP-клієнта
        private class TcpClientState
        {
            public int ClientId { get; set; }
            public NetworkStream? Stream { get; set; }
            public byte[]? Buffer { get; set; }
            public TcpClient? Client { get; set; }
            public IPEndPoint? EndPoint { get; set; }
        }

        // Старт сервера: запускає прослуховування TCP та UDP
        public void Start(ushort port)
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                tcpListener.BeginAcceptTcpClient(OnAcceptTcpClient, null);

                udpListener = new UdpClient(port);
udpListener!.BeginReceive(OnReceiveDataWithUDP, null);

                running = true;

                Console.WriteLine("\nServer is running. Press 'X' to stop, T or U to send a maeeage to all clients with TCP or UDP.");

                while (running)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.X)
                    {
                        running = false;
                    }
                    else if (key.Key == ConsoleKey.T)
                    {
                        SendToAllWithTCP();
                    }
                    else if (key.Key == ConsoleKey.U)
                    {
                        SendToAllWithUDP();
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Server exception: " + ex.Message);
            }
            finally
            {
                Console.WriteLine("Shutting down server ...");
                lock (clientsLock)
                {
                    foreach (var client in tcpClients.Values)
                    {
                        client.Client?.Close();
                    }
                    tcpClients.Clear();
                }
                udpListener?.Close();
                tcpListener?.Stop();
            }
        }

        // Викликано коли TCP-клієнт підключився
        private void OnAcceptTcpClient(IAsyncResult result)
        {
            try
            {
                TcpClient client = tcpListener!.EndAcceptTcpClient(result);
                NetworkStream stream = client.GetStream();

                // генеруємо унікальний ідентифікатор для нового клієнта
                // lock гарантує, що тільки один потік одночасно змінює nextClientId
                int clientId;
                lock (clientsLock)
                {
                    clientId = nextClientId++;
                }

                var endPoint = client.Client.RemoteEndPoint as IPEndPoint;
                string addr = endPoint != null ? endPoint.Address.ToString() : "?";
                string portStr = endPoint != null ? endPoint.Port.ToString() : "?";
                Console.WriteLine($"TCP Client connected (ID: {clientId}): {addr}:{portStr}");

                TcpClientState state = new TcpClientState();
                state.Stream = stream;
                state.Client = client;
                state.Buffer = new byte[4096];
                state.ClientId = clientId;
                state.EndPoint = null;
                tcpClients.Add(clientId, state);

                stream.BeginRead(state.Buffer!, 0, state.Buffer!.Length, OnReceiveDataWithTCP, state);

                tcpListener.BeginAcceptTcpClient(OnAcceptTcpClient, null);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error accepting TCP client: " + ex.Message);
            }
        }

        // Отримання даних від TCP-клієнта, обробка пакету
        private void OnReceiveDataWithTCP(IAsyncResult result)
        {
            // результат повертається асинхронним методом; ми передавали об'єкт state
            TcpClientState? state = result.AsyncState as TcpClientState;
            if (state == null) return;  // якщо перетворення не вдалося, нічого не робимо
            try
            {
                // EndRead повертає кількість прочитаних байтів. 0 означає, що клієнт закрив з'єднання
                int bytesRead = state.Stream!.EndRead(result);
                if (bytesRead > 0)
                {
                    // копіюємо зчитані байти з буфера в новий масив для обробки
                    byte[] data = new byte[bytesRead];
                    for (int i = 0; i < bytesRead; i++)
                    {
                        data[i] = state.Buffer![i];
                    }

                    using (Packet packet = new Packet(data))
                    {
                        object first = packet.ReadNext(out Type t1);
                        string name = t1 == typeof(string) ? (string)first : first?.ToString() ?? string.Empty;
                        object second = packet.ReadNext(out Type t2);
                        string msg = t2 == typeof(string) ? (string)second : second?.ToString() ?? string.Empty;
                        Console.WriteLine($"\n[TCP Received] {name}: {msg}");
                    }

                    state.Buffer = new byte[4096];
                    // запускаємо асинхронне читання зі стріму в буфер клієнта
                state.Stream!.BeginRead(state.Buffer!, 0, state.Buffer!.Length, OnReceiveDataWithTCP, state);
                }
                else
                {
                    RemoveTcpClient(state.ClientId);
                    IPEndPoint clientEndPoint = (IPEndPoint)state.Client!.Client.RemoteEndPoint!;
                    Console.WriteLine($"TCP Client {state.ClientId} disconnected: {clientEndPoint.Address}:{clientEndPoint.Port}");
                    state.Client!.Close();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
                RemoveTcpClient(state.ClientId);
                IPEndPoint clientEndPoint = (IPEndPoint)state.Client!.Client.RemoteEndPoint!;
                Console.WriteLine($"TCP Client {state.ClientId} disconnected unexpectedly: {clientEndPoint.Address}:{clientEndPoint.Port}");
                state.Client!.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling TCP data from client {state.ClientId}: " + ex.Message);
                RemoveTcpClient(state.ClientId);
                state.Client!.Close();
            }
        }

        // Отримання UDP-пакету та логіка збереження UDP-endpoint клієнта
        private void OnReceiveDataWithUDP(IAsyncResult result)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

                byte[]? receivedData = udpListener!.EndReceive(result, ref endPoint!);
                if (receivedData == null || receivedData.Length == 0) return;
                using (Packet packet = new Packet(receivedData))
                {
                        object first = packet.ReadNext(out Type t1);
                        string name = t1 == typeof(string) ? (string)first : first?.ToString() ?? string.Empty;
                        object second = packet.ReadNext(out Type t2);
                        string msg = t2 == typeof(string) ? (string)second : second?.ToString() ?? string.Empty;
                        Console.WriteLine($"\n[UDP Received] {name}: {msg}");
                }

                foreach (var client in tcpClients.Values)
                {
                    if (client.EndPoint != null)
                    {
                        continue;
                    }
                        IPEndPoint tcpEndPoint = (IPEndPoint)client.Client!.Client.RemoteEndPoint!;
                    if (tcpEndPoint.Address.Equals(endPoint.Address))
                    {
                        client.EndPoint = endPoint;
                        break;
                    }
                }

                udpListener!.BeginReceive(OnReceiveDataWithUDP, null);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error handling UDP data: " + ex.Message);
                if (running)
                {
                    udpListener!.BeginReceive(OnReceiveDataWithUDP, null);
                }
            }
        }

        // Відправляє повідомлення всім підключеним TCP-клієнтам
        private void SendToAllWithTCP()
        {
            string sender = "Im TCP/UDP Server!";
            string message = "Hello with TCP at " + DateTime.Now.ToString("HH:mm:ss.fff");
            using (Packet packet = new Packet())
            {
                packet.WriteString(sender);
                packet.WriteString(message); 
                var data = packet.GetBytesArray();
                foreach (var client in tcpClients)
                {
                    // записуємо байти у мережевий стрім клієнта для відправки
                    client.Value.Stream!.Write(data, 0, data.Length);
                }
            }
        }

        // Відправляє повідомлення всім клієнтам по UDP, які мають endpoint
        private void SendToAllWithUDP()
        {
            System.Console.WriteLine("send called");
            string sender = "Im TCP/UDP Server!";
            string message = "Hello with UDP at " + DateTime.Now.ToString("HH:mm:ss.fff");
            using (Packet packet = new Packet())
            {
                packet.WriteString(sender);
                packet.WriteString(message); 
                var data = packet.GetBytesArray();
                // пересилаємо UDP‑пакет усім клієнтам, які вже надали свій UDP-endpoint
                foreach (var client in tcpClients)
                {
                    System.Console.WriteLine("c");
                    if (client.Value.EndPoint != null)
                    {
                        udpListener!.BeginSend(data, data.Length, client.Value.EndPoint, null, null);
                        System.Console.WriteLine("sended");
                    }
                }
            }
        }

        // Видаляє клієнта з внутрішньої таблиці, викликається при розриві
        // Видаляє запис про TCP-клієнта. Оскільки доступ до словника може відбуватись з різних потоків,
        // використовуємо lock для синхронізації
        private void RemoveTcpClient(int clientId)
        {
            lock (clientsLock)
            {
                if (tcpClients.ContainsKey(clientId))
                {
                    tcpClients.Remove(clientId);
                    Console.WriteLine($"Removed TCP client {clientId} from tracking");
                }
            }
        }

    }
}
