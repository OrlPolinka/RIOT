using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace lab_1
{
    public class Rental
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("CustomerId")]
        public int CustomerId { get; set; }

        [JsonPropertyName("CarId")]
        public int CarId { get; set; }

        [JsonPropertyName("PickupLocationId")]
        public int PickupLocationId { get; set; }

        [JsonPropertyName("ReturnLocationId")]
        public int ReturnLocationId { get; set; }

        [JsonPropertyName("StartDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("EndDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("ActualReturnDate")]
        public DateTime? ActualReturnDate { get; set; }

        [JsonPropertyName("TotalCost")]
        public decimal TotalCost { get; set; }

        [JsonPropertyName("Status")]
        public string Status { get; set; }

        public Rental() { }
    }
}
