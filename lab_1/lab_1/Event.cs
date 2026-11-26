using System;
using System.Text.Json.Serialization;

namespace lab_1
{
    public class Event
    {
        public string EventType { get; set; }
        public int EntityId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Data { get; set; } 
    }
}