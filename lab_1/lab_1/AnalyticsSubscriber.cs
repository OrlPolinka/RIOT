using System;
using System.Threading.Tasks;

namespace lab_1
{
    public class AnalyticsSubscriber : ISubscriber
    {
        public string Name => "Analytics";
        public bool IsAvailable { get; set; } = true;

        public int CreatedCount { get; private set; } = 0;
        public int CancelledCount { get; private set; } = 0;
        public int ReturnedCount { get; private set; } = 0;

        public async Task HandleEventAsync(Event evt)
        {
            if (!IsAvailable) throw new Exception("Analytics UNAVAILABLE");
            await Task.Delay(500);

            switch (evt.EventType)
            {
                case "RentalCreated": CreatedCount++; break;
                case "RentalCancelled": CancelledCount++; break;
                case "CarReturned": ReturnedCount++; break;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Analytics] Статистика: Создано={CreatedCount}, Отменено={CancelledCount}, Возвращено={ReturnedCount}");
        }
    }
}