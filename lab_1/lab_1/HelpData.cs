using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace lab_1
{
    public class HelpData
    {
        private readonly string _filePath;
        private int _nextCustomerId = 1;
        private int _nextCarId = 1;
        private int _nextLocationId = 1;
        private int _nextRentalId = 1;
        private int _nextPaymentId = 1;

        public List<Customer> Customers { get; set; }
        public List<Car> Cars { get; set; }
        public List<Location> Locations { get; set; }
        public List<Rental> Rentals { get; set; }
        public List<Payment> Payments { get; set; }

        public HelpData(string filePath = "DataStore.json")
        {
            _filePath = filePath;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<DataContainer>(json);

                    Customers = data?.Customers ?? new List<Customer>();
                    Cars = data?.Cars ?? new List<Car>();
                    Locations = data?.Locations ?? new List<Location>();
                    Rentals = data?.Rentals ?? new List<Rental>();
                    Payments = data?.Payments ?? new List<Payment>();

                    
                    if (Customers.Any())
                        _nextCustomerId = Customers.Max(c => c.Id) + 1;
                    if (Cars.Any())
                        _nextCarId = Cars.Max(c => c.Id) + 1;
                    if (Locations.Any())
                        _nextLocationId = Locations.Max(l => l.Id) + 1;
                    if (Rentals.Any())
                        _nextRentalId = Rentals.Max(r => r.Id) + 1;
                    if (Payments.Any())
                        _nextPaymentId = Payments.Max(p => p.Id) + 1;

                    Console.WriteLine($"[DataStore] Загружено: {Customers.Count} клиентов, {Cars.Count} машин, " +
                                      $"{Locations.Count} локаций, {Rentals.Count} аренд");
                }
                else
                {
                    Console.WriteLine($"[DataStore] Файл {_filePath} не найден. Создаем пустые коллекции.");
                    Customers = new List<Customer>();
                    Cars = new List<Car>();
                    Locations = new List<Location>();
                    Rentals = new List<Rental>();
                    Payments = new List<Payment>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DataStore] Ошибка загрузки данных: {ex.Message}");
                Customers = new List<Customer>();
                Cars = new List<Car>();
                Locations = new List<Location>();
                Rentals = new List<Rental>();
                Payments = new List<Payment>();
            }
        }

        public void SaveData()
        {
            try
            {
                var data = new DataContainer
                {
                    Customers = Customers,
                    Cars = Cars,
                    Locations = Locations,
                    Rentals = Rentals,
                    Payments = Payments
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_filePath, json);
                Console.WriteLine("[DataStore] Данные сохранены");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DataStore] Ошибка сохранения данных: {ex.Message}");
            }
        }

        public int GetNextCustomerId() => _nextCustomerId++;
        public int GetNextCarId() => _nextCarId++;
        public int GetNextLocationId() => _nextLocationId++;
        public int GetNextRentalId() => _nextRentalId++;
        public int GetNextPaymentId() => _nextPaymentId++;

        private class DataContainer
        {
            public List<Customer> Customers { get; set; }
            public List<Car> Cars { get; set; }
            public List<Location> Locations { get; set; }
            public List<Rental> Rentals { get; set; }
            public List<Payment> Payments { get; set; }
        }
    }
}
