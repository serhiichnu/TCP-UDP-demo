// Головний файл: запуск програми
// Для вивчення:
// 1. Подивіться сюди: `Program.cs` — тут обирається, що запускати (сервер або клієнт).
// 2. Якщо обрано сервер, викликається `Server.Start`; далі рухайтеся в Server.cs:
//    a) `Start` – ініціалізує прослуховування та вводить цикл клавіш;
//    b) `OnAcceptTcpClient` – обробка нового TCP-підключення;
//    c) `OnReceiveDataWithTCP`/`OnReceiveDataWithUDP` – прийом та розбір пакетів;
//    d) `SendToAllWithTCP`/`SendToAllWithUDP` – розсилка повідомлень.
// 3. Якщо обрано клієнт, дивіться Client.cs:
//    a) `Start` – підключення до сервера та початок читання;
//    b) `OnReceiveDataWithTCP`/`OnReceiveDataWithUDP` – що робить клієнт при вхідних даних;
//    c) `SendDataWithTcp`/`SendDataWithUdp` – формування та відправка пакетів.
// 4. Обидва модулі користуються `Packet` (Packet.cs) для серіалізації.
using System.Security.Cryptography;
using System.Text;
using ConsoleApp1;

System.Console.WriteLine("TCP/UDP client-server demo V2");

const string SERVER_PASSWORD_HASH = "a18dbfd510020c811377d5ca7588cf82";

// IP за замовчуванням (локально) та номер порту
string defaultIp = "127.0.0.1";
ushort port = 7777;

Console.WriteLine("Start as (S)erver or (C)lient?");
var key = Console.ReadKey(true);

if (key.Key == ConsoleKey.S)
{
    Console.Write($"Enter server password: ");
    string? inputPassword = Console.ReadLine();

    string hash;
    using (var md5 = MD5.Create())
    {
        byte[] bytes = Encoding.UTF8.GetBytes(inputPassword ?? string.Empty);
        byte[] hashBytes = md5.ComputeHash(bytes);
        hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    if (hash != SERVER_PASSWORD_HASH)
    {
        Console.WriteLine("Incorrect password. Server will not start.");
        return;
    }

    Server server = new Server();
    server.Start(port);
}
else if (key.Key == ConsoleKey.C)
{    
    // клієнт вводить адресу сервера, або береться значення за замовчуванням    Console.Write($"Enter server IP: ");
    Console.Write($"Enter server IP: ");
    string? input = Console.ReadLine();
    string ip = string.IsNullOrWhiteSpace(input) ? defaultIp : input.Trim();

    Client client = new Client();
    client.Start(ip, port);
}
