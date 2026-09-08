using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace lab_1.Tests
{
    public class UnitTest1 : IDisposable
    {
        private readonly HelpData _dataStore;
        private readonly Service _service;
        private readonly string _testFilePath;

        public UnitTest1()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            _testFilePath = $"TestData_{Guid.NewGuid()}.json";

            _dataStore = new HelpData(_testFilePath);
            InitializeTestData();
            _service = new Service(_dataStore);
        }

        private void InitializeTestData()
        {
            _dataStore.Customers.Add(new Customer
            {
                Id = 1,
                Surname = "Тестов",
                Name = "Клиент",
                Patronymic = "Тестович",
                Email = "test@test.com",
                PhoneNumber = "+375291234567",
                DriverLicenseNumber = "AB1234567",
                DateOfBirth = new DateTime(1990, 1, 1),
                RegistrationDate = DateTime.Now
            });

            _dataStore.Cars.Add(new Car
            {
                Id = 1,
                Brand = "Toyota",
                Model = "Camry",
                Year = 2020,
                LicensePlate = "А123ВС",
                PricePerDay = 45.00m,
                IsAvailable = true,
                Color = "Серебристый"
            });

            _dataStore.Cars.Add(new Car
            {
                Id = 2,
                Brand = "BMW",
                Model = "X5",
                Year = 2021,
                LicensePlate = "В456ХК",
                PricePerDay = 75.00m,
                IsAvailable = true,
                Color = "Черный"
            });

            _dataStore.Locations.Add(new Location
            {
                Id = 1,
                City = "Минск",
                Address = "ул. Тестовая, 1",
                Phone = "+375291111111",
                WorkingHours = "09:00-21:00"
            });

            _dataStore.Locations.Add(new Location
            {
                Id = 2,
                City = "Минск",
                Address = "ул. Тестовая, 2",
                Phone = "+375292222222",
                WorkingHours = "08:00-20:00"
            });

            _dataStore.SaveData();
        }

        [Fact]
        public void Test1_SuccessScenario_FullRentalCycle()
        {
            Console.WriteLine("\n===== ТЕСТ 1: Успешный сценарий =====");

            var searchRequest = new Request
            {
                Operation = "SearchCar",
                RequestId = 1,
                Data = JsonSerializer.Serialize(new SearchCarRequest
                {
                    Brand = "Toyota",
                    Model = "Camry",
                    MaxPrice = 50.00m
                })
            };

            var searchResponse = _service.ProcessRequest(searchRequest);

            Assert.True(searchResponse.Success, "Поиск машин должен быть успешным");
            Assert.Equal("SearchCar", searchResponse.Operation);

            var cars = JsonSerializer.Deserialize<List<Car>>(searchResponse.Result);
            Assert.NotNull(cars);
            Assert.Single(cars);
            Assert.Equal("Toyota", cars[0].Brand);
            Assert.Equal("Camry", cars[0].Model);

            Console.WriteLine($"Найдена машина: {cars[0].Brand} {cars[0].Model}");

            var rentalRequest = new Request
            {
                Operation = "CreateRental",
                RequestId = 2,
                Data = JsonSerializer.Serialize(new CreateRentalRequest
                {
                    CustomerId = 1,
                    CarId = 1,
                    PickupLocationId = 1,
                    Days = 3
                })
            };

            var rentalResponse = _service.ProcessRequest(rentalRequest);

            Assert.True(rentalResponse.Success, "Создание аренды должно быть успешным");
            Assert.Equal("CreateRental", rentalResponse.Operation);

            var rental = JsonSerializer.Deserialize<Rental>(rentalResponse.Result);
            Assert.NotNull(rental);
            Assert.Equal(1, rental.CustomerId);
            Assert.Equal(1, rental.CarId);
            Assert.Equal("Active", rental.Status);
            Assert.Equal(135.00m, rental.TotalCost); 

            Console.WriteLine($"✓ Аренда создана: ID={rental.Id}, Стоимость={rental.TotalCost} руб.");

            var car = _dataStore.Cars.First(c => c.Id == 1);
            Assert.False(car.IsAvailable, "Машина должна быть недоступна после аренды");

            var returnRequest = new Request
            {
                Operation = "ReturnCar",
                RequestId = 3,
                Data = JsonSerializer.Serialize(new ReturnCarRequest
                {
                    RentalId = rental.Id,
                    ReturnLocationId = 2
                })
            };

            var returnResponse = _service.ProcessRequest(returnRequest);

            Assert.True(returnResponse.Success, "Возврат машины должен быть успешным");
            Assert.Equal("ReturnCar", returnResponse.Operation);

            Console.WriteLine($"Машина возвращена");

            var updatedRental = _dataStore.Rentals.First(r => r.Id == rental.Id);
            Assert.Equal("Completed", updatedRental.Status);
            Assert.NotNull(updatedRental.ActualReturnDate);
            Assert.Equal(2, updatedRental.ReturnLocationId);

            var updatedCar = _dataStore.Cars.First(c => c.Id == 1);
            Assert.True(updatedCar.IsAvailable, "Машина должна быть доступна после возврата");

            Console.WriteLine("ТЕСТ 1 ПРОЙДЕН УСПЕШНО!");
        }

        [Fact]
        public void Test2_UnknownOperation_ReturnsError()
        {
            Console.WriteLine("\n===== ТЕСТ 2: Неизвестная операция =====");

            var request = new Request
            {
                Operation = "SomeUnknownOp",
                RequestId = 999,
                Data = "{}"
            };

            var response = _service.ProcessRequest(request);

            Assert.False(response.Success, "Операция должна завершиться с ошибкой");
            Assert.Equal("SomeUnknownOp", response.Operation);
            Assert.Contains("Неизвестная операция", response.ErrorMessage);

            Console.WriteLine($"Получена ошибка: {response.ErrorMessage}");
            Console.WriteLine("ТЕСТ 2 ПРОЙДЕН УСПЕШНО!");
        }

        [Fact]
        public void Test3_InvalidData_ReturnsError()
        {
            Console.WriteLine("\n===== ТЕСТ 3: Некорректные данные =====");

            var invalidCustomerRequest = new Request
            {
                Operation = "CreateRental",
                RequestId = 1,
                Data = JsonSerializer.Serialize(new CreateRentalRequest
                {
                    CustomerId = 999, 
                    CarId = 1,
                    PickupLocationId = 1,
                    Days = 3
                })
            };

            var response1 = _service.ProcessRequest(invalidCustomerRequest);
            Assert.False(response1.Success, "Должна быть ошибка при несуществующем клиенте");
            Assert.Contains("Клиент с ID 999 не найден", response1.ErrorMessage);
            Console.WriteLine($"Ошибка при несуществующем клиенте: {response1.ErrorMessage}");

            var invalidCarRequest = new Request
            {
                Operation = "CreateRental",
                RequestId = 2,
                Data = JsonSerializer.Serialize(new CreateRentalRequest
                {
                    CustomerId = 1,
                    CarId = 999, 
                    PickupLocationId = 1,
                    Days = 3
                })
            };

            var response2 = _service.ProcessRequest(invalidCarRequest);
            Assert.False(response2.Success, "Должна быть ошибка при несуществующей машине");
            Assert.Contains("Машина с ID 999 не найдена", response2.ErrorMessage);
            Console.WriteLine($"Ошибка при несуществующей машине: {response2.ErrorMessage}");

            var invalidDaysRequest = new Request
            {
                Operation = "CreateRental",
                RequestId = 3,
                Data = JsonSerializer.Serialize(new CreateRentalRequest
                {
                    CustomerId = 1,
                    CarId = 1,
                    PickupLocationId = 1,
                    Days = 0
                })
            };

            var response3 = _service.ProcessRequest(invalidDaysRequest);
            Assert.False(response3.Success, "Должна быть ошибка при 0 днях");
            Assert.Contains("Количество дней должно быть больше 0", response3.ErrorMessage);
            Console.WriteLine($"Ошибка при 0 днях: {response3.ErrorMessage}");

            var invalidCancelRequest = new Request
            {
                Operation = "CancelRental",
                RequestId = 4,
                Data = JsonSerializer.Serialize(new CancelRentalRequest
                {
                    RentalId = 999
                })
            };

            var response4 = _service.ProcessRequest(invalidCancelRequest);
            Assert.False(response4.Success, "Должна быть ошибка при отмене несуществующей аренды");
            Assert.Contains("Аренда с ID 999 не найдена", response4.ErrorMessage);
            Console.WriteLine($"Ошибка при отмене несуществующей аренды: {response4.ErrorMessage}");

            Console.WriteLine("ТЕСТ 3 ПРОЙДЕН УСПЕШНО!");
        }

        [Fact]
        public void Test4_ServerUnavailable_ClientHandlesError()
        {
            Console.WriteLine("\n===== ТЕСТ 4: Недоступный сервер =====");

            var client = new Client();

            bool connected = client.Connect("127.0.0.1", 9999);

            Assert.False(connected, "Подключение к недоступному серверу должно завершиться ошибкой");

            Console.WriteLine("Клиент корректно обработал недоступность сервера");

            var request = new Request
            {
                Operation = "SearchCar",
                RequestId = 1,
                Data = JsonSerializer.Serialize(new SearchCarRequest())
            };

            var response = client.SendRequest(request);

            Assert.False(response.Success, "Отправка запроса без подключения должна вернуть ошибку");
            Assert.Contains("Ошибка сетевого взаимодействия", response.ErrorMessage);

            Console.WriteLine($"Получена ошибка: {response.ErrorMessage}");
            Console.WriteLine("ТЕСТ 4 ПРОЙДЕН УСПЕШНО!");
        }

        [Fact]
        public void Test4_ServerUnavailable_WithConnectionTimeout()
        {
            Console.WriteLine("\n===== ТЕСТ 4.1: Таймаут подключения =====");

            var client = new Client();

            bool connected = client.Connect("192.168.255.255", 3000);

            Assert.False(connected, "Подключение к несуществующему IP должно завершиться ошибкой");

            Console.WriteLine("Клиент корректно обработал таймаут подключения");
        }

        public void Dispose()
        {
            if (System.IO.File.Exists(_testFilePath))
            {
                System.IO.File.Delete(_testFilePath);
            }
        }
    }
}