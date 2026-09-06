using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace lab_1
{
    public class Payment
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("RentalId")]
        public int RentalId { get; set; }

        [JsonPropertyName("TotalSummer")]
        public decimal TotalSummer { get; set; }

        [JsonPropertyName("PaymentDate")]
        public DateTime PaymentDate { get; set; }

        [JsonPropertyName("PaymentMethod")]
        public string PaymentMethod { get; set; }

        [JsonPropertyName("Status")]
        public string Status { get; set; }

        public Payment() { }
    }
}