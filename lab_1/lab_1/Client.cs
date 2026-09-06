using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace lab_1
{
    public class Client
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private int _requestCounter = 0;

        public bool Connect(string serverIp, int port)
        {
            try
            {
                _tcpClient = new TcpClient(serverIp, port);
                _stream = _tcpClient.GetStream();
                Console.WriteLine("[Клиент] Подключен к серверу");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Клиент] Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            _stream?.Close();
            _tcpClient?.Close();
            Console.WriteLine("[Клиент] Отключен от сервера");
        }

        public Response SendRequest(Request request)
        {
            try
            {
                
                string requestJson = JsonSerializer.Serialize(request);
                byte[] data = Encoding.UTF8.GetBytes(requestJson);

                
                _stream.Write(data, 0, data.Length);
                Console.WriteLine($"[Клиент] Отправлен запрос: {request.Operation}");

                byte[] buffer = new byte[4096];
                int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Console.WriteLine($"[Клиент] Получен ответ");

                return JsonSerializer.Deserialize<Response>(responseJson);
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Success = false,
                    ErrorMessage = $"Ошибка сетевого взаимодействия: {ex.Message}"
                };
            }
        }





        public Request CreateSearchCarRequest(string brand = null, string model = null, decimal? maxPrice = null)
        {
            var data = new SearchCarRequest
            {
                Brand = brand,
                Model = model,
                MaxPrice = maxPrice
            };

            return new Request
            {
                Operation = "SearchCar",
                Data = JsonSerializer.Serialize(data),
                RequestId = ++_requestCounter
            };
        }

        public Request CreateRentalRequest(int customerId, int carId, int pickupLocationId, int returnLocationId, int days)
        {
            var data = new CreateRentalRequest
            {
                CustomerId = customerId,
                CarId = carId,
                PickupLocationId = pickupLocationId,
                ReturnLocationId = returnLocationId,
                Days = days
            };

            return new Request
            {
                Operation = "CreateRental",
                Data = JsonSerializer.Serialize(data),
                RequestId = ++_requestCounter
            };
        }

        public Request CreateCancelRentalRequest(int rentalId)
        {
            var data = new CancelRentalRequest { RentalId = rentalId };

            return new Request
            {
                Operation = "CancelRental",
                Data = JsonSerializer.Serialize(data),
                RequestId = ++_requestCounter
            };
        }

        public Request CreateReturnCarRequest(int rentalId)
        {
            var data = new ReturnCarRequest { RentalId = rentalId };

            return new Request
            {
                Operation = "ReturnCar",
                Data = JsonSerializer.Serialize(data),
                RequestId = ++_requestCounter
            };
        }

        public Request CreateGetAllCarsRequest()
        {
            return new Request
            {
                Operation = "GetAllCars",
                Data = "{}",
                RequestId = ++_requestCounter
            };
        }

        public Request CreateGetAllCustomersRequest()
        {
            return new Request
            {
                Operation = "GetAllCustomers",
                Data = "{}",
                RequestId = ++_requestCounter
            };
        }
    }
}