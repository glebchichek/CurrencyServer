using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CurrencyServer
{
    public class CurrencyServer
    {
        private TcpListener listener;
        private CancellationTokenSource cts;
        private bool isRunning;

        private readonly List<ClientSession> activeClients = new List<ClientSession>();
        private readonly object clientsLock = new object();

        private readonly Dictionary<string, double> ratesToUsd =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "USD", 1.0 },
            { "EUR", 0.92 },
            { "UAH", 41.50 },
            { "PLN", 4.05 },
            { "GBP", 0.79 },
            { "JPY", 149.50 },
            { "CNY", 7.25 },
            { "CHF", 0.88 },
            { "CAD", 1.36 },
            { "TRY", 34.20 },
        };

        public event EventHandler<string> LogMessage;
        public bool IsRunning => isRunning;
        public int Port { get; private set; }

        public void Start(int port)
        {
            if (isRunning)
                throw new InvalidOperationException("Сервер уже запущен");

            Port = port;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            isRunning = true;
            cts = new CancellationTokenSource();

            RaiseLog($"Сервер запущен на порту {port}");
            RaiseLog("Ожидание клиентов...");

            Task.Run(() => AcceptLoop(cts.Token));
        }

        public void Stop()
        {
            if (!isRunning) return;

            isRunning = false;
            cts?.Cancel();
            listener?.Stop();

            lock (clientsLock)
            {
                foreach (var client in activeClients)
                {
                    try { client.Close(); } catch { }
                }
                activeClients.Clear();
            }

            RaiseLog("Сервер остановлен");
        }

        private async Task AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        RaiseLog($"Ошибка приёма клиента: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            string endPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            var session = new ClientSession
            {
                EndPoint = endPoint,
                ConnectedAt = DateTime.Now
            };

            lock (clientsLock) activeClients.Add(session);

            RaiseLog($"Клиент подключён: {endPoint}");

            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096];

                    while (!token.IsCancellationRequested && client.Connected)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (bytesRead == 0) break;

                        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        session.Requests.Add(request);
                        session.LastRequestAt = DateTime.Now;

                        RaiseLog($"Запрос от {endPoint}: \"{request}\"");

                        string response = ProcessRequest(request);

                        RaiseLog($"Ответ: \"{response}\"");

                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length, token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    RaiseLog($"Ошибка клиента {endPoint}: {ex.Message}");
            }
            finally
            {
                session.DisconnectedAt = DateTime.Now;
                lock (clientsLock) activeClients.Remove(session);

                RaiseLog($"Клиент отключён: {endPoint} " +
                         $"(подключён с {session.ConnectedAt:HH:mm:ss}, " +
                         $"запросов: {session.Requests.Count})");
            }
        }
        private string ProcessRequest(string request)
        {
            if (string.IsNullOrWhiteSpace(request))
                return "Ошибка: пустой запрос";

            string[] parts = request.Split(new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                return "Ошибка: нужно два кода валюты через пробел, например: USD EUR";

            string from = parts[0].ToUpperInvariant();
            string to = parts[1].ToUpperInvariant();

            if (!ratesToUsd.ContainsKey(from))
                return $"Ошибка: неизвестная валюта \"{from}\"";

            if (!ratesToUsd.ContainsKey(to))
                return $"Ошибка: неизвестная валюта \"{to}\"";

            double rate = ratesToUsd[from] / ratesToUsd[to];
            return $"1 {from} = {rate:F6} {to}";
        }

        public List<ClientSession> GetActiveClients()
        {
            lock (clientsLock)
                return new List<ClientSession>(activeClients);
        }

        private void RaiseLog(string message)
        {
            LogMessage?.Invoke(this, message);
        }
    }

    public class ClientSession
    {
        public string EndPoint { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime? DisconnectedAt { get; set; }
        public DateTime? LastRequestAt { get; set; }
        public List<string> Requests { get; set; } = new List<string>();

        public void Close()
        {

        }
    }
}