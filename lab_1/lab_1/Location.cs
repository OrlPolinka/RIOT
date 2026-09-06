using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace lab_1
{
    public class Location
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("City")]
        public string City { get; set; }

        [JsonPropertyName("Address")]
        public string Address { get; set; }

        [JsonPropertyName("Phone")]
        public string Phone { get; set; }

        [JsonPropertyName("WorkingHours")]
        public string WorkingHours { get; set; }

        public Location() { }
    }
}