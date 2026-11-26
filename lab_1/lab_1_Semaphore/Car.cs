using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace lab_1
{
    public class Car
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Brand")]
        public string Brand { get; set; }

        [JsonPropertyName("Model")]
        public string Model { get; set; }

        [JsonPropertyName("Year")]
        public int Year { get; set; }

        [JsonPropertyName("LicensePlate")]
        public string LicensePlate { get; set; }

        [JsonPropertyName("PricePerDay")]
        public decimal PricePerDay { get; set; }

        [JsonPropertyName("IsAvailable")]
        public bool IsAvailable { get; set; }

        [JsonPropertyName("Color")]
        public string Color { get; set; }

        public Car() { }
    }
}


