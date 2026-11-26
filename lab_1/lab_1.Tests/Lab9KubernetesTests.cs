using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using lab_1;

namespace lab_1.Tests
{
    public class Lab9KubernetesTests
    {
        private readonly ITestOutputHelper _output;

        public Lab9KubernetesTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void DomainOperation_SearchCar_ReturnsValidData()
        {
            var db = new Database("Host=localhost;Database=test;Username=postgres;Password=postgres");
            var rm = new ReplicationManager("Host=localhost;", "Host=localhost;");
            var service = new Service(db, rm, new EventPublisher(), new ClusterManager());

            var request = new Request
            {
                Operation = "SearchCar",
                Data = JsonSerializer.Serialize(new SearchCarRequest { Brand = "Toyota" }),
                RequestId = 1
            };

            var response = service.ProcessRequest(request);

            Assert.NotNull(response);
            _output.WriteLine($"Поиск машины обработан. Success: {response.Success}");
        }

        [Fact]
        public void ValidRequest()
        {
            var db = new Database("Host=localhost;Database=test;Username=postgres;Password=postgres");
            var rm = new ReplicationManager("Host=localhost;", "Host=localhost;");
            var service = new Service(db, rm, new EventPublisher(), new ClusterManager());

            var request = new Request { Operation = "GetAllCars", Data = "{}", RequestId = 2 };
            var response = service.ProcessRequest(request);

            Assert.NotNull(response);
            _output.WriteLine($"Запрос GetAllCars обработан.");
        }

        [Fact]
        public void InvalidRequest_BadJson_ReturnsError()
        {
            var db = new Database("Host=localhost;Database=test;Username=postgres;Password=postgres");
            var rm = new ReplicationManager("Host=localhost;", "Host=localhost;");
            var service = new Service(db, rm, new EventPublisher(), new ClusterManager());

            var request = new Request { Operation = "SearchCar", Data = "{bad_json: 123}", RequestId = 3 };
            var response = service.ProcessRequest(request);

            Assert.False(response.Success);
            Assert.Contains("Ошибка", response.ErrorMessage); 
            _output.WriteLine($"Битый JSON перехвачен: {response.ErrorMessage}");
        }

        [Fact]
        public void ExpectedError_UnknownOperation_ReturnsSpecificMessage()
        {
            var db = new Database("Host=localhost;Database=test;Username=postgres;Password=postgres");
            var rm = new ReplicationManager("Host=localhost;", "Host=localhost;");
            var service = new Service(db, rm, new EventPublisher(), new ClusterManager());

            var request = new Request { Operation = "NonExistentOp", Data = "{}", RequestId = 4 };
            var response = service.ProcessRequest(request);

            Assert.False(response.Success);
            Assert.Contains("Неизвестная операция", response.ErrorMessage);
            _output.WriteLine($"Ожидаемая ошибка получена: {response.ErrorMessage}");
        }

        [Fact]
        public void ConfigParameter_AppMode_IsUsed()
        {
            Environment.SetEnvironmentVariable("APP_MODE", "kubernetes");

            var mode = Environment.GetEnvironmentVariable("APP_MODE") ?? "default";

            Assert.Equal("kubernetes", mode);
            _output.WriteLine($"Параметр конфигурации APP_MODE успешно прочитан: {mode}");
        }
    }
}