using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using lab_1;

namespace lab_1.Tests
{
    public class Lab8DockerTests
    {
        private readonly ITestOutputHelper _output;

        public Lab8DockerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void UnknownOperation_ReturnsMeaningfulError()
        {
            var db = new Database("Host=localhost;Database=test;Username=postgres;Password=postgres");
            var rm = new ReplicationManager("Host=localhost;", "Host=localhost;");
            var publisher = new EventPublisher();
            var cluster = new ClusterManager();

            var service = new Service(db, rm, publisher, cluster);

            var request = new Request { Operation = "InvalidOperation", Data = "{}", RequestId = 1 };
            var response = service.ProcessRequest(request);

            Assert.False(response.Success);
            Assert.Contains("Неизвестная операция", response.ErrorMessage);

            _output.WriteLine($"Сервер корректно вернул ошибку: {response.ErrorMessage}");
        }

        [Fact]
        public void InvalidJsonData_Or_NoDb_ReturnsError()
        {
            var db = new Database("Host=localhost;Database=test;Username=postgres;Password=postgres");
            var rm = new ReplicationManager("Host=localhost;", "Host=localhost;");
            var service = new Service(db, rm, new EventPublisher(), new ClusterManager());

            var request = new Request { Operation = "SearchCar", Data = "{bad_json: 123}", RequestId = 2 };
            var response = service.ProcessRequest(request);

            Assert.False(response.Success);
            Assert.NotNull(response.ErrorMessage);
            Assert.Contains("Ошибка", response.ErrorMessage); 

            _output.WriteLine($"Сервер корректно вернул ошибку (не упал): {response.ErrorMessage}");
        }

        [Fact]
        public void AppConfig_ReadsEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("SERVER_PORT", "8080");

            Assert.Equal(8080, AppConfig.ServerPort);

            _output.WriteLine("AppConfig корректно читает SERVER_PORT из окружения.");
        }
    }
}