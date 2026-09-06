using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace lab_1
{
    public class Server
    {
        private TcpListener _tcpListener;
        private Service _service;
        private HelpData _dataStore;
        private bool _isRunning;

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
                Console.WriteLine($"[Сервер] Запущен на порту 3000");
                Console.WriteLine("[Сервер] Ожидание подключений...");
                Console.WriteLine($"[Сервер] Загружено: {_dataStore.Customers.Count} клиентов, {_dataStore.Cars.Count} машин");

                while (_isRunning)
                {
                    try
                    {
                        TcpClient client = _tcpListener.AcceptTcpClient();
                        Console.WriteLine("[Сервер] Подключен клиент");

                        Thread clientThread = new Thread(() => HandleClient(client));
                        clientThread.Start();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Сервер] Ошибка при принятии клиента: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Сервер] Ошибка при запуске: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _tcpListener?.Stop();
            _dataStore.SaveData();
            Console.WriteLine("[Сервер] Остановлен");
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    while (client.Connected && _isRunning)
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;

                        try
                        {
                            bytesRead = stream.Read(buffer, 0, buffer.Length);
                        }
                        catch
                        {
                            break;
                        }

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        string requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"[Сервер] Получен запрос: {requestJson}");

                        try
                        {
                            Request request = JsonSerializer.Deserialize<Request>(requestJson);
                            Response response = _service.ProcessRequest(request);

                            string responseJson = JsonSerializer.Serialize(response);
                            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                            stream.Write(responseBytes, 0, responseBytes.Length);
                            Console.WriteLine($"[Сервер] Отправлен ответ");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Сервер] Ошибка обработки запроса: {ex.Message}");
                            
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
                Console.WriteLine($"[Сервер] Ошибка при обработке клиента: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine("[Сервер] Клиент отключен");
            }
        }
    }
}