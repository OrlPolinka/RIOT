using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using lab_1;

namespace lab_1.Tests
{
    public class LoadTesting
    {
        private readonly ITestOutputHelper _output;

        public LoadTesting(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DockerServer_Handles_50_Parallel_Requests()
        {
            _output.WriteLine("===== НАГРУЗОЧНЫЙ ТЕСТ: 50 клиентов одновременно =====");

            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task>();
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var client = new Client();
                    if (client.Connect("127.0.0.1", 3000))
                    {
                        var response = client.SendRequest(client.CreateGetAllCarsRequest());
                        if (response.Success) System.Threading.Interlocked.Increment(ref successCount);
                        else System.Threading.Interlocked.Increment(ref failCount);
                        client.Disconnect();
                    }
                    else
                    {
                        System.Threading.Interlocked.Increment(ref failCount);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            _output.WriteLine($"Время выполнения: {stopwatch.ElapsedMilliseconds} мс");
            _output.WriteLine($"Успешных запросов: {successCount}");
            _output.WriteLine($"Ошибок: {failCount}");

            Assert.Equal(50, successCount);
            Assert.Equal(0, failCount);

            _output.WriteLine("ТЕСТ ПРОЙДЕН! Docker-контейнер выдержал нагрузку.");
        }
    }
}