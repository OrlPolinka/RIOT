using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text.Json;

namespace lab_1
{
    public class Service
    {
        private ReplicationManager _replicationManager;
        private EventPublisher _publisher;
        private ClusterManager _cluster;

        private Database _db;
        public Service(Database db, ReplicationManager rm, EventPublisher publisher, ClusterManager cluster)
        {
            _db = db;
            _replicationManager = rm;
            _publisher = publisher;
            _cluster = cluster;
        }
        private readonly object _lock = new object();

        

        private void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            Console.ForegroundColor = oldColor;
        }

        public Response ProcessRequest(Request request)
        {
            try
            {

                Console.WriteLine($"[Сервер] Получен запрос: {request.Operation} (ID: {request.RequestId})");


                    switch (request.Operation)
                    {
                        case "SearchCar":
                            return SearchCar(request);
                        case "CreateRental":
                            return CreateRental(request);
                        case "CancelRental":
                            return CancelRental(request);
                        case "ReturnCar":
                            return ReturnCar(request);
                        case "GetAllCars":
                            return GetAllCars(request);
                        case "GetAllCustomers":
                            return GetAllCustomers(request);
                        case "GetAllRentals":
                            return GetAllRentals(request);
                        case "GetAllLocations":
                            return GetAllLocations(request);
                        case "TimeRequest":
                            return TimeRequest(request);
                        case "SimulatePrimaryFailure":
                            return SimulatePrimaryFailure(request);
                        case "RestorePrimary":
                            return RestorePrimary(request);
                        case "CheckInstances":
                            return CheckInstances(request);
                        case "Synchronize":
                            return Synchronize(request);
                        case "LeaderElection":
                            return LeaderElection();
                        case "ProposeStatus":
                            return ProposeStatus(request);
                        case "Heartbeat":
                            return Heartbeat();
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


        private Response LeaderElection()
        {
            _cluster.ElectLeader();
            return new Response { Success = true, Operation = "LeaderElection", Result = "OK" };
        }

        private Response ProposeStatus(Request request)
        {
            ProposeStatusRequest data;

            try
            {
                data = JsonSerializer.Deserialize<ProposeStatusRequest>(request.Data);
            }
            catch (JsonException ex)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "ProposeStatus",
                    ErrorMessage = $"Некорректный JSON: {ex.Message}"
                };
            }

            if (data == null || string.IsNullOrWhiteSpace(data.Status) || data.RentalId <= 0)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "ProposeStatus",
                    ErrorMessage = "Нужны поля Status и RentalId"
                };
            }
            bool achieved = _cluster.ProposeValue(data.Status, out int yes, out int majority);

            if (!achieved)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "ProposeStatus",
                    ErrorMessage = $"CONSENSUS NOT ACHIEVED ({yes}/{majority})",
                    Result = JsonSerializer.Serialize(new { Yes = yes, Majority = majority, Consensus = false })
                };
            }

            var db = GetActiveDb();
            var rental = db.GetAllRentals().FirstOrDefault(r => r.Id == data.RentalId);
            if (rental == null)
            {
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "ProposeStatus",
                    ErrorMessage = $"Аренда #{data.RentalId} не найдена (консенсус был, но менять нечего)"
                };
            }
            db.UpdateRentalStatus(data.RentalId, data.Status, null, null);


            return new Response
            {
                RequestId = request.RequestId,
                Success = true,
                Operation = "ProposeStatus",
                Result = JsonSerializer.Serialize(new
                {
                    Yes = yes,
                    Majority = majority,
                    Consensus = true,
                    RentalId = data.RentalId,
                    NewStatus = data.Status
                })
            };
        }

        private Response Heartbeat()
        {
            bool isAlive = _cluster.CheckLeaderHeartbeat();
            return new Response { Success = isAlive, Operation = "Heartbeat" };
        }




        private Database GetActiveDb()
        {
            string connStr = _replicationManager.GetActiveConnectionString();
            return new Database(connStr);
        }


        private Response SimulatePrimaryFailure(Request request)
        {
            _replicationManager.SimulatePrimaryFailure();
            return new Response
            {
                RequestId = request.RequestId,
                Success = true,
                Operation = "SimulatePrimaryFailure",
                Result = JsonSerializer.Serialize(new { Primary = "UNAVAILABLE", Replica = "AVAILABLE" })
            };
        }

        private Response RestorePrimary(Request request)
        {
            _replicationManager.RestorePrimary();
            return new Response
            {
                RequestId = request.RequestId,
                Success = true,
                Operation = "RestorePrimary",
                Result = JsonSerializer.Serialize(new { Primary = "AVAILABLE", Replica = "AVAILABLE" })
            };
        }

        private Response CheckInstances(Request request)
        {
            //_replicationManager.CheckAvailability();
            return new Response
            {
                RequestId = request.RequestId,
                Success = true,
                Operation = "CheckInstances",
                Result = JsonSerializer.Serialize(new
                {
                    Primary = _replicationManager.IsPrimaryAvailable ? "AVAILABLE" : "UNAVAILABLE",
                    Replica = _replicationManager.IsReplicaAvailable ? "AVAILABLE" : "UNAVAILABLE",
                    Current = _replicationManager.CurrentInstance
                })
            };
        }

        private Response Synchronize(Request request)
        {
            var (success, message) = _replicationManager.Synchronize();
            return new Response
            {
                RequestId = request.RequestId,
                Success = success,
                Operation = "Synchronize",
                ErrorMessage = success ? null : message,
                Result = success ? JsonSerializer.Serialize(new { Status = "Synchronized" }) : null
            };
        }

        private Response SearchCar(Request request)
        {
            var db = GetActiveDb();

            try
            {
                var searchData = JsonSerializer.Deserialize<SearchCarRequest>(request.Data);
                var availableCars = db.GetAllCars().Where(c => c.IsAvailable);

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

        private Response CreateRental(Request request)
        {
            var db = GetActiveDb();
            try
            {
                var rentalData = JsonSerializer.Deserialize<CreateRentalRequest>(request.Data);

                var customer = db.GetAllCustomers().FirstOrDefault(c => c.Id == rentalData.CustomerId);
                if (customer == null)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "CreateRental",
                        ErrorMessage = $"Клиент с ID {rentalData.CustomerId} не найден"
                    };

                Log($"[LOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ожидание блокировки...", ConsoleColor.DarkYellow);

                lock (_lock)
                {
                    Log($"[LOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВОШЁЛ в критическую секцию", ConsoleColor.Yellow);

                    var car = db.GetAllCars().FirstOrDefault(c => c.Id == rentalData.CarId);
                    if (car == null)
                    {
                        Log($"[LOCK] Поток {Thread.CurrentThread.ManagedThreadId}: машина не найдена", ConsoleColor.Red);
                        Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ из критической секции", ConsoleColor.Yellow);
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
                        Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ из критической секции", ConsoleColor.Yellow);
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CreateRental",
                            ErrorMessage = $"Машина {car.Brand} {car.Model} уже арендована"
                        };
                    }

                    Console.WriteLine($"[ГОНКА] Поток {Thread.CurrentThread.ManagedThreadId}: ждём 3 секунды...");
                    Thread.Sleep(3000);
                    Console.WriteLine($"[ГОНКА] Поток {Thread.CurrentThread.ManagedThreadId}: продолжаем...");

                    var pickupLocation = db.GetAllLocations().FirstOrDefault(l => l.Id == rentalData.PickupLocationId);
                    if (pickupLocation == null)
                    {
                        Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ (нет локации)", ConsoleColor.Yellow);
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
                        Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ (дни <= 0)", ConsoleColor.Yellow);
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
                        //Id = _dataStore.GetNextRentalId(),
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

                    Log($"[TX] Поток {Thread.CurrentThread.ManagedThreadId}: BEGIN TRANSACTION", ConsoleColor.DarkGreen);

                    db.ExecuteInTransaction((conn, tx) =>
                    {
                        int newId = db.InsertRental(conn, tx, rental);
                        rental.Id = newId;

                        if (rentalData.Days == 360) 
                            throw new Exception("Искусственная ошибка для демонстрации ROLLBACK!");

                        Log($"[TX] Поток {Thread.CurrentThread.ManagedThreadId}: INSERT rentals #{newId}", ConsoleColor.Green);

                        bool oldValue = car.IsAvailable;
                        //db.UpdateCarAvailability(conn, tx, car.Id, false);
                        Log($"[TX] Поток {Thread.CurrentThread.ManagedThreadId}: UPDATE cars IsAvailable: {oldValue} → false", ConsoleColor.Green);

                        var payment = new Payment
                        {
                            RentalId = rental.Id,
                            TotalSummer = rental.TotalCost,
                            PaymentDate = DateTime.Now,
                            PaymentMethod = "Card",
                            Status = "Paid"
                        };
                        db.InsertPayment(conn, tx, payment);
                        Log($"[TX] Поток {Thread.CurrentThread.ManagedThreadId}: INSERT payments", ConsoleColor.Green);
                    });

                    _replicationManager.ReplicateCarAvailability(car.Id, false);

                    Log($"[TX] Поток {Thread.CurrentThread.ManagedThreadId}: COMMIT", ConsoleColor.DarkGreen);

                    Log($"[SUCCESS] Поток {Thread.CurrentThread.ManagedThreadId}: аренда #{rental.Id} создана", ConsoleColor.Green);
                    Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ из критической секции", ConsoleColor.Yellow);

                    var evt = new Event
                    {
                        EventType = "RentalCreated",
                        EntityId = rental.CarId,
                        Timestamp = DateTime.Now,
                        Data = $"CarID: {rental.CarId}, CustomerID: {rental.CustomerId}"
                    };
                    _ = _publisher.PublishAsync(evt);

                    var checkCar = db.GetAllCars().FirstOrDefault(c => c.Id == rental.CarId);
                    if (checkCar != null && checkCar.IsAvailable)
                    {
                        db.UpdateCarAvailability(rental.CarId, false);
                    }

                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = true,
                        Operation = "CreateRental",
                        Result = JsonSerializer.Serialize(rental)
                    };
                }
            }
            catch (Exception ex)
            {
                Log($"[TX] ROLLBACK: {ex.Message}", ConsoleColor.Red);
                return new Response
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Operation = "CreateRental",
                    ErrorMessage = $"Ошибка при создании аренды: {ex.Message}"
                };
            }
        }

        private Response TimeRequest(Request request)
        {
            var db = GetActiveDb();
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

        private Response CancelRental(Request request)
        {
            var db = GetActiveDb();
            try
            {
                var cancelData = JsonSerializer.Deserialize<CancelRentalRequest>(request.Data);

                lock (_lock)
                {
                    Log($"[LOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВОШЁЛ (CancelRental)", ConsoleColor.Yellow);

                    var rental = db.GetAllRentals().FirstOrDefault(r => r.Id == cancelData.RentalId);
                    if (rental == null)
                    {
                        Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CancelRental",
                            ErrorMessage = $"Аренда с ID {cancelData.RentalId} не найдена"
                        };
                    }

                    if (rental.Status == "Cancelled")
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CancelRental",
                            ErrorMessage = "Аренда уже отменена"
                        };

                    if (rental.Status == "Completed")
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "CancelRental",
                            ErrorMessage = "Нельзя отменить завершенную аренду"
                        };
                        
                    var car = db.GetAllCars().FirstOrDefault(c => c.Id == rental.CarId);
                    
                    db.ExecuteInTransaction((conn, tx) =>
                    {
                        db.UpdateRentalStatus(conn, tx, rental.Id, "Cancelled", DateTime.Now, null);

                        
                        //if (car != null)
                            //db.UpdateCarAvailability(conn, tx, car.Id, true);
                    });

                    _replicationManager.ReplicateCarAvailability(car.Id, true);


                    Console.WriteLine($"[Событие] RentalCancelled: Аренда #{rental.Id} отменена");

                    Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                    var evt = new Event { EventType = "RentalCancelled", EntityId = rental.CarId, Timestamp = DateTime.Now };
                    _ = _publisher.PublishAsync(evt);

                    var checkCar = db.GetAllCars().FirstOrDefault(c => c.Id == rental.CarId);
                    if (checkCar != null && checkCar.IsAvailable)
                    {
                        db.UpdateCarAvailability(rental.CarId, false);
                    }

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

        private Response ReturnCar(Request request)
        {
            var db = GetActiveDb();
            try
            {
                var returnData = JsonSerializer.Deserialize<ReturnCarRequest>(request.Data);

                lock (_lock)
                {
                    Log($"[LOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВОШЁЛ (CancelRental)", ConsoleColor.Yellow);


                    var rental = db.GetAllRentals().FirstOrDefault(r => r.Id == returnData.RentalId);
                    if (rental == null)
                    {
                        Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "ReturnCar",
                            ErrorMessage = $"Аренда с ID {returnData.RentalId} не найдена"
                        };
                    }

                    if (rental.Status == "Completed")
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "ReturnCar",
                            ErrorMessage = "Машина уже возвращена"
                        };

                    if (rental.Status == "Cancelled")
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "ReturnCar",
                            ErrorMessage = "Нельзя вернуть отмененную аренду"
                        };

                    var returnLocation = db.GetAllLocations().FirstOrDefault(l => l.Id == returnData.ReturnLocationId);
                    if (returnLocation == null)
                        return new Response
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Operation = "ReturnCar",
                            ErrorMessage = $"Локация возврата с ID {returnData.ReturnLocationId} не найдена"
                        };
                    
                    var car = db.GetAllCars().FirstOrDefault(c => c.Id == rental.CarId);

                    db.ExecuteInTransaction((conn, tx) =>
                    {
                        db.UpdateRentalStatus(conn, tx, rental.Id, "Completed", DateTime.Now, returnData.ReturnLocationId);


                        if (car != null)
                            db.UpdateCarAvailability(conn, tx, car.Id, true);
                    });

                    _replicationManager.ReplicateCarAvailability(car.Id, true);

                    //Console.WriteLine($"[Событие] CarReturned: Машина {car.Brand} {car.Model} возвращена в {returnLocation.City}, {returnLocation.Address}");

                    Log($"[UNLOCK] Поток {Thread.CurrentThread.ManagedThreadId}: ВЫШЕЛ", ConsoleColor.Yellow);

                    var evt = new Event { EventType = "CarReturned", EntityId = rental.CarId, Timestamp = DateTime.Now };
                    _ = _publisher.PublishAsync(evt);

                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = true,
                        Operation = "ReturnCar",
                        Result = JsonSerializer.Serialize(new
                        {
                            message = $"Машина возвращена",
                            rentalId = rental.Id,
                            returnDate = rental.ActualReturnDate,
                            returnLocation = $"{returnLocation.City}, {returnLocation.Address}"
                        })
                    };
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

        private Response GetAllCars(Request request)
        {
            var db = GetActiveDb();
            try
            {
                var allCars = db.GetAllCars();
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

        private Response GetAllCustomers(Request request)
        {
            var db = GetActiveDb();
            try
            {
                var allCustomers = db.GetAllCustomers();
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

        private Response GetAllRentals(Request request)
        {
            var db = GetActiveDb();
            try
            {
                var allRentals = db.GetAllRentals();
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

        private Response GetAllLocations(Request request)
        {
            var db = GetActiveDb();
            try
            {
                var allLocations = db.GetAllLocations();
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