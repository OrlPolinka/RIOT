using System;
using System.Linq;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace lab_1.Tests
{
    /// <summary>
    /// Тесты для ЛР №5:
    /// - Репликация между Primary и Replica
    /// - Failover при отказе Primary
    /// - Recovery и синхронизация после восстановления
    /// </summary>
    public class ReplicationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Database _primaryDb;
        private readonly Database _replicaDb;
        private readonly ReplicationManager _rm;

        private const string PrimaryConn =
            "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";
        private const string ReplicaConn =
            "Host=localhost;Port=5432;Database=car_rental_replica;Username=postgres;Password=postgres";

        public ReplicationTests(ITestOutputHelper output)
        {
            _output = output;
            _primaryDb = new Database(PrimaryConn);
            _replicaDb = new Database(ReplicaConn);
            _rm = new ReplicationManager(PrimaryConn, ReplicaConn);
        }

        [Fact]
        public void Test1_PrimaryAndReplica_InitiallyMatch()
        {
            _output.WriteLine("===== ТЕСТ 1: Начальное совпадение =====");

            var (success, message) = _rm.Synchronize();
            Assert.True(success, $"Синхронизация должна пройти: {message}");

            var primaryCars = _primaryDb.GetAllCars();
            var replicaCars = _replicaDb.GetAllCars();

            _output.WriteLine($"Primary cars: {primaryCars.Count}");
            _output.WriteLine($"Replica cars: {replicaCars.Count}");

            Assert.Equal(primaryCars.Count, replicaCars.Count);

            //for (int i = 0; i < primaryCars.Count; i++)
            //{
            //    Assert.Equal(primaryCars[i].Id, replicaCars[i].Id);
            //    Assert.Equal(primaryCars[i].Brand, replicaCars[i].Brand);
            //    Assert.Equal(primaryCars[i].IsAvailable, replicaCars[i].IsAvailable);
            //}

            _output.WriteLine("ТЕСТ 1 ПРОЙДЕН!");
        }

        [Fact]
        public void Test2_ReplicateCarAvailability()
        {
            _output.WriteLine("===== ТЕСТ 2: Репликация is_available =====");

            int carId = 12;

            _primaryDb.SetCarAvailability(carId, true);
            _replicaDb.SetCarAvailability(carId, true);

            _primaryDb.UpdateCarAvailability(carId, false);

            _rm.ReplicateCarAvailability(carId, false);

            var primaryCar = _primaryDb.GetAllCars().First(c => c.Id == carId);
            var replicaCar = _replicaDb.GetAllCars().First(c => c.Id == carId);

            _output.WriteLine($"Primary car #{carId}: IsAvailable = {primaryCar.IsAvailable}");
            _output.WriteLine($"Replica car #{carId}: IsAvailable = {replicaCar.IsAvailable}");

            Assert.Equal(primaryCar.IsAvailable, replicaCar.IsAvailable);
            Assert.False(primaryCar.IsAvailable);

            _output.WriteLine("ТЕСТ 2 ПРОЙДЕН!");
        }

        [Fact]
        public void Test3_CheckInstances_BothAvailable()
        {
            _output.WriteLine("===== ТЕСТ 3: Оба экземпляра доступны =====");

            _rm.CheckAvailability();

            _output.WriteLine($"Primary: {_rm.IsPrimaryAvailable}");
            _output.WriteLine($"Replica: {_rm.IsReplicaAvailable}");
            _output.WriteLine($"Current: {_rm.CurrentInstance}");

            Assert.True(_rm.IsPrimaryAvailable);
            Assert.True(_rm.IsReplicaAvailable);
            Assert.Equal("Primary", _rm.CurrentInstance);

            _output.WriteLine("ТЕСТ 3 ПРОЙДЕН!");
        }

        [Fact]
        public void Test4_Failover_ReturnsPrimaryOrReplica()
        {
            _output.WriteLine("===== ТЕСТ 4: Failover =====");

            string activeConn = _rm.GetActiveConnectionString();
            Assert.NotNull(activeConn);

            _output.WriteLine($"Активный экземпляр: {_rm.CurrentInstance}");
            _output.WriteLine($"Строка подключения: {activeConn}");

            if (_rm.IsPrimaryAvailable)
            {
                Assert.Equal("Primary", _rm.CurrentInstance);
                Assert.Contains("car_rental;", activeConn);
            }
            else
            {
                Assert.Equal("Replica", _rm.CurrentInstance);
                Assert.Contains("car_rental_replica", activeConn);
            }

            _output.WriteLine("ТЕСТ 4 ПРОЙДЕН!");
        }

        [Fact]
        public void Test5_Synchronization_MakesBothEqual()
        {
            _output.WriteLine("===== ТЕСТ 5: Синхронизация =====");

            int carId = 13;
            _replicaDb.SetCarAvailability(carId, false);

            var primaryBefore = _primaryDb.GetAllCars().First(c => c.Id == carId);
            var replicaBefore = _replicaDb.GetAllCars().First(c => c.Id == carId);

            _output.WriteLine($"ДО синхронизации:");
            _output.WriteLine($"  Primary #{carId}: {primaryBefore.IsAvailable}");
            _output.WriteLine($"  Replica #{carId}: {replicaBefore.IsAvailable}");

            var (success, message) = _rm.Synchronize();
            Assert.True(success, message);

            var primaryAfter = _primaryDb.GetAllCars().First(c => c.Id == carId);
            var replicaAfter = _replicaDb.GetAllCars().First(c => c.Id == carId);

            _output.WriteLine($"ПОСЛЕ синхронизации:");
            _output.WriteLine($"  Primary #{carId}: {primaryAfter.IsAvailable}");
            _output.WriteLine($"  Replica #{carId}: {replicaAfter.IsAvailable}");

            Assert.Equal(primaryAfter.IsAvailable, replicaAfter.IsAvailable);

            _output.WriteLine("ТЕСТ 5 ПРОЙДЕН!");
        }

        [Fact]
        public void Test6_Synchronization_CopiesAllTables()
        {
            _output.WriteLine("===== ТЕСТ 6: Синхронизация всех таблиц =====");

            var (success, message) = _rm.Synchronize();
            Assert.True(success, message);

            Assert.Equal(_primaryDb.GetAllCustomers().Count, _replicaDb.GetAllCustomers().Count);
            Assert.Equal(_primaryDb.GetAllCars().Count, _replicaDb.GetAllCars().Count);
            Assert.Equal(_primaryDb.GetAllLocations().Count, _replicaDb.GetAllLocations().Count);
            Assert.Equal(_primaryDb.GetAllRentals().Count, _replicaDb.GetAllRentals().Count);
            Assert.Equal(_primaryDb.GetAllPayments().Count, _replicaDb.GetAllPayments().Count);

            _output.WriteLine($"Customers: {_primaryDb.GetAllCustomers().Count} = {_replicaDb.GetAllCustomers().Count}");
            _output.WriteLine($"Cars: {_primaryDb.GetAllCars().Count} = {_replicaDb.GetAllCars().Count}");
            _output.WriteLine($"Locations: {_primaryDb.GetAllLocations().Count} = {_replicaDb.GetAllLocations().Count}");
            _output.WriteLine($"Rentals: {_primaryDb.GetAllRentals().Count} = {_replicaDb.GetAllRentals().Count}");
            _output.WriteLine($"Payments: {_primaryDb.GetAllPayments().Count} = {_replicaDb.GetAllPayments().Count}");

            _output.WriteLine("ТЕСТ 6 ПРОЙДЕН!");
        }

        [Fact]
        public void Test7_WriteThroughReplica_AfterFailover()
        {
            _output.WriteLine("===== ТЕСТ 7: Запись через Replica =====");

            int carId = 14;
            _replicaDb.UpdateCarAvailability(carId, false);

            var replicaCar = _replicaDb.GetAllCars().First(c => c.Id == carId);
            Assert.False(replicaCar.IsAvailable);

            _output.WriteLine($"Машина #{carId} в Replica: IsAvailable = {replicaCar.IsAvailable}");

            var (success, message) = _rm.Synchronize();
            Assert.True(success, message);

            var primaryCar = _primaryDb.GetAllCars().First(c => c.Id == carId);
            Assert.False(primaryCar.IsAvailable);

            _output.WriteLine($"Машина #{carId} в Primary после синхронизации: IsAvailable = {primaryCar.IsAvailable}");
            _output.WriteLine("ТЕСТ 7 ПРОЙДЕН!");
        }

        public void Dispose()
        {
            for (int i = 1; i <= 5; i++)
            {
                _primaryDb.SetCarAvailability(i, i != 3);
                _replicaDb.SetCarAvailability(i, i != 3);
            }
        }
    }
}