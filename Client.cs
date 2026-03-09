using System.Net;
using System.Net.Sockets;

namespace ConsoleApp1
{
    // Клас клієнта: встановлює TCP/UDP з’єднання із сервером
    internal class Client
    {

        private UdpClient? udpClient;
        private TcpClient? tcpClient;
        private NetworkStream? tcpStream;
        private bool running = false;
        private byte[]? receiveBuffer;

        // Запускає роботу клієнта; підключається до IP і порту
        public void Start(string ip, ushort port)
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(ip, port);
                tcpStream = tcpClient.GetStream();
                receiveBuffer = new byte[4096];
                // старт асинхронного читання з TCP-потоку, callback — OnReceiveDataWithTCP
                tcpStream!.BeginRead(receiveBuffer!, 0, receiveBuffer.Length, OnReceiveDataWithTCP, null);

                udpClient = new UdpClient();
                udpClient.Connect(ip, port);
                // готуємось приймати наступний UDP-пакет (рекурсивний виклик)
                udpClient!.BeginReceive(OnReceiveDataWithUDP, null);

                running = true;

                while (running)
                {
                    Console.WriteLine("\nPress 'T' for TCP sending, 'U' for UDP sending or 'X' to exit.");
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.X:
                            running = false;
                            break;
                        case ConsoleKey.U:
                            SendDataWithUdp();
                            break;
                        case ConsoleKey.T:
                            SendDataWithTcp();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Main exception: " + ex.Message);
            }
            finally
            {
                udpClient?.Close();
                tcpStream?.Close();
                tcpClient?.Close();
                Console.WriteLine("Client shut down.");
            }
        }

        // Обробка вхідних TCP-пакетів від сервера
        private void OnReceiveDataWithTCP(IAsyncResult result)
        {
            try
            {
                int bytesRead = tcpStream!.EndRead(result);
                if (bytesRead > 0)
                {
                    byte[] data = new byte[bytesRead];
                    for (int i = 0; i < bytesRead; i++)
                    {
                            // переносимо отримані байти в окремий масив
                        data[i] = receiveBuffer![i];
                    }

                    using (Packet packet = new Packet(data))
                    {
                        object first = packet.ReadNext(out Type t1);
                        string name = t1 == typeof(string) ? (string)first : first?.ToString() ?? string.Empty;
                        object second = packet.ReadNext(out Type t2);
                        string msg = t2 == typeof(string) ? (string)second : second?.ToString() ?? string.Empty;
                        Console.WriteLine($"\n[TCP Received] {name}: {msg}");
                    }
                }
                else
                {
                    Console.WriteLine("TCP Server closed the connection.");
                    running = false;
                    return;
                }
                receiveBuffer = new byte[4096];
                tcpStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, OnReceiveDataWithTCP, null);
            }
            catch (IOException)
            {
                Console.WriteLine("TCP Connection was lost.");
                running = false;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("TCP Stream was closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in TCP receive callback: " + ex.Message);
                running = false;
            }
        }

        // Обробка вхідних UDP-пакетів від сервера
        private void OnReceiveDataWithUDP(IAsyncResult result)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] receivedData = udpClient!.EndReceive(result, ref remoteEndPoint!);

                using (Packet packet = new Packet(receivedData))
                {
                    object first = packet.ReadNext(out Type t1);
                    string name = t1 == typeof(string) ? (string)first : first?.ToString() ?? string.Empty;
                    object second = packet.ReadNext(out Type t2);
                    string msg = t2 == typeof(string) ? (string)second : second?.ToString() ?? string.Empty;
                    Console.WriteLine($"\n[UDP Received] {name}: {msg}");
                }

                udpClient.BeginReceive(OnReceiveDataWithUDP, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in UDP receive callback: " + ex.Message);
            }
        }

        // Зчитує ім’я та повідомлення з консолі і відправляє по TCP
        private void SendDataWithTcp()
        {
            try
            {
                Console.Write("Enter your name: ");
                string name = Console.ReadLine() ?? string.Empty;
                Console.Write("Enter message: ");
                string message = Console.ReadLine() ?? string.Empty;

                using (Packet packet = new Packet())
                {
                    packet.WriteString(name);    
                    packet.WriteString(message); 
                    var data = packet.GetBytesArray();
                    // відправка чорнового пакету по TCP
                    tcpStream!.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending TCP data: " + ex.Message);
            }
        }

        // Зчитує ім’я та повідомлення з консолі і відправляє по UDP
        private void SendDataWithUdp()
        {
            try
            {
                Console.Write("Enter your name: ");
                string name = Console.ReadLine() ?? string.Empty;
                Console.Write("Enter message: ");
                string message = Console.ReadLine() ?? string.Empty;

                using (Packet packet = new Packet())
                {
                    packet.WriteString(name);    
                    packet.WriteString(message); 
                    var data = packet.GetBytesArray();
                    // відправка пакету по UDP (без встановленого з’єднання)
                    udpClient!.Send(data, data.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending UDP data: " + ex.Message);
            }
        }

    }
}
