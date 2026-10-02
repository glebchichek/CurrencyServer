using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CurrencyClient
{
    public class CurrencyClientConnection
    {
        private TcpClient client;
        private NetworkStream stream;
        private CancellationTokenSource cts;

        public bool IsConnected => client?.Connected == true;

        public event EventHandler<string> LogMessage;
        public event EventHandler<string> ResponseReceived;
        public event EventHandler Connected;
        public event EventHandler Disconnected;

        public async Task ConnectAsync(string host, int port)
        {
            if (IsConnected)
                throw new InvalidOperationException("Уже подключены");

            client = new TcpClient();
            await client.ConnectAsync(host, port);
            stream = client.GetStream();
            cts = new CancellationTokenSource();

            RaiseLog($"Подключено к {host}:{port}");
            Connected?.Invoke(this, EventArgs.Empty);

            _ = Task.Run(() => ReceiveLoopAsync(cts.Token));
        }

        public void Disconnect()
        {
            try
            {
                cts?.Cancel();
                stream?.Close();
                client?.Close();

                RaiseLog("Соединение закрыто");
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                RaiseLog($"Ошибка отключения: {ex.Message}");
            }
        }

        public async Task SendRequestAsync(string request)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Нет соединения с сервером");

            if (string.IsNullOrWhiteSpace(request))
                return;

            byte[] data = Encoding.UTF8.GetBytes(request);
            await stream.WriteAsync(data, 0, data.Length);

            RaiseLog($"Отправлено: \"{request}\"");
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[4096];

            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break;

                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    ResponseReceived?.Invoke(this, response);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    RaiseLog($"Ошибка приёма: {ex.Message}");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RaiseLog(string message)
        {
            LogMessage?.Invoke(this, message);
        }
    }
}
