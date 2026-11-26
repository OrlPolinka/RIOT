using System;
using System.Text.Json.Serialization;

namespace lab_1
{
    public class Request
    {
        [JsonPropertyName("Operation")]
        public string Operation { get; set; } 

        [JsonPropertyName("Data")]
        public string Data { get; set; }   

        [JsonPropertyName("RequestId")]
        public int RequestId { get; set; } 
    }

    public class Response
    {
        [JsonPropertyName("RequestId")]
        public int RequestId { get; set; }    

        [JsonPropertyName("Success")]
        public bool Success { get; set; } 

        [JsonPropertyName("Result")]
        public string Result { get; set; }    

        [JsonPropertyName("ErrorMessage")]
        public string ErrorMessage { get; set; } 

        [JsonPropertyName("Operation")]
        public string Operation { get; set; }  
    }

    public class SearchCarRequest
    {
        [JsonPropertyName("Brand")]
        public string Brand { get; set; }

        [JsonPropertyName("Model")]
        public string Model { get; set; }

        [JsonPropertyName("MaxPrice")]
        public decimal? MaxPrice { get; set; }  
    }

    public class CreateRentalRequest
    {
        [JsonPropertyName("CustomerId")]
        public int CustomerId { get; set; }

        [JsonPropertyName("CarId")]
        public int CarId { get; set; }

        [JsonPropertyName("PickupLocationId")]
        public int PickupLocationId { get; set; }

        [JsonPropertyName("ReturnLocationId")]
        public int ReturnLocationId { get; set; }

        [JsonPropertyName("Days")]
        public int Days { get; set; }
    }

    public class CancelRentalRequest
    {
        [JsonPropertyName("RentalId")]
        public int RentalId { get; set; }
    }

    public class ReturnCarRequest
    {
        [JsonPropertyName("RentalId")]
        public int RentalId { get; set; }

        [JsonPropertyName("ReturnLocationId")]
        public int ReturnLocationId { get; set; }
    }
}