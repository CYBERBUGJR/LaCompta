using Microsoft.Data.Sqlite;
using System.IO;

namespace LaCompta.Data
{
    public class DatabaseContext
    {
        private readonly string _connectionString;

        public DatabaseContext(string modDataPath)
        {
            var dbPath = Path.Combine(modDataPath, "lacompta.db");
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        public SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void InitializeDatabase()
        {
            using var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS daily_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    season TEXT NOT NULL,
                    year INTEGER NOT NULL,
                    day INTEGER NOT NULL,
                    farming_income INTEGER NOT NULL DEFAULT 0,
                    foraging_income INTEGER NOT NULL DEFAULT 0,
                    fishing_income INTEGER NOT NULL DEFAULT 0,
                    mining_income INTEGER NOT NULL DEFAULT 0,
                    other_income INTEGER NOT NULL DEFAULT 0,
                    total_expenses INTEGER NOT NULL DEFAULT 0,
                    player_id TEXT NOT NULL DEFAULT '',
                    UNIQUE(season, year, day, player_id)
                );

                CREATE TABLE IF NOT EXISTS item_transactions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    daily_record_id INTEGER NOT NULL,
                    item_name TEXT NOT NULL,
                    item_id TEXT NOT NULL,
                    category TEXT NOT NULL,
                    quantity INTEGER NOT NULL,
                    unit_price INTEGER NOT NULL,
                    total_price INTEGER NOT NULL,
                    cost_basis INTEGER NOT NULL DEFAULT 0,
                    season TEXT NOT NULL,
                    year INTEGER NOT NULL,
                    day INTEGER NOT NULL,
                    player_id TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY (daily_record_id) REFERENCES daily_records(id)
                );

                CREATE TABLE IF NOT EXISTS season_summaries (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    season TEXT NOT NULL,
                    year INTEGER NOT NULL,
                    farming_total INTEGER NOT NULL DEFAULT 0,
                    foraging_total INTEGER NOT NULL DEFAULT 0,
                    fishing_total INTEGER NOT NULL DEFAULT 0,
                    mining_total INTEGER NOT NULL DEFAULT 0,
                    other_total INTEGER NOT NULL DEFAULT 0,
                    total_expenses INTEGER NOT NULL DEFAULT 0,
                    best_day INTEGER NOT NULL DEFAULT 0,
                    best_day_income INTEGER NOT NULL DEFAULT 0,
                    player_id TEXT NOT NULL DEFAULT '',
                    UNIQUE(season, year, player_id)
                );

                CREATE TABLE IF NOT EXISTS fish_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    fish_name TEXT NOT NULL,
                    fish_id TEXT NOT NULL,
                    is_legendary INTEGER NOT NULL DEFAULT 0,
                    quantity INTEGER NOT NULL DEFAULT 1,
                    total_revenue INTEGER NOT NULL DEFAULT 0,
                    season TEXT NOT NULL,
                    year INTEGER NOT NULL,
                    day INTEGER NOT NULL,
                    player_id TEXT NOT NULL DEFAULT '',
                    UNIQUE(fish_id, season, year, day, player_id)
                );

                CREATE INDEX IF NOT EXISTS idx_daily_records_season_year ON daily_records(season, year, player_id);
                CREATE INDEX IF NOT EXISTS idx_item_transactions_daily ON item_transactions(daily_record_id);
                CREATE INDEX IF NOT EXISTS idx_fish_records_legendary ON fish_records(is_legendary);
            ";
            command.ExecuteNonQuery();
        }
    }
}
