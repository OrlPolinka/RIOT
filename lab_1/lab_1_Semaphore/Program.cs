using System;
using System.Text.Json;
using System.Linq;

namespace lab_1 
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("Выберите режим запуска:");
            Console.WriteLine("1 - Сервер");
            Console.WriteLine("2 - Клиент");
            Console.Write("Ваш выбор: ");

            string choice = Console.ReadLine();

            if (choice == "1")
            {
                RunServer();
            }
            else if (choice == "2")
            {
                RunClient();
            }
            else
            {
                Console.WriteLine("Неверный выбор. Запуск сервера по умолчанию...");
                RunServer();
            }
        }

        static void RunServer()
        {
            var server = new Server(3000);

            // обработка Ctrl + C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                server.Stop();
                Environment.Exit(0);
            };

            server.Start();
        }


        static void RunClient()
        {
            var client = new Client();

            Console.Write("Введите IP-адрес сервера (по умолчанию 127.0.0.1): ");
            string ip = Console.ReadLine();
            if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

            Console.Write("Введите порт сервера (по умолчанию 3000): ");
            string portStr = Console.ReadLine();
            int port = string.IsNullOrEmpty(portStr) ? 3000 : int.Parse(portStr);

            if (!client.Connect(ip, port))
            {
                Console.WriteLine("Не удалось подключиться к серверу. Нажмите любую клавишу для выхода...");
                Console.ReadKey();
                return;
            }

            Console.Write("Введите смещение часов в секундах (например, 7 или -5): ");
            if (int.TryParse(Console.ReadLine(), out int offsetSec))
                client.SetClockOffset(TimeSpan.FromSeconds(offsetSec));

            //var dataStore = new HelpData("DataStore.json");

            bool isRunning = true;
            while (isRunning)
            {
                Console.Clear();
                Console.WriteLine("===== АРЕНДА АВТОМОБИЛЕЙ =====");
                Console.WriteLine("1. Показать все машины");
                Console.WriteLine("2. Поиск машин");
                Console.WriteLine("3. Создать аренду");
                Console.WriteLine("4. Отменить аренду");
                Console.WriteLine("5. Вернуть машину");
                Console.WriteLine("6. Показать все аренды");
                Console.WriteLine("7. Показать всех клиентов");
                Console.WriteLine("8. Отправить тестовый запрос (неизвестная операция)");
                Console.WriteLine("9. Синхронизация времени");
                Console.WriteLine("0. Выход");
                Console.Write("Выберите операцию: ");

                string menuChoice = Console.ReadLine();

                switch (menuChoice)
                {
                    case "1":
                        ShowAllCars(client);
                        break;
                    case "2":
                        SearchCars(client);
                        break;
                    case "3":
                        CreateRental(client);
                        break;
                    case "4":
                        CancelRental(client);
                        break;
                    case "5":
                        ReturnCar(client);
                        break;
                    case "6":
                        ShowAllRentals(client);
                        break;
                    case "7":
                        ShowAllCustomers(client);
                        break;
                    case "8":
                        TestUnknownOperation(client);
                        break;
                    case "9":
                        SyncTime(client);
                        break;
                    case "0":
                        isRunning = false;
                        break;
                    default:
                        Console.WriteLine("Неверный выбор. Нажмите любую клавишу...");
                        Console.ReadKey();
                        break;
                }
            }

            client.Disconnect();
        }

        static void ShowAllCars(Client client)
        {
            Console.Clear();
            Console.WriteLine("===== ВСЕ МАШИНЫ =====");

            var request = client.CreateGetAllCarsRequest();
            var response = client.SendRequest(request);

            if (response.Success)
            {
                var cars = JsonSerializer.Deserialize<List<Car>>(response.Result);
                foreach (var car in cars)
                {
                    Console.WriteLine($"ID: {car.Id}, {car.Brand} {car.Model}, {car.Year} г., {car.Color}, " +
                                      $"Цена: {car.PricePerDay} руб/день, {(car.IsAvailable ? "СВОБОДНА" : "ЗАНЯТА")}");
                }
            }
            else
            {
                Console.WriteLine($"Ошибка: {response.ErrorMessage}");
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }

        static void SearchCars(Client client)
        {
            Console.Clear();
            Console.WriteLine("===== ПОИСК МАШИН =====");

            Console.Write("Марка (или Enter для пропуска): ");
            string brand = Console.ReadLine();

            Console.Write("Модель (или Enter для пропуска): ");
            string model = Console.ReadLine();

            decimal? maxPrice = SafeReadDecimal("Максимальная цена в день (или Enter для пропуска): ");

            var request = client.CreateSearchCarRequest(
                string.IsNullOrEmpty(brand) ? null : brand,
                string.IsNullOrEmpty(model) ? null : model,
                maxPrice
            );

            var response = client.SendRequest(request);

            if (response.Success)
            {
                var cars = JsonSerializer.Deserialize<List<Car>>(response.Result);
                if (cars.Count == 0)
                {
                    Console.WriteLine("Машин по вашему запросу не найдено.");
                }
                else
                {
                    Console.WriteLine($"\nНайдено машин: {cars.Count}");
                    foreach (var car in cars)
                    {
                        Console.WriteLine($"ID: {car.Id}, {car.Brand} {car.Model}, {car.Year} г., {car.Color}, " +
                                          $"Цена: {car.PricePerDay} руб/день");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Ошибка: {response.ErrorMessage}");
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }

        static void CreateRental(Client client)
        {
            Console.Clear();
            Console.WriteLine("===== СОЗДАНИЕ АРЕНДЫ =====");

            var cars = FetchCars(client).Where(c => c.IsAvailable).ToList();
            var customers = FetchCustomers(client);
            var locations = FetchLocations(client);

            if (cars.Count == 0)
            {
                Console.WriteLine("Нет доступных машин!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Доступные машины:");
            foreach (var car in cars)
            {
                Console.WriteLine($"ID: {car.Id}, {car.Brand} {car.Model}, Цена: {car.PricePerDay} руб/день");
            }

            int carId = SafeReadInt("Выберите ID машины: ");

            var selectedCar = cars.FirstOrDefault(c => c.Id == carId);
            if (selectedCar == null)
            {
                Console.WriteLine($"Машина с ID {carId} не найдена или уже занята!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nДоступные клиенты:");
            foreach (var c in customers)
            {
                Console.WriteLine($"ID: {c.Id}, {c.Surname} {c.Name}");
            }

            int customerId = SafeReadInt("ID клиента: ");

            var customer = customers.FirstOrDefault(c => c.Id == customerId);
            if (customer == null)
            {
                Console.WriteLine($"Клиент с ID {customerId} не найден!");
                Console.ReadKey();
                return;
            }

            int days = SafeReadInt("Количество дней аренды: ", 1, 365);

            Console.WriteLine("\nДоступные локации:");
            foreach (var loc in locations)
            {
                Console.WriteLine($"ID: {loc.Id}, {loc.City}, {loc.Address}");
            }

            int pickupId = SafeReadInt("ID места выдачи: ", 1, locations.Count);

            var request = client.CreateRentalRequest(customerId, carId, pickupId, days);
            var response = client.SendRequest(request);

            if (response.Success)
            {
                var rental = JsonSerializer.Deserialize<Rental>(response.Result);
                Console.WriteLine($"\nАренда успешно создана!");
                Console.WriteLine($"ID аренды: {rental.Id}");
                Console.WriteLine($"Общая стоимость: {rental.TotalCost} руб.");
                Console.WriteLine($"Дата окончания: {rental.EndDate}");
            }
            else
            {
                Console.WriteLine($"Ошибка: {response.ErrorMessage}");
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }

        static void CancelRental(Client client)
        {
            Console.Clear();
            Console.WriteLine("===== ОТМЕНА АРЕНДЫ =====");

            var rentals = FetchRentals(client);
            var cars = FetchCars(client);
            var customers = FetchCustomers(client);

            var activeRentals = rentals.Where(r => r.Status == "Active").ToList();

            if (activeRentals.Count == 0)
            {
                Console.WriteLine("Нет активных аренд для отмены.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nАктивные аренды:");
            foreach (var rent in activeRentals)
            {
                var c = cars.FirstOrDefault(x => x.Id == rent.CarId);
                var customer = customers.FirstOrDefault(x => x.Id == rent.CustomerId);
                Console.WriteLine($"ID: {rent.Id}, Клиент: {customer?.Surname} {customer?.Name}, " +
                                 $"Машина: {c?.Brand} {c?.Model}, Стоимость: {rent.TotalCost} руб.");
            }

            int rentalId = SafeReadInt("Введите ID аренды для отмены: ");

            var rental = activeRentals.FirstOrDefault(r => r.Id == rentalId);
            if (rental == null)
            {
                Console.WriteLine($"Активная аренда с ID {rentalId} не найдена!");
                Console.ReadKey();
                return;
            }

            var car = cars.FirstOrDefault(c => c.Id == rental.CarId);
            Console.WriteLine($"\nВы уверены, что хотите отменить аренду машины {car?.Brand} {car?.Model}?");
            Console.Write("(y/n): ");
            string confirm = Console.ReadLine()?.ToLower();

            if (confirm != "y")
            {
                Console.WriteLine("Отмена отменена!");
                Console.ReadKey();
                return;
            }

            var request = client.CreateCancelRentalRequest(rentalId);
            var response = client.SendRequest(request);

            if (response.Success)
            {
                Console.WriteLine("Аренда успешно отменена!");
                Console.WriteLine("Машина снова доступна для аренды.");
            }
            else
            {
                Console.WriteLine($"Ошибка: {response.ErrorMessage}");
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }

        static void ReturnCar(Client client)
        {
            Console.Clear();
            Console.WriteLine("===== ВОЗВРАТ МАШИНЫ =====");

            var rentals = FetchRentals(client);
            var cars = FetchCars(client);
            var customers = FetchCustomers(client);
            var locations = FetchLocations(client);

            var activeRentals = rentals.Where(r => r.Status == "Active").ToList();

            if (activeRentals.Count == 0)
            {
                Console.WriteLine("Нет активных аренд для возврата.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nАктивные аренды:");
            foreach (var rent in activeRentals)
            {
                var car = cars.FirstOrDefault(c => c.Id == rent.CarId);
                var customer = customers.FirstOrDefault(c => c.Id == rent.CustomerId);
                Console.WriteLine($"ID: {rent.Id}, Клиент: {customer?.Surname} {customer?.Name}, " +
                                 $"Машина: {car?.Brand} {car?.Model}, Стоимость: {rent.TotalCost} руб.");
            }

            int rentalId = SafeReadInt("Введите ID аренды для возврата: ");

            var rental = activeRentals.FirstOrDefault(r => r.Id == rentalId);
            if (rental == null)
            {
                Console.WriteLine($"Активная аренда с ID {rentalId} не найдена!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nДоступные локации для возврата:");
            foreach (var loc in locations)
            {
                Console.WriteLine($"ID: {loc.Id}, {loc.City}, {loc.Address} (работает: {loc.WorkingHours})");
            }

            int returnLocationId = SafeReadInt("Выберите ID локации возврата: ", 1, locations.Count);

            var returnLocation = locations.FirstOrDefault(l => l.Id == returnLocationId);
            if (returnLocation == null)
            {
                Console.WriteLine("Локация не найдена!");
                Console.ReadKey();
                return;
            }

            var request = client.CreateReturnCarRequest(rentalId, returnLocationId);
            var response = client.SendRequest(request);

            if (response.Success)
            {
                Console.WriteLine("Машина успешно возвращена!");
                Console.WriteLine($"Место возврата: {returnLocation.City}, {returnLocation.Address}");
            }
            else
            {
                Console.WriteLine($"Ошибка: {response.ErrorMessage}");
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }

        static void ShowAllRentals(Client client)
        {
            Console.Clear();
            Console.WriteLine("===== ВСЕ АРЕНДЫ =====");

            var rentals = FetchRentals(client);
            var cars = FetchCars(client);
            var customers = FetchCustomers(client);
            var locations = FetchLocations(client);

            if (rentals.Count == 0)
            {
                Console.WriteLine("Аренд пока нет.");
            }
            else
            {
                foreach (var rental in rentals)
                {
                    var car = cars.FirstOrDefault(c => c.Id == rental.CarId);
                    var customer = customers.FirstOrDefault(c => c.Id == rental.CustomerId);
                    var pickupLoc = locations.FirstOrDefault(l => l.Id == rental.PickupLocationId);
                    var returnLoc = rental.ReturnLocationId.HasValue
                        ? locations.FirstOrDefault(l => l.Id == rental.ReturnLocationId.Value)
                        : null;

                    Console.WriteLine($"ID: {rental.Id}, Клиент: {customer?.Surname} {customer?.Name}, " +
                                      $"Машина: {car?.Brand} {car?.Model}, Статус: {rental.Status}, " +
                                      $"Стоимость: {rental.TotalCost} руб., " +
                                      $"Выдача: {pickupLoc?.City}, {pickupLoc?.Address}, " +
                                      $"Возврат: {(rental.Status == "Completed" ? $"{returnLoc?.City}, {returnLoc?.Address}" : "не указано (ждем возврата)")}, " +
                                      $"Период: {rental.StartDate:dd.MM.yyyy} - {rental.EndDate:dd.MM.yyyy}");

                    if (rental.ActualReturnDate.HasValue)
                        Console.WriteLine($"  Фактический возврат: {rental.ActualReturnDate:dd.MM.yyyy HH:mm}");
                }
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }

        static void ShowAllCustomers(Client client)
        {
            Console.Clear();
            Console.WriteLine("===== ВСЕ КЛИЕНТЫ =====");

            var request = client.CreateGetAllCustomersRequest();
            var response = client.SendRequest(request);

            if (response.Success)
            {
                var customers = JsonSerializer.Deserialize<List<Customer>>(response.Result);
                foreach (var customer in customers)
                {
                    Console.WriteLine($"ID: {customer.Id}, {customer.Surname} {customer.Name} {customer.Patronymic}, " +
                                      $"Email: {customer.Email}, Телефон: {customer.PhoneNumber}");
                }
            }
            else
            {
                Console.WriteLine($"Ошибка: {response.ErrorMessage}");
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }



        static int SafeReadInt(string prompt, int minValue = int.MinValue, int maxValue = int.MaxValue)
        {
            int result;
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine();

                if (string.IsNullOrEmpty(input))
                {
                    Console.WriteLine("Ввод не может быть пустым. Попробуйте снова.");
                    continue;
                }

                if (!int.TryParse(input, out result))
                {
                    Console.WriteLine("Ошибка: нужно ввести число. Попробуйте снова.");
                    continue;
                }

                if (result < minValue || result > maxValue)
                {
                    Console.WriteLine($"Число должно быть от {minValue} до {maxValue}. Попробуйте снова.");
                    continue;
                }

                return result;
            }
        }

        static decimal? SafeReadDecimal(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine();

                if (string.IsNullOrEmpty(input))
                    return null;

                if (decimal.TryParse(input, out decimal result))
                    return result;

                Console.WriteLine("Ошибка: нужно ввести число. Попробуйте снова.");
            }
        }



        static List<Car> FetchCars(Client client)
        {
            var response = client.SendRequest(client.CreateGetAllCarsRequest());
            if (!response.Success) return new List<Car>();
            return JsonSerializer.Deserialize<List<Car>>(response.Result);
        }

        static List<Customer> FetchCustomers(Client client)
        {
            var response = client.SendRequest(client.CreateGetAllCustomersRequest());
            if (!response.Success) return new List<Customer>();
            return JsonSerializer.Deserialize<List<Customer>>(response.Result);
        }

        static List<Rental> FetchRentals(Client client)
        {
            var response = client.SendRequest(client.CreateGetAllRentalsRequest());
            if (!response.Success) return new List<Rental>();
            return JsonSerializer.Deserialize<List<Rental>>(response.Result);
        }

        static List<Location> FetchLocations(Client client)
        {
            var response = client.SendRequest(client.CreateGetAllLocationsRequest());
            if (!response.Success) return new List<Location>();
            return JsonSerializer.Deserialize<List<Location>>(response.Result);
        }


        static void TestUnknownOperation(Client client)
        {
            var request = new Request
            {
                Operation = "SomeUnknownOp",
                Data = "{}",
                RequestId = 999
            };

            Console.WriteLine($"Операция: {request.Operation}");
            var response = client.SendRequest(request);

            Console.WriteLine($"\nРезультат:");
            Console.WriteLine($"Success: {response.Success}");
            Console.WriteLine($"Ошибка: {response.ErrorMessage}");

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }


        static void SyncTime(Client client)
        {
            Console.Clear();
            Console.WriteLine("СИНХРОНИЗАЦИЯ ВРЕМЕНИ С СЕРВЕРОМ");

            var localBefore = client.GetLocalTime();

            var requestTime = DateTime.UtcNow;
            var response = client.SendRequest(client.CreateTimeRequest());
            var responseTime = DateTime.UtcNow;

            if (!response.Success)
            {
                Console.WriteLine($"Ошибка: {response.ErrorMessage}");
                Console.ReadKey();
                return;
            }

            var doc = JsonDocument.Parse(response.Result);
            var serverTimeBefore = doc.RootElement.GetProperty("ServerTime").GetDateTime();

            Console.WriteLine("ДО СИНХРОНИЗАЦИИ");
            Console.WriteLine($"  Клиент:  {localBefore:HH:mm:ss.fff}");
            Console.WriteLine($"  Сервер:  {serverTimeBefore:HH:mm:ss.fff}");
            Console.WriteLine($"  Разница: {(serverTimeBefore - localBefore).TotalSeconds:+0.000;-0.000;0.000} сек");
            Console.WriteLine();

            var rtt = responseTime - requestTime;
            var networkDelay = rtt / 2;

            Console.WriteLine("ИЗМЕРЕНИЯ");
            Console.WriteLine($"  RTT:     {rtt.TotalMilliseconds:F0} мс");
            Console.WriteLine($"  RTT/2:   {networkDelay.TotalMilliseconds:F0} мс");
            Console.WriteLine();

            var clientLocalAtResponse = client.GetLocalTime();
            var offset = (serverTimeBefore + networkDelay) - clientLocalAtResponse;

            Console.WriteLine("ПОПРАВКА");
            Console.WriteLine($"  Offset:  {offset.TotalSeconds:+0.000;-0.000;0.000} сек");
            Console.WriteLine();

            client.ApplySyncOffset(offset);
            client.SetLastKnownServerTime(serverTimeBefore);

            var localAfter = client.GetLocalTime();
            var serverTimeAfter = client.GetServerTimeEstimate();

            Console.WriteLine("ПОСЛЕ СИНХРОНИЗАЦИИ");
            Console.WriteLine($"  Клиент:  {localAfter:HH:mm:ss.fff}");
            Console.WriteLine($"  Сервер:  {serverTimeAfter:HH:mm:ss.fff}  (оценка)");
            Console.WriteLine($"  Разница: {(serverTimeAfter - localAfter).TotalSeconds:+0.000;-0.000;0.000} сек");
            Console.WriteLine();

            Console.ReadKey();
        }


    }
}