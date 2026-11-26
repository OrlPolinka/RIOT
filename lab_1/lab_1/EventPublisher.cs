using System;
using System.Threading.Tasks;

namespace lab_1
{
    public class EventPublisher
    {
        private readonly List<EventBroker> _brokers;

        public EventPublisher(params EventBroker[] brokers)
        {
            _brokers = brokers.ToList();
        }

        public async Task PublishAsync(Event evt)
        {

            foreach (var broker in _brokers)
            {
                try
                {
                    Console.WriteLine($"[Publisher] PUBLISH: {evt.EventType} (EntityID: {evt.EntityId})");
                    await broker.PublishAsync(evt);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"[Publisher] ОШИБКА доставки в брокер: {ex.Message}");
                }
                
            }
        }
    }
}