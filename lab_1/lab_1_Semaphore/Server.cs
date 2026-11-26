using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace lab_1
{
    public class Server
    {
        private TcpListener _tcpListener;
        private Service _service;
        private HelpData _dataStore;
        private bool _isRunning;
        private int _clientCounter = 0;
        private void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            Console.ForegroundColor = oldColor;
        }


        public Server(int port)
        {
            _tcpListener = new TcpListener(IPAddress.Any, port);
            _dataStore = new HelpData("DataStore.json");
            _service = new Service(_dataStore);
            _isRunning = false;
        }

        public void Start()
        {
            try
            {
                _tcpListener.Start();
                _isRunning = true;
                Log("Сервер запущен на порту 3000", ConsoleColor.Green);
                Log("Ожидание подключений...");
                Log($"Загружено: {_dataStore.Customers.Count} клиентов, {_dataStore.Cars.Count} машин");

                while (_isRunning)
                {
                    try
                    {
                        TcpClient client = _tcpListener.AcceptTcpClient();
                        int clientId = Interlocked.Increment(ref _clientCounter);
                        _ = Task.Run(() => HandleClientAsync(client, clientId));
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException ex) when (!_isRunning)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                            Log($"Ошибка при принятии клиента: {ex.Message}", ConsoleColor.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при запуске: {ex.Message}", ConsoleColor.Red);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _tcpListener?.Stop();
            _dataStore.SaveData();
            Console.WriteLine("[Сервер] Остановлен");
        }

        private async Task HandleClientAsync(TcpClient client, int clientId)
        {
            int taskId = Task.CurrentId ?? Thread.CurrentThread.ManagedThreadId;
            string clientInfo = $"Client {clientId} (Task {taskId})";

            Log($"{clientInfo} → START", ConsoleColor.Cyan);

            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    while (client.Connected && _isRunning)
                    {
                        byte[] lengthBuffer = new byte[4];
                        int totalRead = 0;
                        while (totalRead < 4)
                        {
                            int bytesRead = await stream.ReadAsync(lengthBuffer, totalRead, 4 - totalRead);
                            if (bytesRead == 0) break;
                            totalRead += bytesRead;
                        }
                        if (totalRead < 4) break;

                        int requestLength = BitConverter.ToInt32(lengthBuffer, 0);

                        byte[] requestBuffer = new byte[requestLength];
                        totalRead = 0;
                        while (totalRead < requestLength)
                        {
                            int bytesRead = await stream.ReadAsync(requestBuffer, totalRead, requestLength - totalRead);
                            if (bytesRead == 0) break;
                            totalRead += bytesRead;
                        }
                        if (totalRead < requestLength) break;

                        string requestJson = Encoding.UTF8.GetString(requestBuffer, 0, requestLength);
                        Log($"{clientInfo} → Получен запрос: {requestJson}", ConsoleColor.Yellow);

                        try
                        {
                            Request request = JsonSerializer.Deserialize<Request>(requestJson);

                            Log($"{clientInfo} → Операция: {request.Operation} (RequestId: {request.RequestId})", ConsoleColor.Magenta);

                            await Task.Delay(3000);

                            if (request.Operation == "TimeRequest")
                            {
                                await Task.Delay(500);   
                            }

                            Response response = await _service.ProcessRequest(request);

                            string responseJson = JsonSerializer.Serialize(response);
                            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);


                            byte[] lengthBytes = BitConverter.GetBytes(responseBytes.Length);
                            await stream.WriteAsync(lengthBytes, 0, 4);
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                            //await  stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                            Log($"{clientInfo} → Ответ отправлен", ConsoleColor.Green);
                        }
                        catch (Exception ex)
                        {
                            Log($"{clientInfo} → Ошибка обработки: {ex.Message}", ConsoleColor.Red);

                            var errorResponse = new Response
                            {
                                Success = false,
                                ErrorMessage = $"Ошибка обработки запроса: {ex.Message}"
                            };
                            string errorJson = JsonSerializer.Serialize(errorResponse);
                            byte[] errorBytes = Encoding.UTF8.GetBytes(errorJson);
                            stream.Write(errorBytes, 0, errorBytes.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"{clientInfo} → Ошибка: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                client.Close();
                Log($"{clientInfo} → END", ConsoleColor.Cyan);
            }
        }
    }
}