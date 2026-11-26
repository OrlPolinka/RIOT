//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text.Json;
//using System.Threading.Tasks;
//using Xunit;
//using Xunit.Abstractions;

//namespace lab_1.Tests
//{
//    public class TransactionTests : IDisposable
//    {
//        private readonly ITestOutputHelper _output;
//        private readonly Database _db;
//        private readonly Service _service;
//        private readonly int _testCarId = 1;
//        private readonly int _testCustomerId = 1;
//        private readonly int _testLocationId = 1;

//        private const string ConnectionString =
//            "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";

//        public TransactionTests(ITestOutputHelper output)
//        {
//            _output = output;
//            _db = new Database(ConnectionString);
//            _service = new Service(_db);

//            Cleanup();
//        }

//        [Fact]
//        public void Test1_OneClient_RentalSucceeds()
//        {
//            _output.WriteLine("===== ТЕСТ 1: Один клиент — успешная аренда =====");

//            var carBefore = _db.GetAllCars().First(c => c.Id == _testCarId);
//            _output.WriteLine($"ДО: IsAvailable = {carBefore.IsAvailable}");
//            Assert.True(carBefore.IsAvailable, "Машина должна быть свободна до теста");

//            int rentalsBefore = _db.CountRentalsByCar(_testCarId);

//            var request = new Request
//            {
//                Operation = "CreateRental",
//                RequestId = 1,
//                Data = JsonSerializer.Serialize(new CreateRentalRequest
//                {
//                    CustomerId = _testCustomerId,
//                    CarId = _testCarId,
//                    PickupLocationId = _testLocationId,
//                    Days = 3
//                })
//            };

//            var response = _service.ProcessRequest(request);

//            _output.WriteLine($"Response.Success: {response.Success}");
//            _output.WriteLine($"Response.ErrorMessage: {response.ErrorMessage ?? "нет"}");

//            Assert.True(response.Success, "Аренда должна быть успешной");

//            var carAfter = _db.GetAllCars().First(c => c.Id == _testCarId);
//            _output.WriteLine($"ПОСЛЕ: IsAvailable = {carAfter.IsAvailable}");

//            Assert.False(carAfter.IsAvailable, "Машина должна быть занята после аренды");
//            Assert.Equal(rentalsBefore + 1, _db.CountRentalsByCar(_testCarId));

//            _output.WriteLine("ТЕСТ 1 ПРОЙДЕН!");
//        }

//        [Fact]
//        public async Task Test2_TwoClients_SameCar_OnlyOneSucceeds()
//        {
//            _output.WriteLine("===== ТЕСТ 2: Два клиента, одна машина (lock) =====");

//            var car = _db.GetAllCars().First(c => c.Id == _testCarId);
//            _output.WriteLine($"Машина: {car.Brand} {car.Model}, IsAvailable = {car.IsAvailable}");
//            Assert.True(car.IsAvailable);

//            var request1 = new Request
//            {
//                Operation = "CreateRental",
//                RequestId = 2,
//                Data = JsonSerializer.Serialize(new CreateRentalRequest
//                {
//                    CustomerId = 1,
//                    CarId = _testCarId,
//                    PickupLocationId = 1,
//                    Days = 2
//                })
//            };

//            var request2 = new Request
//            {
//                Operation = "CreateRental",
//                RequestId = 3,
//                Data = JsonSerializer.Serialize(new CreateRentalRequest
//                {
//                    CustomerId = 2,
//                    CarId = _testCarId,
//                    PickupLocationId = 1,
//                    Days = 2
//                })
//            };

//            var task1 = Task.Run(() => _service.ProcessRequest(request1));
//            var task2 = Task.Run(() => _service.ProcessRequest(request2));

//            var results = await Task.WhenAll(task1, task2);

//            int successCount = results.Count(r => r.Success);
//            int failCount = results.Count(r => !r.Success);

//            _output.WriteLine($"Успешных: {successCount}");
//            _output.WriteLine($"Отклонённых: {failCount}");

//            for (int i = 0; i < results.Length; i++)
//                _output.WriteLine($"Client {i + 1}: Success={results[i].Success}, Error={results[i].ErrorMessage ?? "нет"}");

//            Assert.Equal(1, successCount);
//            Assert.Equal(1, failCount);

//            var failed = results.First(r => !r.Success);
//            Assert.Contains("уже арендована", failed.ErrorMessage);

//            var carAfter = _db.GetAllCars().First(c => c.Id == _testCarId);
//            Assert.False(carAfter.IsAvailable);

//            int rentalsAfter = _db.CountRentalsByCar(_testCarId);
//            Assert.Equal(1, rentalsAfter);

//            _output.WriteLine("ТЕСТ 2 ПРОЙДЕН!");
//        }

//        [Fact]
//        public void Test3_CompositeOperation_CommitOnSuccess()
//        {
//            _output.WriteLine("===== ТЕСТ 3: COMMIT при успехе =====");

//            int rentalsBefore = _db.CountRentalsByCar(_testCarId);
//            var carBefore = _db.GetAllCars().First(c => c.Id == _testCarId);

//            _output.WriteLine($"ДО:");
//            _output.WriteLine($"  Аренд: {rentalsBefore}");
//            _output.WriteLine($"  IsAvailable: {carBefore.IsAvailable}");

//            var request = new Request
//            {
//                Operation = "CreateRental",
//                RequestId = 4,
//                Data = JsonSerializer.Serialize(new CreateRentalRequest
//                {
//                    CustomerId = _testCustomerId,
//                    CarId = _testCarId,
//                    PickupLocationId = _testLocationId,
//                    Days = 3
//                })
//            };

//            var response = _service.ProcessRequest(request);

//            _output.WriteLine($"Response.Success: {response.Success}");

//            Assert.True(response.Success);

//            int rentalsAfter = _db.CountRentalsByCar(_testCarId);
//            var carAfter = _db.GetAllCars().First(c => c.Id == _testCarId);

//            _output.WriteLine($"ПОСЛЕ:");
//            _output.WriteLine($"  Аренд: {rentalsAfter}");
//            _output.WriteLine($"  IsAvailable: {carAfter.IsAvailable}");

//            Assert.Equal(rentalsBefore + 1, rentalsAfter);
//            Assert.False(carAfter.IsAvailable);

//            _output.WriteLine("ТЕСТ 3 ПРОЙДЕН! COMMIT сохранил ОБЕ операции.");
//        }

//        [Fact]
//        public void Test4_CompositeOperation_RollbackOnError()
//        {
//            _output.WriteLine("===== ТЕСТ 4: ROLLBACK при ошибке =====");

//            int rentalsBefore = _db.CountRentalsByCar(_testCarId);
//            var carBefore = _db.GetAllCars().First(c => c.Id == _testCarId);

//            _output.WriteLine($"ДО:");
//            _output.WriteLine($"  Аренд: {rentalsBefore}");
//            _output.WriteLine($"  IsAvailable: {carBefore.IsAvailable}");

//            var request = new Request
//            {
//                Operation = "CreateRental",
//                RequestId = 5,
//                Data = JsonSerializer.Serialize(new CreateRentalRequest
//                {
//                    CustomerId = _testCustomerId,
//                    CarId = _testCarId,
//                    PickupLocationId = _testLocationId,
//                    Days = 360
//                })
//            };

//            var response = _service.ProcessRequest(request);

//            _output.WriteLine($"Response.Success: {response.Success}");
//            _output.WriteLine($"Response.ErrorMessage: {response.ErrorMessage ?? "нет"}");

//            Assert.False(response.Success);

//            int rentalsAfter = _db.CountRentalsByCar(_testCarId);
//            var carAfter = _db.GetAllCars().First(c => c.Id == _testCarId);

//            _output.WriteLine($"ПОСЛЕ:");
//            _output.WriteLine($"  Аренд: {rentalsAfter}");
//            _output.WriteLine($"  IsAvailable: {carAfter.IsAvailable}");

//            Assert.Equal(rentalsBefore, rentalsAfter); 
//            Assert.True(carAfter.IsAvailable);  

//            _output.WriteLine("ТЕСТ 4 ПРОЙДЕН! ROLLBACK откатил ВСЕ изменения.");
//        }


//        private void Cleanup()
//        {
//            _db.DeleteRentalsByCar(_testCarId);
//            _db.SetCarAvailability(_testCarId, true);

//        }

//        public void Dispose()
//        {
//            Cleanup();
//        }
//    }
//}