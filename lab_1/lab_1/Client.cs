using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace lab_1
{
    public class Client
    {

        private TimeSpan _clockOffset = TimeSpan.Zero;      
        private TimeSpan _serverTimeOffset = TimeSpan.Zero;  

        public TimeSpan ClockOffset => _clockOffset;
        public TimeSpan ServerTimeOffset => _serverTimeOffset;

        private DateTime _lastKnownServerTime = DateTime.MinValue;
        private DateTime _lastKnownServerTimeLocal = DateTime.MinValue;


        public DateTime GetServerTimeEstimate()
        {
            if (_lastKnownServerTime == DateTime.MinValue) return DateTime.UtcNow;
            var elapsed = DateTime.UtcNow - _lastKnownServerTimeLocal;
            return _lastKnownServerTime + elapsed;
        }

        public void SetLastKnownServerTime(DateTime serverTime)
        {
            _lastKnownServerTime = serverTime;
            _lastKnownServerTimeLocal = DateTime.UtcNow;
        }

        public void SetClockOffset(TimeSpan offset)
        {
            _clockOffset = offset;
            Console.WriteLine($"[Клиент] Смещение часов: {offset.TotalSeconds:+0;-0;0} сек");
        }

        public void SetServerTimeOffset(TimeSpan offset)
        {
            _serverTimeOffset = offset;
        }

        public DateTime GetLocalTime() => DateTime.UtcNow + _clockOffset;
        public DateTime GetCorrectedTime() => GetLocalTime() + _serverTimeOffset;

        public Request CreateTimeRequest()
        {
            return new Request
            {
                Operation = "TimeRequest",
                Data = "{}",
                RequestId = ++_requestCounter
            };
        }

        public void ApplySyncOffset(TimeSpan offset)
        {
            _clockOffset += offset;
            _serverTimeOffset = TimeSpan.Zero;
            Console.WriteLine($"[Клиент] Синхронизация применена.");
        }


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

                byte[] requestLengthBytes = BitConverter.GetBytes(data.Length);
                _stream.Write(requestLengthBytes, 0, 4);
                _stream.Write(data, 0, data.Length);
                Console.WriteLine($"[Клиент] Отправлен запрос: {request.Operation}");

                byte[] responseLengthBuffer = new byte[4];
                ReadExact(_stream, responseLengthBuffer, 4);
                int responseLength = BitConverter.ToInt32(responseLengthBuffer, 0);

                byte[] responseBuffer = new byte[responseLength];
                ReadExact(_stream, responseBuffer, responseLength);
                string responseJson = Encoding.UTF8.GetString(responseBuffer, 0, responseLength);

                Console.WriteLine($"[Клиент] Получен ответ ({responseLength} байт)");

                return JsonSerializer.Deserialize<Response>(responseJson);

                /*_stream.Write(data, 0, data.Length);
                Console.WriteLine($"[Клиент] Отправлен запрос: {request.Operation}");

                byte[] buffer = new byte[4096];
                int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Console.WriteLine($"[Клиент] Получен ответ");

                return JsonSerializer.Deserialize<Response>(responseJson);*/
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

        private static void ReadExact(NetworkStream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = stream.Read(buffer, totalRead, count - totalRead);
                if (bytesRead == 0)
                    throw new Exception("Соединение закрыто сервером");
                totalRead += bytesRead;
            }
        }

        public Request CreateGetAllRentalsRequest()
        {
            return new Request
            {
                Operation = "GetAllRentals",
                Data = "{}",
                RequestId = ++_requestCounter
            };
        }

        public Request CreateGetAllLocationsRequest()
        {
            return new Request
            {
                Operation = "GetAllLocations",
                Data = "{}",
                RequestId = ++_requestCounter
            };
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

        public Request CreateRentalRequest(int customerId, int carId, int pickupLocationId, int days)
        {
            var data = new CreateRentalRequest
            {
                CustomerId = customerId,
                CarId = carId,
                PickupLocationId = pickupLocationId,
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

        public Request CreateReturnCarRequest(int rentalId, int returnLocationId)
        {
            var data = new ReturnCarRequest
            {
                RentalId = rentalId,
                ReturnLocationId = returnLocationId
            };

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