//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Net.Sockets;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;
//using Xunit.Abstractions;

//namespace lab_1.Tests
//{
//    /// <summary>
//    /// Интеграционные тесты параллельной обработки клиентов сервером.
//    /// Каждый тест запускает сервер на отдельном порту и работает с реальными TCP-клиентами.
//    /// </summary>
//    public class ConcurrencyTests : IDisposable
//    {
//        private readonly ITestOutputHelper _output;
//        private readonly Server _server;
//        private readonly Thread _serverThread;
//        private readonly int _port;

//        public ConcurrencyTests(ITestOutputHelper output)
//        {
//            _output = output;

//            _port = 4000 + new Random().Next(1, 1000);

//            _server = new Server(_port);
//            _serverThread = new Thread(() => _server.Start()) { IsBackground = true };
//            _serverThread.Start();

//            Thread.Sleep(800);
//        }

//        [Fact]
//        public void Test1_OneClient_CorrectProcessing()
//        {
//            _output.WriteLine("===== ТЕСТ 1: Один клиент =====");

//            var client = new Client();
//            Assert.True(client.Connect("127.0.0.1", _port), "Клиент должен подключиться");

//            var request = client.CreateSearchCarRequest(null, null, null);
//            var sw = Stopwatch.StartNew();
//            var response = client.SendRequest(request);
//            sw.Stop();

//            _output.WriteLine($"  Ответ получен за {sw.ElapsedMilliseconds} мс");
//            _output.WriteLine($"  Success: {response.Success}");
//            _output.WriteLine($"  Operation: {response.Operation}");

//            Assert.True(response.Success, "Запрос должен выполниться успешно");
//            Assert.Equal("SearchCar", response.Operation);

//            var cars = JsonSerializer.Deserialize<List<Car>>(response.Result);
//            Assert.NotNull(cars);
//            Assert.NotEmpty(cars);

//            _output.WriteLine($"Найдено машин: {cars.Count}");
//            _output.WriteLine("ТЕСТ 1 ПРОЙДЕН!");

//            client.Disconnect();
//        }

//        [Fact]
//        public void Test2_TwoClients_BothProcessed()
//        {
//            _output.WriteLine("===== ТЕСТ 2: Два клиента =====");

//            var client1 = new Client();
//            var client2 = new Client();

//            Assert.True(client1.Connect("127.0.0.1", _port), "Client 1 должен подключиться");
//            Assert.True(client2.Connect("127.0.0.1", _port), "Client 2 должен подключиться");

//            var req1 = client1.CreateGetAllCarsRequest();
//            var resp1 = client1.SendRequest(req1);

//            var req2 = client2.CreateGetAllCustomersRequest();
//            var resp2 = client2.SendRequest(req2);

//            _output.WriteLine($"Client 1: Success={resp1.Success}, Operation={resp1.Operation}");
//            _output.WriteLine($"Client 2: Success={resp2.Success}, Operation={resp2.Operation}");

//            Assert.True(resp1.Success, "Client 1 должен получить успешный ответ");
//            Assert.True(resp2.Success, "Client 2 должен получить успешный ответ");
//            Assert.Equal("GetAllCars", resp1.Operation);
//            Assert.Equal("GetAllCustomers", resp2.Operation);

//            var cars = JsonSerializer.Deserialize<List<Car>>(resp1.Result);
//            var customers = JsonSerializer.Deserialize<List<Customer>>(resp2.Result);

//            Assert.NotEmpty(cars);
//            Assert.NotEmpty(customers);

//            _output.WriteLine("Оба клиента обработаны успешно");
//            _output.WriteLine("ТЕСТ 2 ПРОЙДЕН!");

//            client1.Disconnect();
//            client2.Disconnect();
//        }

//        [Fact]
//        public async Task Test3_ParallelProcessing_Client2StartsBeforeClient1Ends()
//        {
//            _output.WriteLine("===== ТЕСТ 3: Параллельная обработка =====");

//            var client1 = new Client();
//            var client2 = new Client();

//            Assert.True(client1.Connect("127.0.0.1", _port), "Client 1 должен подключиться");
//            Assert.True(client2.Connect("127.0.0.1", _port), "Client 2 должен подключиться");

//            DateTime timeClient1Start = DateTime.MinValue;
//            DateTime timeClient1End = DateTime.MinValue;
//            DateTime timeClient2Start = DateTime.MinValue;
//            DateTime timeClient2End = DateTime.MinValue;

//            var task1 = Task.Run(() =>
//            {
//                timeClient1Start = DateTime.Now;
//                _output.WriteLine($"[{timeClient1Start:HH:mm:ss.fff}] Client 1 → START");

//                var req = client1.CreateSearchCarRequest(null, null, null);
//                var resp = client1.SendRequest(req);


//                timeClient1End = DateTime.Now;
//                _output.WriteLine($"[{timeClient1End:HH:mm:ss.fff}] Client 1 → END");
//                return resp;
//            });

//            await Task.Delay(1500);

//            var task2 = Task.Run(() =>
//            {
//                timeClient2Start = DateTime.Now;
//                _output.WriteLine($"[{timeClient2Start:HH:mm:ss.fff}] Client 2 → START");

//                var req = client2.CreateGetAllCarsRequest();
//                var resp = client2.SendRequest(req);

//                timeClient2End = DateTime.Now;
//                _output.WriteLine($"[{timeClient2End:HH:mm:ss.fff}] Client 2 → END");
//                return resp;
//            });

//            await Task.WhenAll(task1, task2);

//            _output.WriteLine("");
//            _output.WriteLine("===== Временные метки =====");
//            _output.WriteLine($"Client 1: {timeClient1Start:HH:mm:ss.fff} → {timeClient1End:HH:mm:ss.fff} ({(timeClient1End - timeClient1Start).TotalSeconds:F2} сек)");
//            _output.WriteLine($"Client 2: {timeClient2Start:HH:mm:ss.fff} → {timeClient2End:HH:mm:ss.fff} ({(timeClient2End - timeClient2Start).TotalSeconds:F2} сек)");

//            Assert.True(
//                timeClient2Start < timeClient1End,
//                $"Client 2 должен начать ({timeClient2Start:HH:mm:ss.fff}) ДО завершения Client 1 ({timeClient1End:HH:mm:ss.fff})");

//            Assert.True(task1.Result.Success, "Client 1 должен получить ответ");
//            Assert.True(task2.Result.Success, "Client 2 должен получить ответ");

//            _output.WriteLine("");
//            _output.WriteLine($"Client 2 начал обработку за {(timeClient1End - timeClient2Start).TotalSeconds:F2} сек до завершения Client 1");
//            _output.WriteLine("ТЕСТ 3 ПРОЙДЕН! (Параллельная обработка подтверждена)");

//            client1.Disconnect();
//            client2.Disconnect();
//        }

//        [Fact]
//        public async Task Test4_RaceCondition_TwoClientsRentSameCar()
//        {
//            _output.WriteLine("===== ТЕСТ 4: ДЕМОНСТРАЦИЯ RACE CONDITION =====");

//            var dataStore = new HelpData("DataStore.json");
//            var availableCar = dataStore.Cars.FirstOrDefault(c => c.IsAvailable);

//            Assert.NotNull(availableCar);
//            _output.WriteLine($"Выбрана машина: {availableCar.Brand} {availableCar.Model} (ID={availableCar.Id})");
//            _output.WriteLine($"IsAvailable = {availableCar.IsAvailable}");

//            int carId = availableCar.Id;

//            var client1 = new Client();
//            var client2 = new Client();

//            Assert.True(client1.Connect("127.0.0.1", _port), "Client 1 должен подключиться");
//            Assert.True(client2.Connect("127.0.0.1", _port), "Client 2 должен подключиться");

//            var task1 = Task.Run(() =>
//            {
//                _output.WriteLine("[Client 1] Отправка запроса CreateRental...");
//                var req = client1.CreateRentalRequest(1, carId, 1, 3);
//                return client1.SendRequest(req);
//            });

//            var task2 = Task.Run(() =>
//            {
//                _output.WriteLine("[Client 2] Отправка запроса CreateRental...");
//                var req = client2.CreateRentalRequest(2, carId, 1, 3);
//                return client2.SendRequest(req);
//            });

//            var results = await Task.WhenAll(task1, task2);

//            _output.WriteLine("");
//            _output.WriteLine("===== Результаты =====");
//            for (int i = 0; i < results.Length; i++)
//            {
//                var r = results[i];
//                _output.WriteLine($"Client {i + 1}: Success={r.Success}, Error={r.ErrorMessage ?? "нет"}");
//            }

//            int successCount = results.Count(r => r.Success);
//            int failCount = results.Count(r => !r.Success);

//            _output.WriteLine("");
//            _output.WriteLine($"Успешных аренд: {successCount}");
//            _output.WriteLine($"Отклонённых аренд: {failCount}");

//            Assert.Equal(2, successCount);   
//            Assert.Equal(0, failCount);      

//            _output.WriteLine("");
//            _output.WriteLine("RACE CONDITION ПРОДЕМОНСТРИРОВАНА!");
//            _output.WriteLine("Оба клиента арендовали ОДНУ и ту же машину!");

//            var finalData = new HelpData("DataStore.json");
//            var finalCar = finalData.Cars.First(c => c.Id == carId);

//            _output.WriteLine("");
//            _output.WriteLine("===== Состояние общего ресурса =====");
//            _output.WriteLine($"Машина ID={carId}: IsAvailable={finalCar.IsAvailable}");
//            _output.WriteLine($"Аренд на эту машину: {finalData.Rentals.Count(r => r.CarId == carId)}");

//            Assert.False(finalCar.IsAvailable, "Машина помечена как занятая");

//            int rentalsForCar = finalData.Rentals.Count(r => r.CarId == carId);
//            Assert.Equal(2, rentalsForCar); 

//            _output.WriteLine("");
//            _output.WriteLine("ПРОБЛЕМА: в системе 2 аренды на 1 машину!");
//            _output.WriteLine("Это и есть Race Condition — некорректное состояние!");

//            client1.Disconnect();
//            client2.Disconnect();
//        }

//        public void Dispose()
//        {
//            _server?.Stop();
//            _serverThread?.Join(1000);
//        }
//    }
//}