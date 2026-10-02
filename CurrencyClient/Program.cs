using System.Text;

namespace CurrencyClient
{
    internal class Program
    {
        static CurrencyClientConnection connection;
        static readonly object consoleLock = new object();
        static bool waitingForResponse = false;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("============================================================");
            Console.WriteLine("КЛИЕНТ КУРСОВ ВАЛЮТ");
            Console.WriteLine("============================================================");
            Console.WriteLine();

            Console.Write("Хост (по умолчанию 127.0.0.1): ");
            string host = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

            Console.Write("Порт (по умолчанию 8888): ");
            string portInput = Console.ReadLine();
            int port = 8888;
            if (!string.IsNullOrWhiteSpace(portInput) &&
                int.TryParse(portInput, out int p) && p > 0 && p < 65536)
            {
                port = p;
            }

            connection = new CurrencyClientConnection();
            connection.LogMessage += Connection_LogMessage;
            connection.ResponseReceived += Connection_ResponseReceived;
            connection.Connected += Connection_Connected;
            connection.Disconnected += Connection_Disconnected;

            try
            {
                await connection.ConnectAsync(host, port);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                Console.WriteLine("Нажмите Enter для выхода...");
                Console.ReadLine();
                return;
            }
            await Task.Delay(200);

            Console.WriteLine();
            Console.WriteLine("Команды:");
            Console.WriteLine("  <FROM> <TO>   — запросить курс (например: USD EUR)");
            Console.WriteLine("  exit          — отключиться и выйти");
            Console.WriteLine();

            while (connection.IsConnected)
            {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (line == null) continue;
                line = line.Trim();

                if (line.Length == 0) continue;

                if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    connection.Disconnect();
                    break;
                }

                try
                {
                    waitingForResponse = true;
                    await connection.SendRequestAsync(line);

                    int waited = 0;
                    while (waitingForResponse && waited < 3000)
                    {
                        await Task.Delay(50);
                        waited += 50;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка отправки: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Нажмите Enter для выхода...");
            Console.ReadLine();
        }

        static void Connection_LogMessage(object sender, string message)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                Console.ResetColor();
            }
        }

        static void Connection_ResponseReceived(object sender, string response)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine($"  >>> ОТВЕТ СЕРВЕРА: {response}");
                Console.ResetColor();
                Console.WriteLine();
            }

            waitingForResponse = false;
        }

        static void Connection_Connected(object sender, EventArgs e)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(">>> Подключение установлено");
                Console.ResetColor();
            }
        }

        static void Connection_Disconnected(object sender, EventArgs e)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(">>> Отключено от сервера");
                Console.ResetColor();
            }
        }
    }
}
