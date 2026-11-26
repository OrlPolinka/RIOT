using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using lab_1;

namespace lab_1_Semaphore.Tests
{

    public class SynchronizationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Server _server;
        private readonly Thread _serverThread;
        private readonly int _port;
        private readonly string _dataStorePath;
        private readonly string _dataStoreBackup;

        public SynchronizationTests(ITestOutputHelper output)
        {
            _output = output;

            _dataStorePath = Path.Combine(AppContext.BaseDirectory, "DataStore.json");
            _dataStoreBackup = File.Exists(_dataStorePath) ? File.ReadAllText(_dataStorePath) : null;

            _port = 5000 + new Random().Next(1, 1000);

            _server = new Server(_port);
            _serverThread = new Thread(() => _server.Start()) { IsBackground = true };
            _serverThread.Start();

            Thread.Sleep(800);
        }

        // ЧАСТЬ 1

        [Fact]
        public void Test1_1_OneClient_RentalSucceeds()
        {
            _output.WriteLine("===== ТЕСТ 1.1: Один клиент, успешная аренда =====");

            var dataStore = new HelpData("DataStore.json");
            var freeCar = dataStore.Cars.FirstOrDefault(c => c.IsAvailable);
            Assert.NotNull(freeCar);

            _output.WriteLine($"Свободная машина: {freeCar.Brand} {freeCar.Model} (ID={freeCar.Id})");

            var client = new Client();
            Assert.True(client.Connect("127.0.0.1", _port));

            var response = client.SendRequest(client.CreateRentalRequest(1, freeCar.Id, 1, 3));

            _output.WriteLine($"Success: {response.Success}");
            _output.WriteLine($"Error: {response.ErrorMessage ?? "нет"}");

            Assert.True(response.Success, "Аренда должна быть успешной");

            var finalData = new HelpData("DataStore.json");
            var finalCar = finalData.Cars.First(c => c.Id == freeCar.Id);
            Assert.False(finalCar.IsAvailable, "Машина должна быть занята");

            _output.WriteLine("ТЕСТ 1.1 ПРОЙДЕН!");
            client.Disconnect();
        }

        [Fact]
        public async Task Test1_2_TwoClients_SameCar_OnlyOneSucceeds()
        {
            _output.WriteLine("===== ТЕСТ 1.2: Два клиента, одна машина (синхронизация) =====");

            var dataStore = new HelpData("DataStore.json");
            var freeCar = dataStore.Cars.FirstOrDefault(c => c.IsAvailable);
            Assert.NotNull(freeCar);

            int carId = freeCar.Id;
            _output.WriteLine($"Машина: {freeCar.Brand} {freeCar.Model} (ID={carId})");

            var client1 = new Client();
            var client2 = new Client();
            Assert.True(client1.Connect("127.0.0.1", _port));
            Assert.True(client2.Connect("127.0.0.1", _port));

            var task1 = Task.Run(() => client1.SendRequest(client1.CreateRentalRequest(1, carId, 1, 3)));
            var task2 = Task.Run(() => client2.SendRequest(client2.CreateRentalRequest(2, carId, 1, 3)));

            var results = await Task.WhenAll(task1, task2);

            //Thread.Sleep(5000);

            int successCount = results.Count(r => r.Success);
            int failCount = results.Count(r => !r.Success);

            _output.WriteLine($"Успешных: {successCount}");
            _output.WriteLine($"Отклонённых: {failCount}");

            for (int i = 0; i < results.Length; i++)
                _output.WriteLine($"Client {i + 1}: Success={results[i].Success}, Error={results[i].ErrorMessage ?? "нет"}");

            Assert.Equal(1, successCount);
            Assert.Equal(1, failCount);

            var failed = results.First(r => !r.Success);
            Assert.Contains("уже арендована", failed.ErrorMessage);

            //var checkClient = new Client();
            //checkClient.Connect("127.0.0.1", _port);

            //var rentalsResp = checkClient.SendRequest(checkClient.CreateGetAllRentalsRequest());
            //var rentals = JsonSerializer.Deserialize<List<Rental>>(rentalsResp.Result);

            //int rentalsForCar = rentals.Count(r => r.CarId == carId && r.Status == "Active");
            //Assert.Equal(1, rentalsForCar);

            _output.WriteLine("ТЕСТ 1.2 ПРОЙДЕН! (Синхронизация работает)");
            client1.Disconnect();
            client2.Disconnect();
        }


        // ЧАСТЬ 2

        [Fact]
        public void Test2_1_TimeRequest_ReturnsServerTime()
        {
            _output.WriteLine("===== ТЕСТ 2.1: TimeRequest возвращает время сервера =====");

            var client = new Client();
            Assert.True(client.Connect("127.0.0.1", _port));

            var before = DateTime.UtcNow;
            var response = client.SendRequest(client.CreateTimeRequest());
            var after = DateTime.UtcNow;

            Assert.True(response.Success);
            Assert.Equal("TimeRequest", response.Operation);

            var doc = JsonDocument.Parse(response.Result);
            var serverTime = doc.RootElement.GetProperty("ServerTime").GetDateTime();

            _output.WriteLine($"Before:     {before:HH:mm:ss.fff}");
            _output.WriteLine($"ServerTime: {serverTime:HH:mm:ss.fff}");
            _output.WriteLine($"After:      {after:HH:mm:ss.fff}");

            Assert.True(serverTime >= before.AddSeconds(-1));
            Assert.True(serverTime <= after.AddSeconds(1));

            _output.WriteLine("ТЕСТ 2.1 ПРОЙДЕН!");
            client.Disconnect();
        }

        [Fact]
        public void Test2_2_TimeRequest_RTTIsPositive()
        {
            _output.WriteLine("===== ТЕСТ 2.2: RTT измеряется =====");

            var client = new Client();
            Assert.True(client.Connect("127.0.0.1", _port));

            var requestTime = DateTime.UtcNow;
            var response = client.SendRequest(client.CreateTimeRequest());
            var responseTime = DateTime.UtcNow;

            Assert.True(response.Success);

            var rtt = responseTime - requestTime;
            _output.WriteLine($"RTT: {rtt.TotalMilliseconds:F0} мс");

            Assert.True(rtt.TotalMilliseconds > 0, "RTT должен быть > 0");

            Assert.True(rtt.TotalMilliseconds < 5000, "RTT не должен быть > 5 сек");

            _output.WriteLine("ТЕСТ 2.2 ПРОЙДЕН!");
            client.Disconnect();
        }

        [Fact]
        public void Test2_3_ClockOffset_Calculated()
        {
            _output.WriteLine("===== ТЕСТ 2.3: Расчёт смещения часов =====");

            var client = new Client();
            client.SetClockOffset(TimeSpan.FromSeconds(7));

            Assert.True(client.Connect("127.0.0.1", _port));

            var requestTime = DateTime.UtcNow;
            var response = client.SendRequest(client.CreateTimeRequest());
            var responseTime = DateTime.UtcNow;

            Assert.True(response.Success);

            var rtt = responseTime - requestTime;
            var networkDelay = rtt / 2;

            var doc = JsonDocument.Parse(response.Result);
            var serverTime = doc.RootElement.GetProperty("ServerTime").GetDateTime();

            var clientLocal = client.GetLocalTime();
            var offset = (serverTime + networkDelay) - clientLocal;

            _output.WriteLine($"Client offset:  +7 сек");
            _output.WriteLine($"Server time:    {serverTime:HH:mm:ss.fff}");
            _output.WriteLine($"Client local:   {clientLocal:HH:mm:ss.fff}");
            _output.WriteLine($"RTT:            {rtt.TotalMilliseconds:F0} мс");
            _output.WriteLine($"Offset:         {offset.TotalSeconds:+0.000;-0.000;0.000} сек");

            Assert.True(offset.TotalSeconds < -5.0 && offset.TotalSeconds > -9.0,
                $"Offset должен быть около -7 сек, а получен {offset.TotalSeconds:F3}");

            _output.WriteLine("ТЕСТ 2.3 ПРОЙДЕН!");
            client.Disconnect();
        }

        [Fact]
        public void Test2_4_CorrectedTime_CloseToServerTime()
        {
            _output.WriteLine("===== ТЕСТ 2.4: CorrectedTime близко к ServerTime =====");

            var client = new Client();
            client.SetClockOffset(TimeSpan.FromSeconds(7));

            Assert.True(client.Connect("127.0.0.1", _port));

            var requestTime = DateTime.UtcNow;
            var response = client.SendRequest(client.CreateTimeRequest());
            var responseTime = DateTime.UtcNow;

            Assert.True(response.Success);

            var rtt = responseTime - requestTime;
            var networkDelay = rtt / 2;

            var doc = JsonDocument.Parse(response.Result);
            var serverTime = doc.RootElement.GetProperty("ServerTime").GetDateTime();

            var offset = (serverTime + networkDelay) - client.GetLocalTime();
            client.SetServerTimeOffset(offset);

            var correctedTime = client.GetCorrectedTime();

            _output.WriteLine($"Server Time:    {serverTime:HH:mm:ss.fff}");
            _output.WriteLine($"Corrected Time: {correctedTime:HH:mm:ss.fff}");

            var diff = Math.Abs((correctedTime - serverTime).TotalMilliseconds);
            _output.WriteLine($"Разница: {diff:F0} мс");

            Assert.True(diff < rtt.TotalMilliseconds + 100,
                $"Corrected Time должен быть близок к Server Time (разница {diff:F0} мс, RTT {rtt.TotalMilliseconds:F0} мс)");

            _output.WriteLine("ТЕСТ 2.4 ПРОЙДЕН!");
            client.Disconnect();
        }

        [Fact]
        public void Test2_5_TwoClients_DifferentOffsets_BothSync()
        {
            _output.WriteLine("===== ТЕСТ 2.5: Два клиента с разными offset =====");

            var client1 = new Client();
            var client2 = new Client();

            client1.SetClockOffset(TimeSpan.FromSeconds(7));
            client2.SetClockOffset(TimeSpan.FromSeconds(-5));

            Assert.True(client1.Connect("127.0.0.1", _port));
            Assert.True(client2.Connect("127.0.0.1", _port));

            var req1 = client1.CreateTimeRequest();
            var t1 = DateTime.UtcNow;
            var resp1 = client1.SendRequest(req1);
            var t2 = DateTime.UtcNow;
            Assert.True(resp1.Success);

            var doc1 = JsonDocument.Parse(resp1.Result);
            var serverTime1 = doc1.RootElement.GetProperty("ServerTime").GetDateTime();
            var rtt1 = t2 - t1;
            var offset1 = (serverTime1 + rtt1 / 2) - client1.GetLocalTime();
            client1.SetServerTimeOffset(offset1);

            var req2 = client2.CreateTimeRequest();
            var t3 = DateTime.UtcNow;
            var resp2 = client2.SendRequest(req2);
            var t4 = DateTime.UtcNow;
            Assert.True(resp2.Success);

            var doc2 = JsonDocument.Parse(resp2.Result);
            var serverTime2 = doc2.RootElement.GetProperty("ServerTime").GetDateTime();
            var rtt2 = t4 - t3;
            var offset2 = (serverTime2 + rtt2 / 2) - client2.GetLocalTime();
            client2.SetServerTimeOffset(offset2);

            var corrected1 = client1.GetCorrectedTime();
            var corrected2 = client2.GetCorrectedTime();

            _output.WriteLine($"Client 1 offset: {offset1.TotalSeconds:+0.000;-0.000;0.000} сек");
            _output.WriteLine($"Client 2 offset: {offset2.TotalSeconds:+0.000;-0.000;0.000} сек");
            _output.WriteLine($"Client 1 Corrected: {corrected1:HH:mm:ss.fff}");
            _output.WriteLine($"Client 2 Corrected: {corrected2:HH:mm:ss.fff}");

            var diffBetweenClients = Math.Abs((corrected1 - corrected2).TotalMilliseconds);
            _output.WriteLine($"Разница между клиентами: {diffBetweenClients:F0} мс");

            Assert.True(diffBetweenClients < 2000,
                $"После синхронизации клиенты должны показывать близкое время (разница {diffBetweenClients:F0} мс)");

            _output.WriteLine("ТЕСТ 2.5 ПРОЙДЕН!");
            client1.Disconnect();
            client2.Disconnect();
        }

    }
}