using System;

namespace lab_1
{
    public static class AppConfig
    {
        public static string PrimaryConnection =>
            Environment.GetEnvironmentVariable("DB_PRIMARY_CONN")
            ?? "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";

        public static string ReplicaConnection =>
            Environment.GetEnvironmentVariable("DB_REPLICA_CONN")
            ?? "Host=localhost;Port=5432;Database=car_rental_replica;Username=postgres;Password=postgres";

        public static int ServerPort =>
            int.TryParse(Environment.GetEnvironmentVariable("SERVER_PORT"), out var p) ? p : 3000;

        public static string ClientHost =>
            Environment.GetEnvironmentVariable("SERVER_HOST") ?? "127.0.0.1";
    }
}