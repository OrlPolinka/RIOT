using System;
using System.Threading;
using System.Threading.Tasks;

namespace lab_1
{
    public class LoggerSubscriber : ISubscriber
    {
        public string Name => "Logger";
        public bool IsAvailable { get; set; } = true;

        public async Task HandleEventAsync(Event evt)
        {
            if (!IsAvailable) throw new Exception("Logger UNAVAILABLE");
            await Task.Delay(1000); 
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Logger] Получено событие: {evt.EventType} (ID: {evt.EntityId})");
        }
    }

    //RentalCreated и RentalCancelled
    public class CarServiceSubscriber : ISubscriber
    {
        public string Name => "CarService";
        public bool IsAvailable { get; set; } = true;  // true false

        private readonly Database _db;

        public CarServiceSubscriber(Database db)
        {
            _db = db;
        }

        public async Task HandleEventAsync(Event evt)
        {
            if (!IsAvailable) throw new Exception("CarService UNAVAILABLE");
            await Task.Delay(3000);

            if (evt.EventType == "RentalCreated")
            {
                _db.UpdateCarAvailability(evt.EntityId, false); 
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [CarService] Машина #{evt.EntityId} ЗАБРОНИРОВАНА (БД обновлена)");
            }
            else if (evt.EventType == "RentalCancelled")
            {
                _db.UpdateCarAvailability(evt.EntityId, true); 
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [CarService] Машина #{evt.EntityId} ОСВОБОЖДЕНА (БД обновлена)");
            }
        }
        }

    //RentalCreated и CarReturned
    public class NotificationServiceSubscriber : ISubscriber
    {
        public string Name => "Notification";
        public bool IsAvailable { get; set; } = true;

        public async Task HandleEventAsync(Event evt)
        {
            if (!IsAvailable) throw new Exception("Notification UNAVAILABLE");
            await Task.Delay(2000); 
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Notification] Отправлено уведомление клиенту: {evt.EventType} (ID: {evt.EntityId})");
        }
    }
}