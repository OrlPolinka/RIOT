using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace lab_1.Tests
{
    public class MockSubscriber : ISubscriber
    {
        public string Name { get; set; } = "Mock";
        public bool IsAvailable { get; set; } = true;
        public int HandledCount { get; private set; } = 0;
        public Event LastEvent { get; private set; }

        public async Task HandleEventAsync(Event evt)
        {
            if (!IsAvailable) throw new Exception($"{Name} UNAVAILABLE");

            await Task.Delay(10); 
            HandledCount++;
            LastEvent = evt;
        }
    }

    public class PubSubTests
    {
        private readonly ITestOutputHelper _output;

        public PubSubTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Test1_Event_Creation()
        {
            _output.WriteLine("===== ТЕСТ 1: Создание события =====");

            var evt = new Event
            {
                EventType = "RentalCreated",
                EntityId = 125,
                Timestamp = DateTime.Now,
                Data = "Test Data"
            };

            Assert.NotNull(evt);
            Assert.Equal("RentalCreated", evt.EventType);
            Assert.Equal(125, evt.EntityId);
            Assert.NotNull(evt.Data);
            Assert.True(evt.Timestamp > DateTime.MinValue);

            _output.WriteLine("ТЕСТ 1 ПРОЙДЕН!");
        }

        [Fact]
        public void Test2_Subscribe_RegistersSubscriber()
        {
            _output.WriteLine("===== ТЕСТ 2: Регистрация подписчика =====");

            var broker = new EventBroker();
            var subscriber = new MockSubscriber { Name = "TestSub" };

            broker.Subscribe("RentalCreated", subscriber);

            var field = typeof(EventBroker).GetField("_subscriptions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var subscriptions = field.GetValue(broker) as System.Collections.IDictionary;

            Assert.NotNull(subscriptions);
            Assert.True(subscriptions.Contains("RentalCreated"));

            _output.WriteLine("ТЕСТ 2 ПРОЙДЕН! Подписчик зарегистрирован.");
        }

        [Fact]
        public async Task Test3_SubscribedSubscriber_ReceivesEvent()
        {
            _output.WriteLine("===== ТЕСТ 3: Подписанный подписчик получает событие =====");

            var broker = new EventBroker();
            var subscriber = new MockSubscriber { Name = "Logger" };
            broker.Subscribe("RentalCreated", subscriber);

            var publisher = new EventPublisher(broker);

            var evt = new Event { EventType = "RentalCreated", EntityId = 1 };
            await publisher.PublishAsync(evt);

            Assert.Equal(1, subscriber.HandledCount);
            Assert.Equal("RentalCreated", subscriber.LastEvent.EventType);
            Assert.Equal(1, subscriber.LastEvent.EntityId);

            _output.WriteLine("ТЕСТ 3 ПРОЙДЕН!");
        }

        [Fact]
        public async Task Test4_UnsubscribedSubscriber_DoesNotReceiveEvent()
        {
            _output.WriteLine("===== ТЕСТ 4: Неподписанный подписчик НЕ получает =====");

            var broker = new EventBroker();
            var subscribed = new MockSubscriber { Name = "Logger" };
            var notSubscribed = new MockSubscriber { Name = "CarService" };

            broker.Subscribe("RentalCreated", subscribed);

            var publisher = new EventPublisher(broker);
            await publisher.PublishAsync(new Event { EventType = "RentalCreated", EntityId = 1 });

            Assert.Equal(1, subscribed.HandledCount); 
            Assert.Equal(0, notSubscribed.HandledCount);  

            _output.WriteLine("ТЕСТ 4 ПРОЙДЕН!");
        }

        [Fact]
        public async Task Test5_OneEvent_DeliveredToMultipleSubscribers()
        {
            _output.WriteLine("===== ТЕСТ 5: Одно событие — несколько подписчиков =====");

            var broker = new EventBroker();
            var sub1 = new MockSubscriber { Name = "Logger" };
            var sub2 = new MockSubscriber { Name = "CarService" };
            var sub3 = new MockSubscriber { Name = "Notification" };

            broker.Subscribe("RentalCreated", sub1);
            broker.Subscribe("RentalCreated", sub2);
            broker.Subscribe("RentalCreated", sub3);

            var publisher = new EventPublisher(broker);
            await publisher.PublishAsync(new Event { EventType = "RentalCreated", EntityId = 1 });

            Assert.Equal(1, sub1.HandledCount);
            Assert.Equal(1, sub2.HandledCount);
            Assert.Equal(1, sub3.HandledCount);

            _output.WriteLine("ТЕСТ 5 ПРОЙДЕН! Все 3 подписчика получили событие.");
        }

        [Fact]
        public async Task Test6_MultipleEventTypes()
        {
            _output.WriteLine("===== ТЕСТ 6: Разные типы событий =====");

            var broker = new EventBroker();
            var logger = new MockSubscriber { Name = "Logger" };
            var carService = new MockSubscriber { Name = "CarService" };

            broker.Subscribe("RentalCreated", logger);
            broker.Subscribe("RentalCancelled", logger);
            broker.Subscribe("CarReturned", logger);

            broker.Subscribe("RentalCreated", carService);
            broker.Subscribe("RentalCancelled", carService);

            var publisher = new EventPublisher(broker);

            await publisher.PublishAsync(new Event { EventType = "RentalCreated" });
            await publisher.PublishAsync(new Event { EventType = "RentalCancelled" });
            await publisher.PublishAsync(new Event { EventType = "CarReturned" });

            Assert.Equal(3, logger.HandledCount);
            Assert.Equal(2, carService.HandledCount); 

            _output.WriteLine("ТЕСТ 6 ПРОЙДЕН!");
        }

        [Fact]
        public async Task Test7_SubscriberError_DoesNotCrashBroker()
        {
            _output.WriteLine("===== ТЕСТ 7: Ошибка подписчика =====");

            var broker = new EventBroker();
            var failing = new MockSubscriber { Name = "CarService", IsAvailable = false };
            var working = new MockSubscriber { Name = "Logger", IsAvailable = true };

            broker.Subscribe("RentalCreated", failing);
            broker.Subscribe("RentalCreated", working);

            var publisher = new EventPublisher(broker);

            await publisher.PublishAsync(new Event { EventType = "RentalCreated" });

            Assert.Equal(0, failing.HandledCount); 
            Assert.Equal(1, working.HandledCount);  

            _output.WriteLine("ТЕСТ 7 ПРОЙДЕН! Ошибка изолирована.");
        }

        [Fact]
        public async Task Test8_Broker_ContinuesAfterSubscriberFailure()
        {
            _output.WriteLine("===== ТЕСТ 8: Broker продолжает работу =====");

            var broker = new EventBroker();
            var failing = new MockSubscriber { Name = "CarService", IsAvailable = false };
            var working = new MockSubscriber { Name = "Logger" };

            broker.Subscribe("RentalCreated", failing);
            broker.Subscribe("RentalCreated", working);

            var publisher = new EventPublisher(broker);

            await publisher.PublishAsync(new Event { EventType = "RentalCreated", EntityId = 1 });
            await publisher.PublishAsync(new Event { EventType = "RentalCreated", EntityId = 2 });
            await publisher.PublishAsync(new Event { EventType = "RentalCreated", EntityId = 3 });

            Assert.Equal(3, working.HandledCount);
            Assert.Equal(0, failing.HandledCount);

            _output.WriteLine("ТЕСТ 8 ПРОЙДЕН! Broker не упал.");
        }

        [Fact]
        public async Task Test9_AddNewSubscriber_WithoutChangingPublisher()
        {
            _output.WriteLine("===== ТЕСТ 9: Новый подписчик без изменения Publisher =====");

            var broker = new EventBroker();
            var oldSub = new MockSubscriber { Name = "OldSubscriber" };
            broker.Subscribe("RentalCreated", oldSub);

            var publisher = new EventPublisher(broker);

            await publisher.PublishAsync(new Event { EventType = "RentalCreated" });
            Assert.Equal(1, oldSub.HandledCount);

            var newSub = new MockSubscriber { Name = "NewSubscriber" };
            broker.Subscribe("RentalCreated", newSub);

            await publisher.PublishAsync(new Event { EventType = "RentalCreated" });

            Assert.Equal(2, oldSub.HandledCount);
            Assert.Equal(1, newSub.HandledCount);

            _output.WriteLine("ТЕСТ 9 ПРОЙДЕН! Publisher не менялся.");
        }

        [Fact]
        public async Task Test10_EventWithoutSubscribers_DoesNotCrash()
        {
            _output.WriteLine("===== ТЕСТ 10: Событие без подписчиков =====");

            var broker = new EventBroker();
            var publisher = new EventPublisher(broker);

            var evt = new Event { EventType = "UnknownEvent", EntityId = 1 };

            await publisher.PublishAsync(evt);

            _output.WriteLine("ТЕСТ 10 ПРОЙДЕН! Broker не упал.");
        }

    }

    public class SlowSubscriber : ISubscriber
    {
        public string Name { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int DelayMs { get; set; } = 1000;
        public int HandledCount { get; private set; } = 0;

        public async Task HandleEventAsync(Event evt)
        {
            if (!IsAvailable) throw new Exception($"{Name} UNAVAILABLE");
            await Task.Delay(DelayMs);
            HandledCount++;
        }
    }
}