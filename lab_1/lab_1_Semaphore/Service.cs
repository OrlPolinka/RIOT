using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace lab_1
{
    public class Service
    {
        private HelpData _dataStore;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public Service(HelpData dataStore)
        {
            _dataStore = dataStore;
        }

        private void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            Console.ForegroundColor = oldColor;
        }

        public async Task<Response> ProcessRequest(Request request)
        {
            try
            {

                Console.WriteLine($"[Сервер] Получен запрос: {request.Operation} (ID: {request.RequestId})");


                    switch (request.Operation)
                    {
                        case "SearchCar":
                            return await SearchCar(request);
                        case "CreateRental":
                            return await CreateRental(request);
                        case "CancelRental":
                            return await CancelRental(request);
                        case "ReturnCar":
                            return await ReturnCar(request);
                        case "GetAllCars":
                            return await GetAllCars(request);
                        case "GetAllCustomers":
                            return await GetAllCustomers(request);
                        case "GetAllRentals":
                            return await GetAllRentals(request);
                        case "GetAllLocations":
                            return await GetAllLocations(request);
                        case "TimeRequest":
                            return await TimeRequest(request);
                    default:
                            return new Response
                            {
                                RequestId = request.RequestId,
                                Success = false,
                                Operation = request.Operation,
                                ErrorMessage = $"Неизвестная операция: {request.Operation}"
                            };
                    }
              
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = request.Operation,
                    ErrorMessage = $"Ошибка на сервере: {ex.Message}"
                };
            }
        }

        private async Task<Response> SearchCar(Request request)
        {
            try
            {
                var searchData = JsonSerializer.Deserialize<SearchCarRequest>(request.Data);
                var availableCars = _dataStore.Cars.Where(c => c.IsAvailable);

                if (!string.IsNullOrEmpty(searchData.Brand))
                    availableCars = availableCars.Where(c =>
                        c.Brand.ToLower().Contains(searchData.Brand.ToLower()));

                if (!string.IsNullOrEmpty(searchData.Model))
                    availableCars = availableCars.Where(c =>
                        c.Model.ToLower().Contains(searchData.Model.ToLower()));

                if (searchData.MaxPrice.HasValue)
                    availableCars = availableCars.Where(c =>
                        c.PricePerDay <= searchData.MaxPrice.Value);

                var result = availableCars.ToList();

                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "SearchCar",
                    Result = JsonSerializer.Serialize(result)
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "SearchCar",
                    ErrorMessage = $"Ошибка при поиске машин: {ex.Message}"
                };
            }
        }

        private async Task<Response> CreateRental(Request request)
        {
            try
            {
                var rentalData = JsonSerializer.Deserialize<CreateRentalRequest>(request.Data);

                var customer = _dataStore.Customers.FirstOrDefault(c => c.Id == rentalData.CustomerId);
                if (customer == null)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "CreateRental",
                        ErrorMessage = $"Клиент с ID {rentalData.CustomerId} не найден"
                    };

                Log($"[SEMAPHORE] Поток {Thread.CurrentThread.ManagedThreadId}: ожидание семафора...", ConsoleColor.DarkYellow);

                await _semaphore.WaitAsync();
                try
                {
                    Log($"[SEMAPHORE] Поток {Thread.CurrentThread.ManagedThreadId}: ВОШЁЛ", ConsoleColor.Yellow);

                    var car = _dataStore.Cars.FirstOrDefault(c => c.Id == rentalData.CarId);
                    if (car == null)
                    {
                        Log($"[SEMAPHORE] Поток {Thread.CurrentThread.ManagedThreadId}: машина не найдена", ConsoleColor.Red);
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ из критической секции", ConsoleColor.Yellow); 
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CreateRental",
                            ErrorMessage = $"Машина с ID {rentalData.CarId} не найдена"
                        };
                    }

                    Log($"[CHECK] Поток {Thread.CurrentThread.ManagedThreadId}: IsAvailable = {car.IsAvailable}", ConsoleColor.Cyan);

                    if (!car.IsAvailable)
                    {
                        Log($"[FAIL] Поток {Thread.CurrentThread.ManagedThreadId}: машина уже занята", ConsoleColor.Red);
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ из критической секции", ConsoleColor.Yellow);
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CreateRental",
                            ErrorMessage = $"Машина {car.Brand} {car.Model} уже арендована"
                        };
                    }

                    Console.WriteLine($"[ГОНКА] Поток {Thread.CurrentThread.ManagedThreadId}: ждём 3 секунды...");
                    await Task.Delay(3000);
                    Console.WriteLine($"[ГОНКА] Поток {Thread.CurrentThread.ManagedThreadId}: продолжаем...");

                    var pickupLocation = _dataStore.Locations.FirstOrDefault(l => l.Id == rentalData.PickupLocationId);
                    if (pickupLocation == null)
                    {
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ (нет локации)", ConsoleColor.Yellow);
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CreateRental",
                            ErrorMessage = $"Локация выдачи с ID {rentalData.PickupLocationId} не найдена"
                        };
                    }



                    if (rentalData.Days <= 0)
                    {
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ (дни <= 0)", ConsoleColor.Yellow);
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CreateRental",
                            ErrorMessage = "Количество дней должно быть больше 0"
                        };
                    }

                    var rental = new Rental
                    {
                        Id = _dataStore.GetNextRentalId(),
                        CustomerId = rentalData.CustomerId,
                        CarId = rentalData.CarId,
                        PickupLocationId = rentalData.PickupLocationId,
                        ReturnLocationId = null,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(rentalData.Days),
                        ActualReturnDate = null,
                        TotalCost = car.PricePerDay * rentalData.Days,
                        Status = "Active"
                    };

                    _dataStore.Rentals.Add(rental);

                    bool oldValue = car.IsAvailable;   
                    car.IsAvailable = false;
                    Log($"[UPDATE] Поток {Thread.CurrentThread.ManagedThreadId}: IsAvailable: {oldValue} → false, аренда #{rental.Id}", ConsoleColor.Green);

                    var payment = new Payment
                    {
                        Id = _dataStore.GetNextPaymentId(),
                        RentalId = rental.Id,
                        TotalSummer = rental.TotalCost,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = "Card",
                        Status = "Paid"
                    };
                    _dataStore.Payments.Add(payment);


                    _dataStore.SaveData();

                    Log($"[SUCCESS] Поток {Thread.CurrentThread.ManagedThreadId}: аренда #{rental.Id} создана", ConsoleColor.Green);
                    Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ из критической секции", ConsoleColor.Yellow);

                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = true,
                        Operation = "CreateRental",
                        Result = JsonSerializer.Serialize(rental)
                    };
                }
                finally
                {
                    _semaphore.Release();
                    Log($"[SEMAPHORE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "CreateRental",
                    ErrorMessage = $"Ошибка при создании аренды: {ex.Message}"
                };
            }
        }

        private async Task<Response> TimeRequest(Request request)
        {
            try
            {
                var serverTime = DateTime.UtcNow;
                Log($"[TIME] Сервер отдаёт время: {serverTime:HH:mm:ss.fff}", ConsoleColor.Cyan);

                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "TimeRequest",
                    Result = JsonSerializer.Serialize(new { ServerTime = serverTime })
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "TimeRequest",
                    ErrorMessage = $"Ошибка при получении времени: {ex.Message}"
                };
            }
        }

        private async Task<Response> CancelRental(Request request)
        {
            try
            {
                var cancelData = JsonSerializer.Deserialize<CancelRentalRequest>(request.Data);

                await _semaphore.WaitAsync();
                try
                {
                    Log($"[SEMAPHORE] Поток {Thread.CurrentThread.ManagedThreadId}: ВОШЁЛ (CancelRental)", ConsoleColor.Yellow);

                    var rental = _dataStore.Rentals.FirstOrDefault(r => r.Id == cancelData.RentalId);
                    if (rental == null)
                    {
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CancelRental",
                            ErrorMessage = $"Аренда с ID {cancelData.RentalId} не найдена"
                        };
                    }

                    if (rental.Status == "Cancelled")
                    {

                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CancelRental",
                            ErrorMessage = "Аренда уже отменена"
                        };
                    }

                    if (rental.Status == "Completed")
                    {
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CancelRental",
                            ErrorMessage = "Нельзя отменить завершенную аренду"
                        };
                    }

                rental.Status = "Cancelled";
                rental.ActualReturnDate = DateTime.Now;

                var car = _dataStore.Cars.FirstOrDefault(c => c.Id == rental.CarId);
                if (car != null)
                    car.IsAvailable = true;

              
                _dataStore.SaveData();

                    Log($"[SUCCESS] Поток {Thread.CurrentThread.ManagedThreadId}: аренда #{rental.Id} отменена", ConsoleColor.Green);
                    Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                    return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "CancelRental",
                    Result = JsonSerializer.Serialize(new
                    {
                        message = $"Аренда #{rental.Id} отменена",
                        rentalId = rental.Id,
                        status = rental.Status
                    })
                };
            }
                finally
                {
                    _semaphore.Release();
                    Log($"[SEMAPHORE] Поток {Thread.CurrentThread.ManagedThreadId}: СЕМАФОР ОСВОБОЖДЁН", ConsoleColor.DarkYellow);
                }
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "CancelRental",
                    ErrorMessage = $"Ошибка при отмене аренды: {ex.Message}"
                };
            }
        }

        private async Task<Response> ReturnCar(Request request)
        {
            try
            {
                var returnData = JsonSerializer.Deserialize<ReturnCarRequest>(request.Data);

                await _semaphore.WaitAsync();
                try
                {
                    Log($"[SEMAPHORE] Поток {Thread.CurrentThread.ManagedThreadId}: ВОШЁЛ (ReturnCar)", ConsoleColor.Yellow);

                    var rental = _dataStore.Rentals.FirstOrDefault(r => r.Id == returnData.RentalId);
                    if (rental == null)
                    {
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "ReturnCar",
                            ErrorMessage = $"Аренда с ID {returnData.RentalId} не найдена"
                        };
                    }

                    if (rental.Status == "Completed")
                    {
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "ReturnCar",
                            ErrorMessage = "Машина уже возвращена"
                        };
                    }

                    if (rental.Status == "Cancelled")
                    {
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "ReturnCar",
                            ErrorMessage = "Нельзя вернуть отмененную аренду"
                        };
                    }

                    var returnLocation = _dataStore.Locations.FirstOrDefault(l => l.Id == returnData.ReturnLocationId);
                    if (returnLocation == null)
                    {
                        Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "ReturnCar",
                            ErrorMessage = $"Локация возврата с ID {returnData.ReturnLocationId} не найдена"
                        };
                    }

                    rental.ActualReturnDate = DateTime.Now;
                    rental.ReturnLocationId = returnData.ReturnLocationId;
                    rental.Status = "Completed";

                    var car = _dataStore.Cars.FirstOrDefault(c => c.Id == rental.CarId);
                    if (car != null)
                        car.IsAvailable = true;

                    _dataStore.SaveData();

                    Log($"[SUCCESS] Поток {Thread.CurrentThread.ManagedThreadId}: машина возвращена", ConsoleColor.Green);
                    Log($"[RELEASE] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = true,
                        Operation = "ReturnCar",
                        Result = JsonSerializer.Serialize(new
                        {
                            message = $"Машина {car.Brand} {car.Model} возвращена",
                            rentalId = rental.Id,
                            returnDate = rental.ActualReturnDate,
                            returnLocation = $"{returnLocation.City}, {returnLocation.Address}"
                        })
                    };
                }
                finally
                {
                    _semaphore.Release();
                    Log($"[SEMAPHORE] Поток {Thread.CurrentThread.ManagedThreadId}: СЕМАФОР ОСВОБОЖДЁН", ConsoleColor.DarkYellow);
                }
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "ReturnCar",
                    ErrorMessage = $"Ошибка при возврате машины: {ex.Message}"
                };
            }
        }

        private async Task<Response> GetAllCars(Request request)
        {
            try
            {
                var allCars = _dataStore.Cars.ToList();
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "GetAllCars",
                    Result = JsonSerializer.Serialize(allCars)
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "GetAllCars",
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<Response> GetAllCustomers(Request request)
        {
            try
            {
                var allCustomers = _dataStore.Customers.ToList();
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "GetAllCustomers",
                    Result = JsonSerializer.Serialize(allCustomers)
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "GetAllCustomers",
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<Response> GetAllRentals(Request request)
        {
            try
            {
                var allRentals = _dataStore.Rentals.ToList();
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "GetAllRentals",
                    Result = JsonSerializer.Serialize(allRentals)
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "GetAllRentals",
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<Response> GetAllLocations(Request request)
        {
            try
            {
                var allLocations = _dataStore.Locations.ToList();
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "GetAllLocations",
                    Result = JsonSerializer.Serialize(allLocations)
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "GetAllLocations",
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}