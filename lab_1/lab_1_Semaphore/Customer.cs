using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace lab_1
{
    public class Customer
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Surname")]
        public string Surname { get; set; }

        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Patronymic")]
        public string Patronymic { get; set; }

        [JsonPropertyName("Email")]
        public string Email { get; set; }

        [JsonPropertyName("PhoneNumber")]
        public string PhoneNumber { get; set; }

        [JsonPropertyName("DriverLicenseNumber")]
        public string DriverLicenseNumber { get; set; }  // номер прав

        [JsonPropertyName("DateOfBirth")]
        public DateTime DateOfBirth { get; set; }

        [JsonPropertyName("RegistrationDate")]
        public DateTime RegistrationDate { get; set; }

        public Customer() { }
    }
}
