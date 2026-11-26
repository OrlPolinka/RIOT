using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace lab_1
{
    public class EventBroker
    {
        public bool IsAvailable { get; set; } = true;
        private readonly Dictionary<string, List<ISubscriber>> _subscriptions = new();

        private readonly Queue<(Event evt, ISubscriber sub)> _failedEvents = new();
        private readonly int _maxRetries = 3;

        public void Subscribe(string eventType, ISubscriber subscriber)
        {
            if (!_subscriptions.ContainsKey(eventType))
                _subscriptions[eventType] = new List<ISubscriber>();

            _subscriptions[eventType].Add(subscriber);
            Console.WriteLine($"[Broker] {subscriber.Name} подписан на {eventType}");
        }

        public async Task PublishAsync(Event evt)
        {
            if (!IsAvailable) 
                throw new Exception("Broker UNAVAILABLE");

            Console.WriteLine($"[Broker] EVENT RECEIVED: {evt.EventType}");

            if (!_subscriptions.ContainsKey(evt.EventType))
            {
                Console.WriteLine($"[Broker] Нет подписчиков на {evt.EventType}");
                return;
            }

            var subscribers = _subscriptions[evt.EventType];

            var tasks = subscribers.Select(s => DeliverWithRetry(s, evt));
            await Task.WhenAll(tasks);

            await ProcessQueueAsync();
        }

        private async Task DeliverWithRetry(ISubscriber subscriber, Event evt)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    await subscriber.HandleEventAsync(evt);
                    return; 
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Broker] Попытка {attempt} доставки {subscriber.Name} FAIL: {ex.Message}");

                    if (attempt == _maxRetries)
                    {
                        Console.WriteLine($"[Broker] Событие {evt.EventType} для {subscriber.Name} отправлено в очередь");
                        _failedEvents.Enqueue((evt, subscriber));
                    }
                    else
                    {
                        await Task.Delay(1000); 
                    }
                }
            }
        }

        private async Task ProcessQueueAsync()
        {
            if (_failedEvents.Count == 0) return;

            Console.WriteLine($"[Broker] Обработка очереди (событий: {_failedEvents.Count})");

            var count = _failedEvents.Count;
            for (int i = 0; i < count; i++)
            {
                var (evt, sub) = _failedEvents.Dequeue();
                try
                {
                    await sub.HandleEventAsync(evt);
                    Console.WriteLine($"[Broker] Очередное событие {evt.EventType} доставлено {sub.Name} (из очереди)");
                }
                catch
                {
                    _failedEvents.Enqueue((evt, sub));
                }
            }
        }

    }
}