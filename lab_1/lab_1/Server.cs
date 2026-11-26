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
        private ReplicationManager _replicationManager;
        private TcpListener _tcpListener;
        private Service _service;
        private Database _db;
        private bool _isRunning;
        private int _clientCounter = 0;
        private EventPublisher _publisher;
        private EventBroker _broker;
        private LoggerSubscriber _logger;
        private CarServiceSubscriber _carService;
        private NotificationServiceSubscriber _notification;
        private AnalyticsSubscriber _analytics;
        private EventBroker _analyticsBroker;
        private ClusterManager _clusterManager;
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
            //string primaryConn = "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";
            //string replicaConn = "Host=localhost;Port=5432;Database=car_rental_replica;Username=postgres;Password=postgres";

            string primaryConn = AppConfig.PrimaryConnection;
            string replicaConn = AppConfig.ReplicaConnection;

            _replicationManager = new ReplicationManager(primaryConn, replicaConn);
            _db = new Database(primaryConn);

            _broker = new EventBroker();
            _analyticsBroker = new EventBroker();

            _logger = new LoggerSubscriber();
            _carService = new CarServiceSubscriber(_db);
            _notification = new NotificationServiceSubscriber();
            _analytics = new AnalyticsSubscriber();

            _broker.Subscribe("RentalCreated", _carService);
            _broker.Subscribe("RentalCreated", _notification);
            _broker.Subscribe("RentalCancelled", _carService);
            _broker.Subscribe("CarReturned", _notification);

            _analyticsBroker.Subscribe("RentalCreated", _logger);
            _analyticsBroker.Subscribe("RentalCreated", _analytics);
            _analyticsBroker.Subscribe("RentalCancelled", _logger);
            _analyticsBroker.Subscribe("RentalCancelled", _analytics);
            _analyticsBroker.Subscribe("CarReturned", _logger);
            _analyticsBroker.Subscribe("CarReturned", _analytics);

            _publisher = new EventPublisher(_broker, _analyticsBroker);

            _clusterManager = new ClusterManager();

            _service = new Service(_db, _replicationManager, _publisher, _clusterManager);
            _isRunning = false;
        }

        public void Start()
        {
            try
            {   
                _tcpListener.Start();
                _isRunning = true;
                //Log("Сервер запущен на порту 3000", ConsoleColor.Green);
                Log($"Сервер запущен на порту {_tcpListener.LocalEndpoint}", ConsoleColor.Green);
                Log("Ожидание подключений...");

                Thread commandThread = new Thread(() =>
                {
                    while (_isRunning)
                    {
                        string cmd = Console.ReadLine();
                        if (string.IsNullOrEmpty(cmd)) continue;

                        switch (cmd.ToLower().Trim())
                        {
                            case "fail car":
                                _carService.IsAvailable = false;
                                Log("[COMMAND] CarService → UNAVAILABLE", ConsoleColor.Red);
                                break;
                            case "ok car":
                                _carService.IsAvailable = true;
                                Log("[COMMAND] CarService → AVAILABLE", ConsoleColor.Green);
                                break;
                            case "fail logger":
                                _logger.IsAvailable = false;
                                Log("[COMMAND] Logger → UNAVAILABLE", ConsoleColor.Red);
                                break;
                            case "ok logger":
                                _logger.IsAvailable = true;
                                Log("[COMMAND] Logger → AVAILABLE", ConsoleColor.Green);
                                break;
                            case "fail notify":
                                _notification.IsAvailable = false;
                                Log("[COMMAND] Notification → UNAVAILABLE", ConsoleColor.Red);
                                break;
                            case "ok notify":
                                _notification.IsAvailable = true;
                                Log("[COMMAND] Notification → AVAILABLE", ConsoleColor.Green);
                                break;
                            case "fail analytics":
                                _analytics.IsAvailable = false;
                                Log("[COMMAND] Analytics → UNAVAILABLE", ConsoleColor.Red);
                                break;
                            case "ok analytics":
                                _analytics.IsAvailable = true;
                                Log("[COMMAND] Analytics → AVAILABLE", ConsoleColor.Green);
                                break;

                            case "fail broker":
                                _broker.IsAvailable = false;
                                Log("[COMMAND] Основной Broker → UNAVAILABLE", ConsoleColor.Red);
                                break;
                            case "ok broker":
                                _broker.IsAvailable = true;
                                Log("[COMMAND] Основной Broker → AVAILABLE", ConsoleColor.Green);
                                break;
                            case "fail analytics broker":
                                _analyticsBroker.IsAvailable = false;
                                Log("[COMMAND] Analytics Broker → UNAVAILABLE", ConsoleColor.Red);
                                break;
                            case "ok analytics brokers":
                                _analyticsBroker.IsAvailable = true;
                                Log("[COMMAND] Analytics Broker → AVAILABLE", ConsoleColor.Green);
                                break;


                            case "fail node 5":
                                _clusterManager.FailNode(5);
                                break;
                            case "ok node 5":
                                _clusterManager.RestoreNode(5);
                                break;
                            case "fail node 4":
                                _clusterManager.FailNode(4);
                                break;
                            case "ok node 4":
                                _clusterManager.RestoreNode(4);
                                break;
                            case "fail node 3":
                                _clusterManager.FailNode(3);
                                break;
                            case "ok node 3":
                                _clusterManager.RestoreNode(3);
                                break;
                            case "fail node 2":
                                _clusterManager.FailNode(2);
                                break;
                            case "ok node 2":
                                _clusterManager.RestoreNode(2);
                                break;
                            case "fail node 1":
                                _clusterManager.FailNode(1);
                                break;
                            case "ok node 1":
                                _clusterManager.RestoreNode(1);
                                break;
                        }
                    }
                });
                commandThread.IsBackground = true;
                commandThread.Start();

                var db = new Database(_replicationManager.GetActiveConnectionString());
                Log($"Загружено: {db.GetAllCustomers().Count} клиентов, {db.GetAllCars().Count} машин");

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
            //_db.SaveData();
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
                                await Task.Delay(500);   // 500 мс
                            }

                            Response response = _service.ProcessRequest(request);

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