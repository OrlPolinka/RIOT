using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace lab_1
{
    public class Service
    {
        private HelpData _dataStore;

        public Service(HelpData dataStore)
        {
            _dataStore = dataStore;
        }

        public Response ProcessRequest(Request request)
        {
            try
            {
                //if (request == null || string.IsNullOrEmpty(request.Operation))
                //{
                //    return new Response
                //    {
                //        Success = false,
                //        ErrorMessage = "Некорректный запрос: не указана операция"
                //    };
                //}

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

        private Response SearchCar(Request request)
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

        private Response CreateRental(Request request)
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

                var car = _dataStore.Cars.FirstOrDefault(c => c.Id == rentalData.CarId);
                if (car == null)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "CreateRental",
                        ErrorMessage = $"Машина с ID {rentalData.CarId} не найдена"
                    };

                if (!car.IsAvailable)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "CreateRental",
                        ErrorMessage = $"Машина {car.Brand} {car.Model} уже арендована"
                    };

                var pickupLocation = _dataStore.Locations.FirstOrDefault(l => l.Id == rentalData.PickupLocationId);
                if (pickupLocation == null)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "CreateRental",
                        ErrorMessage = $"Локация выдачи с ID {rentalData.PickupLocationId} не найдена"
                    };

                var returnLocation = _dataStore.Locations.FirstOrDefault(l => l.Id == rentalData.ReturnLocationId);
                if (returnLocation == null)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "CreateRental",
                        ErrorMessage = $"Локация возврата с ID {rentalData.ReturnLocationId} не найдена"
                    };

                if (rentalData.Days <= 0)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "CreateRental",
                        ErrorMessage = "Количество дней должно быть больше 0"
                    };

                var rental = new Rental
                {
                    Id = _dataStore.GetNextRentalId(),
                    CustomerId = rentalData.CustomerId,
                    CarId = rentalData.CarId,
                    PickupLocationId = rentalData.PickupLocationId,
                    ReturnLocationId = rentalData.ReturnLocationId,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(rentalData.Days),
                    ActualReturnDate = null,
                    TotalCost = car.PricePerDay * rentalData.Days,
                    Status = "Active"
                };

                _dataStore.Rentals.Add(rental);
                car.IsAvailable = false;

                
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

                Console.WriteLine($"[Событие] CarReserved: Машина {car.Brand} {car.Model} забронирована");
                Console.WriteLine($"[Событие] RentalCreated: Создана аренда #{rental.Id}");
                Console.WriteLine($"[Событие] PaymentCompleted: Оплата #{payment.Id} на сумму {payment.TotalSummer} руб.");

                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "CreateRental",
                    Result = JsonSerializer.Serialize(rental)
                };
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

        private Response CancelRental(Request request)
        {
            try
            {
                var cancelData = JsonSerializer.Deserialize<CancelRentalRequest>(request.Data);

                var rental = _dataStore.Rentals.FirstOrDefault(r => r.Id == cancelData.RentalId);
                if (rental == null)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "CancelRental",
                        ErrorMessage = $"Аренда с ID {cancelData.RentalId} не найдена"
                    };

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

                rental.Status = "Cancelled";

                var car = _dataStore.Cars.FirstOrDefault(c => c.Id == rental.CarId);
                if (car != null)
                    car.IsAvailable = true;

              
                _dataStore.SaveData();

                Console.WriteLine($"[Событие] RentalCancelled: Аренда #{rental.Id} отменена");

                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "CancelRental",
                    Result = JsonSerializer.Serialize(new { message = $"Аренда #{rental.Id} отменена" })
                };
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
            try
            {
                var returnData = JsonSerializer.Deserialize<ReturnCarRequest>(request.Data);

                var rental = _dataStore.Rentals.FirstOrDefault(r => r.Id == returnData.RentalId);
                if (rental == null)
                    return new Response
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Operation = "ReturnCar",
                        ErrorMessage = $"Аренда с ID {returnData.RentalId} не найдена"
                    };

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

                rental.ActualReturnDate = DateTime.Now;
                rental.Status = "Completed";

                var car = _dataStore.Cars.FirstOrDefault(c => c.Id == rental.CarId);
                if (car != null)
                    car.IsAvailable = true;

                _dataStore.SaveData();

                Console.WriteLine($"[Событие] CarReturned: Машина {car.Brand} {car.Model} возвращена");

                return new Response
                {
                    RequestId = request.RequestId,
                    Success = true,
                    Operation = "ReturnCar",
                    Result = JsonSerializer.Serialize(new
                    {
                        message = $"Машина {car.Brand} {car.Model} возвращена",
                        rentalId = rental.Id,
                        returnDate = rental.ActualReturnDate
                    })
                };
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

        private Response GetAllCustomers(Request request)
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
    }
}