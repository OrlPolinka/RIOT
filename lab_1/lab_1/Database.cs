using Npgsql;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace lab_1
{
    public class Database
    {
        private readonly string _connectionString;

        public Database(string connectionString)
        {
            _connectionString = connectionString;
        }

        public NpgsqlConnection GetConnection()
        {
            var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public List<Car> GetAllCars()
        {
            var cars = new List<Car>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("SELECT id, brand, model, year, license_plate, price_per_day, is_available, color FROM cars", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cars.Add(new Car
                {
                    Id = reader.GetInt32(0),
                    Brand = reader.GetString(1),
                    Model = reader.GetString(2),
                    Year = reader.GetInt32(3),
                    LicensePlate = reader.GetString(4),
                    PricePerDay = reader.GetDecimal(5),
                    IsAvailable = reader.GetBoolean(6),
                    Color = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
            return cars;
        }

        public List<Customer> GetAllCustomers()
        {
            var customers = new List<Customer>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("SELECT id, surname, name, patronymic, email, phone_number, driver_license_number, date_of_birth, registration_date FROM customers", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                customers.Add(new Customer
                {
                    Id = reader.GetInt32(0),
                    Surname = reader.GetString(1),
                    Name = reader.GetString(2),
                    Patronymic = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Email = reader.GetString(4),
                    PhoneNumber = reader.GetString(5),
                    DriverLicenseNumber = reader.GetString(6),
                    DateOfBirth = reader.GetDateTime(7),
                    RegistrationDate = reader.GetDateTime(8)
                });
            }
            return customers;
        }

        public List<Rental> GetAllRentals()
        {
            var rentals = new List<Rental>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("SELECT id, customer_id, car_id, pickup_location_id, return_location_id, start_date, end_date, actual_return_date, total_cost, status FROM rentals", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rentals.Add(new Rental
                {
                    Id = reader.GetInt32(0),
                    CustomerId = reader.GetInt32(1),
                    CarId = reader.GetInt32(2),
                    PickupLocationId = reader.GetInt32(3),
                    ReturnLocationId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    StartDate = reader.GetDateTime(5),
                    EndDate = reader.GetDateTime(6),
                    ActualReturnDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    TotalCost = reader.GetDecimal(8),
                    Status = reader.GetString(9)
                });
            }
            return rentals;
        }

        public List<Location> GetAllLocations()
        {
            var locations = new List<Location>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("SELECT id, city, address, phone, working_hours FROM locations", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                locations.Add(new Location
                {
                    Id = reader.GetInt32(0),
                    City = reader.GetString(1),
                    Address = reader.GetString(2),
                    Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
                    WorkingHours = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
            return locations;
        }

        public List<Payment> GetAllPayments()
        {
            var payments = new List<Payment>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("SELECT id, rental_id, total_summer, payment_date, payment_method, status FROM payments", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                payments.Add(new Payment
                {
                    Id = reader.GetInt32(0),
                    RentalId = reader.GetInt32(1),
                    TotalSummer = reader.GetDecimal(2),
                    PaymentDate = reader.GetDateTime(3),
                    PaymentMethod = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Status = reader.GetString(5)
                });
            }
            return payments;
        }

        public int InsertRental(Rental rental)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(
                @"INSERT INTO rentals (customer_id, car_id, pickup_location_id, start_date, end_date, total_cost, status)
          VALUES (@cid, @carid, @locid, @start, @end, @cost, @status) RETURNING id", conn);
            cmd.Parameters.AddWithValue("cid", rental.CustomerId);
            cmd.Parameters.AddWithValue("carid", rental.CarId);
            cmd.Parameters.AddWithValue("locid", rental.PickupLocationId);
            cmd.Parameters.AddWithValue("start", rental.StartDate);
            cmd.Parameters.AddWithValue("end", rental.EndDate);
            cmd.Parameters.AddWithValue("cost", rental.TotalCost);
            cmd.Parameters.AddWithValue("status", rental.Status);
            return (int)cmd.ExecuteScalar();
        }

        public void UpdateCarAvailability(int carId, bool isAvailable)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(
                "UPDATE cars SET is_available = @avail WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("avail", isAvailable);
            cmd.Parameters.AddWithValue("id", carId);
            cmd.ExecuteNonQuery();
        }

        public void InsertPayment(Payment payment)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(
                @"INSERT INTO payments (rental_id, total_summer, payment_date, payment_method, status)
          VALUES (@rid, @sum, @date, @method, @status)", conn);
            cmd.Parameters.AddWithValue("rid", payment.RentalId);
            cmd.Parameters.AddWithValue("sum", payment.TotalSummer);
            cmd.Parameters.AddWithValue("date", payment.PaymentDate);
            cmd.Parameters.AddWithValue("method", payment.PaymentMethod);
            cmd.Parameters.AddWithValue("status", payment.Status);
            cmd.ExecuteNonQuery();
        }

        public void UpdateRentalStatus(int rentalId, string status, DateTime? actualReturnDate, int? returnLocationId)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(
                @"UPDATE rentals SET status = @status, actual_return_date = @date, return_location_id = @locid
          WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("date", (object?)actualReturnDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("locid", (object?)returnLocationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", rentalId);
            cmd.ExecuteNonQuery();
        }




        public void ExecuteInTransaction(Action<NpgsqlConnection, NpgsqlTransaction> action)
        {
            using var conn = GetConnection();
            using var transaction = conn.BeginTransaction();
            try
            {
                action(conn, transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public int InsertRental(NpgsqlConnection conn, NpgsqlTransaction tx, Rental rental)
        {
            using var cmd = new NpgsqlCommand(
                @"INSERT INTO rentals (customer_id, car_id, pickup_location_id, start_date, end_date, total_cost, status)
          VALUES (@cid, @carid, @locid, @start, @end, @cost, @status) RETURNING id", conn);
            cmd.Transaction = tx;
            cmd.Parameters.AddWithValue("cid", rental.CustomerId);
            cmd.Parameters.AddWithValue("carid", rental.CarId);
            cmd.Parameters.AddWithValue("locid", rental.PickupLocationId);
            cmd.Parameters.AddWithValue("start", rental.StartDate);
            cmd.Parameters.AddWithValue("end", rental.EndDate);
            cmd.Parameters.AddWithValue("cost", rental.TotalCost);
            cmd.Parameters.AddWithValue("status", rental.Status);
            return (int)cmd.ExecuteScalar();
        }

        public void UpdateCarAvailability(NpgsqlConnection conn, NpgsqlTransaction tx, int carId, bool isAvailable)
        {
            using var cmd = new NpgsqlCommand(
                "UPDATE cars SET is_available = @avail WHERE id = @id", conn);
            cmd.Transaction = tx;
            cmd.Parameters.AddWithValue("avail", isAvailable);
            cmd.Parameters.AddWithValue("id", carId);
            cmd.ExecuteNonQuery();
        }

        public void InsertPayment(NpgsqlConnection conn, NpgsqlTransaction tx, Payment payment)
        {
            using var cmd = new NpgsqlCommand(
                @"INSERT INTO payments (rental_id, total_summer, payment_date, payment_method, status)
          VALUES (@rid, @sum, @date, @method, @status)", conn);
            cmd.Transaction = tx;
            cmd.Parameters.AddWithValue("rid", payment.RentalId);
            cmd.Parameters.AddWithValue("sum", payment.TotalSummer);
            cmd.Parameters.AddWithValue("date", payment.PaymentDate);
            cmd.Parameters.AddWithValue("method", payment.PaymentMethod);
            cmd.Parameters.AddWithValue("status", payment.Status);
            cmd.ExecuteNonQuery();
        }

        public void UpdateRentalStatus(NpgsqlConnection conn, NpgsqlTransaction tx, int rentalId, string status, DateTime? actualReturnDate, int? returnLocationId)
        {
            using var cmd = new NpgsqlCommand(
                @"UPDATE rentals SET status = @status, actual_return_date = @date, return_location_id = @locid
          WHERE id = @id", conn);
            cmd.Transaction = tx;
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("date", (object?)actualReturnDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("locid", (object?)returnLocationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", rentalId);
            cmd.ExecuteNonQuery();
        }



        public void DeleteRentalsByCar(int carId)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("DELETE FROM rentals WHERE car_id = @id", conn);
            cmd.Parameters.AddWithValue("id", carId);
            cmd.ExecuteNonQuery();
        }

        public void SetCarAvailability(int carId, bool isAvailable)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("UPDATE cars SET is_available = @a WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("a", isAvailable);
            cmd.Parameters.AddWithValue("id", carId);
            cmd.ExecuteNonQuery();
        }

        public int CountRentalsByCar(int carId)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM rentals WHERE car_id = @id", conn);
            cmd.Parameters.AddWithValue("id", carId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}