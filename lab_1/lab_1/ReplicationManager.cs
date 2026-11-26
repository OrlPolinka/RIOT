using Npgsql;
using System;
using System.Collections.Generic;

namespace lab_1
{
    public class ReplicationManager
    {
        private readonly string _primaryConnectionString;
        private readonly string _replicaConnectionString;

        public bool IsPrimaryAvailable { get; private set; } = true;
        public bool IsReplicaAvailable { get; private set; } = true;

        public string CurrentInstance
        {
            get
            {
                if (IsPrimaryAvailable) return "Primary";
                if (IsReplicaAvailable) return "Replica";
                return "None";
            }
        }

        public ReplicationManager(string primaryConn, string replicaConn)
        {
            _primaryConnectionString = primaryConn;
            _replicaConnectionString = replicaConn;
            CheckAvailability();
        }

        public void CheckAvailability()
        {
            bool wasPrimaryAvailable = IsPrimaryAvailable;
            bool wasReplicaAvailable = IsReplicaAvailable;

            IsPrimaryAvailable = CanConnect(_primaryConnectionString);
            IsReplicaAvailable = CanConnect(_replicaConnectionString);

            if (wasPrimaryAvailable != IsPrimaryAvailable)
                Log($"Primary: {(IsPrimaryAvailable ? "AVAILABLE" : "UNAVAILABLE")}",
                    IsPrimaryAvailable ? ConsoleColor.Green : ConsoleColor.Red);

            if (wasReplicaAvailable != IsReplicaAvailable)
                Log($"Replica: {(IsReplicaAvailable ? "AVAILABLE" : "UNAVAILABLE")}",
                    IsReplicaAvailable ? ConsoleColor.Green : ConsoleColor.Red);
        }

        private bool CanConnect(string connString)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                conn.Open();
                using var cmd = new NpgsqlCommand("SELECT 1", conn);
                cmd.ExecuteScalar();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetActiveConnectionString()
        {
            CheckAvailability();

            if (IsPrimaryAvailable) return _primaryConnectionString;
            if (IsReplicaAvailable)
            {
                Log("Failover → переключение на Replica", ConsoleColor.Yellow);
                return _replicaConnectionString;
            }

            throw new Exception("Оба экземпляра БД недоступны!");
        }

        public void SimulatePrimaryFailure()
        {
            Log("Для симуляции отказа: переименуйте БД car_rental в pgAdmin", ConsoleColor.Yellow);
            Log("Команда: ALTER DATABASE car_rental RENAME TO car_rental_disabled;", ConsoleColor.Yellow);
        }

        public void RestorePrimary()
        {
            Log("Primary → AVAILABLE (проверяем...)", ConsoleColor.Green);
            CheckAvailability();
        }

        public void ReplicateCarAvailability(int carId, bool isAvailable)
        {
            if (!IsReplicaAvailable)
            {
                Log("Replica недоступна — репликация пропущена", ConsoleColor.Red);
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(_replicaConnectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(
                    "UPDATE cars SET is_available = @a WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("a", isAvailable);
                cmd.Parameters.AddWithValue("id", carId);
                int rows = cmd.ExecuteNonQuery();

                Log($"Replication → Replica: car #{carId} is_available = {isAvailable}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log($"Ошибка репликации: {ex.Message}", ConsoleColor.Red);
            }
        }

        public (bool success, string message) Synchronize()
        {
            try
            {
                Log("═══════════════════════════════════════", ConsoleColor.Cyan);
                Log("SYNCHRONIZATION: Replica → Primary", ConsoleColor.Cyan);
                Log("═══════════════════════════════════════", ConsoleColor.Cyan);

                CheckAvailability();
                if (!IsPrimaryAvailable)
                {
                    Log("Primary недоступен — синхронизация невозможна", ConsoleColor.Red);
                    return (false, "Primary недоступен");
                }
                if (!IsReplicaAvailable)
                {
                    Log("Replica недоступна — синхронизация невозможна", ConsoleColor.Red);
                    return (false, "Replica недоступна");
                }

                using var primaryConn = new NpgsqlConnection(_primaryConnectionString);
                using var replicaConn = new NpgsqlConnection(_replicaConnectionString);
                primaryConn.Open();
                replicaConn.Open();

                Log("Синхронизация таблиц...", ConsoleColor.Yellow);

                SyncTable(replicaConn, primaryConn, "customers",
                    new[] { "surname", "name", "patronymic", "email", "phone_number",
                    "driver_license_number", "date_of_birth", "registration_date" });

                SyncTable(replicaConn, primaryConn, "locations",
                    new[] { "city", "address", "phone", "working_hours" });

                SyncTable(replicaConn, primaryConn, "cars",
                    new[] { "brand", "model", "year", "license_plate", "price_per_day",
                    "is_available", "color" });

                SyncTable(replicaConn, primaryConn, "rentals",
                    new[] { "customer_id", "car_id", "pickup_location_id", "return_location_id",
                    "start_date", "end_date", "actual_return_date", "total_cost", "status" });

                SyncTable(replicaConn, primaryConn, "payments",
                    new[] { "rental_id", "total_summer", "payment_date", "payment_method", "status" });

                Log("═══════════════════════════════════════", ConsoleColor.Cyan);
                Log("SYNCHRONIZATION → SUCCESS", ConsoleColor.Green);
                Log("Primary = Replica", ConsoleColor.Green);
                Log("═══════════════════════════════════════", ConsoleColor.Cyan);

                return (true, "Синхронизация выполнена успешно");
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА синхронизации: {ex.Message}", ConsoleColor.Red);
                return (false, ex.Message);
            }
        }

        private void SyncTable(NpgsqlConnection source, NpgsqlConnection target, string table, string[] columns)
        {
            try
            {
                using (var disableFk = new NpgsqlCommand("SET session_replication_role = 'replica';", target))
                {
                    disableFk.ExecuteNonQuery();
                }

                using (var delCmd = new NpgsqlCommand($"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE", target))
                {
                    delCmd.ExecuteNonQuery();
                }

                var columnsStr = string.Join(", ", columns);
                var rows = new List<(int id, object[] values)>();

                using (var readCmd = new NpgsqlCommand($"SELECT id, {columnsStr} FROM {table} ORDER BY id", source))
                using (var reader = readCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        var values = new object[columns.Length];
                        for (int i = 0; i < columns.Length; i++)
                        {
                            values[i] = reader.IsDBNull(i + 1) ? DBNull.Value : reader.GetValue(i + 1);
                        }
                        rows.Add((id, values));
                    }
                }

                foreach (var (id, values) in rows)
                {
                    var paramNames = new List<string> { "@id" };
                    var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@id", id)
            };

                    for (int i = 0; i < columns.Length; i++)
                    {
                        var pName = $"@p{i}";
                        parameters.Add(new NpgsqlParameter(pName, values[i] ?? DBNull.Value));
                        paramNames.Add(pName);
                    }

                    var insertSql = $"INSERT INTO {table} (id, {columnsStr}) VALUES ({string.Join(", ", paramNames)})";

                    using var insertCmd = new NpgsqlCommand(insertSql, target);
                    insertCmd.Parameters.AddRange(parameters.ToArray());
                    insertCmd.ExecuteNonQuery();
                }

                using (var enableFk = new NpgsqlCommand("SET session_replication_role = 'origin';", target))
                {
                    enableFk.ExecuteNonQuery();
                }

                using (var seqCmd = new NpgsqlCommand(
                    $"SELECT setval(pg_get_serial_sequence('{table}', 'id'), " +
                    $"COALESCE((SELECT MAX(id) FROM {table}), 1), true)", target))
                {
                    seqCmd.ExecuteNonQuery();
                }

                Log($"  {table}: {rows.Count} строк скопировано", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log($"  {table}: ОШИБКА — {ex.Message}", ConsoleColor.Red);
                throw;
            }
        }

        private void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Replication] {message}");
            Console.ForegroundColor = oldColor;
        }
    }
}