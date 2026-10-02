using System.Text;

namespace CurrencyServer
{
    class Program
    {
        static CurrencyServer server;
        static readonly object consoleLock = new object();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("============================================================");
            Console.WriteLine("СЕРВЕР КУРСОВ ВАЛЮТ");
            Console.WriteLine("============================================================");
            Console.WriteLine();

            Console.Write("Введите порт (по умолчанию 8888): ");
            string portInput = Console.ReadLine();
            int port = 8888;
            if (!string.IsNullOrWhiteSpace(portInput) &&
                int.TryParse(portInput, out int p) && p > 0 && p < 65536)
            {
                port = p;
            }

            Console.WriteLine();
            Console.WriteLine($"Поддерживаемые валюты: USD, EUR, UAH, PLN, GBP, JPY, CNY, CHF, CAD, TRY");
            Console.WriteLine($"Формат запроса от клиента: FROM TO (например, USD EUR)");
            Console.WriteLine();

            server = new CurrencyServer();
            server.LogMessage += Server_LogMessage;

            try
            {
                server.Start(port);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка запуска сервера: {ex.Message}");
                Console.WriteLine("Нажмите Enter для выхода...");
                Console.ReadLine();
                return;
            }
            Console.WriteLine();
            Console.WriteLine("Команды сервера:");
            Console.WriteLine("  status  — показать активных клиентов");
            Console.WriteLine("  stop    — остановить сервер");
            Console.WriteLine("  exit    — выход");
            Console.WriteLine();

            while (server.IsRunning)
            {
                string cmd = Console.ReadLine();
                if (cmd == null) continue;
                cmd = cmd.Trim().ToLower();

                switch (cmd)
                {
                    case "status":
                        PrintStatus();
                        break;

                    case "stop":
                        server.Stop();
                        break;

                    case "exit":
                        if (server.IsRunning) server.Stop();
                        return;

                    default:
                        if (!string.IsNullOrEmpty(cmd))
                            Console.WriteLine("Неизвестная команда.");
                        break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Нажмите Enter для выхода...");
            Console.ReadLine();
        }

        static void PrintStatus()
        {
            var clients = server.GetActiveClients();

            lock (consoleLock)
            {
                Console.WriteLine();
                Console.WriteLine("---------- СТАТУС СЕРВЕРА ----------");
                Console.WriteLine($"Порт: {server.Port}");
                Console.WriteLine($"Активных клиентов: {clients.Count}");
                Console.WriteLine();

                int i = 1;
                foreach (var c in clients)
                {
                    Console.WriteLine($"  [{i++}] {c.EndPoint}");
                    Console.WriteLine($"      Подключён:     {c.ConnectedAt:HH:mm:ss}");
                    Console.WriteLine($"      Последний зап.:{c.LastRequestAt?.ToString("HH:mm:ss") ?? "—"}");
                    Console.WriteLine($"      Запросов:      {c.Requests.Count}");
                }

                Console.WriteLine("-------------------------------------");
                Console.WriteLine();
            }
        }
        static void Server_LogMessage(object sender, string message)
        {
            lock (consoleLock)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            }
        }
    }
}
