using LaCompta.Models;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace LaCompta.Data
{
    public class Repository
    {
        private readonly DatabaseContext _db;

        public Repository(DatabaseContext db)
        {
            _db = db;
        }

        // === Daily Records ===

        public long AddDailyRecord(DailyRecord record)
        {
            using var conn = _db.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO daily_records (season, year, day, farming_income, foraging_income, fishing_income, mining_income, other_income, total_expenses, player_id)
                VALUES ($season, $year, $day, $farming, $foraging, $fishing, $mining, $other, $expenses, $playerId)
                ON CONFLICT(season, year, day, player_id) DO UPDATE SET
                    farming_income = $farming,
                    foraging_income = $foraging,
                    fishing_income = $fishing,
                    mining_income = $mining,
                    other_income = $other,
                    total_expenses = $expenses;
                SELECT id FROM daily_records WHERE season = $season AND year = $year AND day = $day AND player_id = $playerId;";
            cmd.Parameters.AddWithValue("$season", record.Season);
            cmd.Parameters.AddWithValue("$year", record.Year);
            cmd.Parameters.AddWithValue("$day", record.Day);
            cmd.Parameters.AddWithValue("$farming", record.FarmingIncome);
            cmd.Parameters.AddWithValue("$foraging", record.ForagingIncome);
            cmd.Parameters.AddWithValue("$fishing", record.FishingIncome);
            cmd.Parameters.AddWithValue("$mining", record.MiningIncome);
            cmd.Parameters.AddWithValue("$other", record.OtherIncome);
            cmd.Parameters.AddWithValue("$expenses", record.TotalExpenses);
            cmd.Parameters.AddWithValue("$playerId", record.PlayerId);
            return (long)cmd.ExecuteScalar()!;
        }

        public List<DailyRecord> GetDailyRecords(string season, int year, string playerId = "")
        {
            using var conn = _db.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM daily_records WHERE season = $season AND year = $year AND ($playerId = '' OR player_id = $playerId) ORDER BY day";
            cmd.Parameters.AddWithValue("$season", season);
            cmd.Parameters.AddWithValue("$year", year);
            cmd.Parameters.AddWithValue("$playerId", playerId);

            var records = new List<DailyRecord>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(MapDailyRecord(reader));
            }
            return records;
        }

        // === Item Transactions ===

        public void AddItemTransaction(ItemTransaction tx)
        {
            using var conn = _db.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO item_transactions (daily_record_id, item_name, item_id, category, quantity, unit_price, total_price, cost_basis, season, year, day, player_id)
                VALUES ($dailyId, $name, $itemId, $cat, $qty, $unit, $total, $cost, $season, $year, $day, $playerId)";
            cmd.Parameters.AddWithValue("$dailyId", tx.DailyRecordId);
            cmd.Parameters.AddWithValue("$name", tx.ItemName);
            cmd.Parameters.AddWithValue("$itemId", tx.ItemId);
            cmd.Parameters.AddWithValue("$cat", tx.Category);
            cmd.Parameters.AddWithValue("$qty", tx.Quantity);
            cmd.Parameters.AddWithValue("$unit", tx.UnitPrice);
            cmd.Parameters.AddWithValue("$total", tx.TotalPrice);
            cmd.Parameters.AddWithValue("$cost", tx.CostBasis);
            cmd.Parameters.AddWithValue("$season", tx.Season);
            cmd.Parameters.AddWithValue("$year", tx.Year);
            cmd.Parameters.AddWithValue("$day", tx.Day);
            cmd.Parameters.AddWithValue("$playerId", tx.PlayerId);
            cmd.ExecuteNonQuery();
        }

        public List<ItemTransaction> GetTopProfitableItems(string season, int year, int limit = 10, string playerId = "")
        {
            using var conn = _db.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT *, (total_price - cost_basis) as profit FROM item_transactions
                WHERE season = $season AND year = $year AND ($playerId = '' OR player_id = $playerId)
                ORDER BY profit DESC LIMIT $limit";
            cmd.Parameters.AddWithValue("$season", season);
            cmd.Parameters.AddWithValue("$year", year);
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.Parameters.AddWithValue("$playerId", playerId);

            var items = new List<ItemTransaction>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(MapItemTransaction(reader));
            }
            return items;
        }

        // === Season Summaries ===

        public void SaveSeasonSummary(SeasonSummary summary)
        {
            using var conn = _db.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO season_summaries (season, year, farming_total, foraging_total, fishing_total, mining_total, other_total, total_expenses, best_day, best_day_income, player_id)
                VALUES ($season, $year, $farming, $foraging, $fishing, $mining, $other, $expenses, $bestDay, $bestIncome, $playerId)";
            cmd.Parameters.AddWithValue("$season", summary.Season);
            cmd.Parameters.AddWithValue("$year", summary.Year);
            cmd.Parameters.AddWithValue("$farming", summary.FarmingTotal);
            cmd.Parameters.AddWithValue("$foraging", summary.ForagingTotal);
            cmd.Parameters.AddWithValue("$fishing", summary.FishingTotal);
            cmd.Parameters.AddWithValue("$mining", summary.MiningTotal);
            cmd.Parameters.AddWithValue("$other", summary.OtherTotal);
            cmd.Parameters.AddWithValue("$expenses", summary.TotalExpenses);
            cmd.Parameters.AddWithValue("$bestDay", summary.BestDay);
            cmd.Parameters.AddWithValue("$bestIncome", summary.BestDayIncome);
            cmd.Parameters.AddWithValue("$playerId", summary.PlayerId);
            cmd.ExecuteNonQuery();
        }

        public List<SeasonSummary> GetAllSeasonSummaries(string playerId = "")
        {
            using var conn = _db.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM season_summaries WHERE ($playerId = '' OR player_id = $playerId) ORDER BY year, CASE season WHEN 'spring' THEN 1 WHEN 'summer' THEN 2 WHEN 'fall' THEN 3 WHEN 'winter' THEN 4 END";
            cmd.Parameters.AddWithValue("$playerId", playerId);

            var summaries = new List<SeasonSummary>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                summaries.Add(MapSeasonSummary(reader));
            }
            return summaries;
        }

        // === Fish Records ===

        public void AddFishRecord(FishRecord fish)
        {
            using var conn = _db.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO fish_records (fish_name, fish_id, is_legendary, quantity, total_revenue, season, year, day, player_id)
                VALUES ($name, $fishId, $legendary, $qty, $revenue, $season, $year, $day, $playerId)
                ON CONFLICT(fish_id, season, year, day, player_id) DO UPDATE SET
                    quantity = quantity + $qty,
                    total_revenue = total_revenue + $revenue";
            cmd.Parameters.AddWithValue("$name", fish.FishName);
            cmd.Parameters.AddWithValue("$fishId", fish.FishId);
            cmd.Parameters.AddWithValue("$legendary", fish.IsLegendary ? 1 : 0);
            cmd.Parameters.AddWithValue("$qty", fish.Quantity);
            cmd.Parameters.AddWithValue("$revenue", fish.TotalRevenue);
            cmd.Parameters.AddWithValue("$season", fish.Season);
            cmd.Parameters.AddWithValue("$year", fish.Year);
            cmd.Parameters.AddWithValue("$day", fish.Day);
            cmd.Parameters.AddWithValue("$playerId", fish.PlayerId);
            cmd.ExecuteNonQuery();
        }

        public List<FishRecord> GetLegendaryFish(string playerId = "")
            => GetFishRecords(playerId, legendaryOnly: true);

        public List<FishRecord> GetAllFish(string playerId = "")
            => GetFishRecords(playerId, legendaryOnly: false);

        private List<FishRecord> GetFishRecords(string playerId, bool legendaryOnly)
        {
            using var conn = _db.GetConnection();
            var cmd = conn.CreateCommand();
            var where = legendaryOnly ? "is_legendary = 1 AND " : "";
            cmd.CommandText = $"SELECT * FROM fish_records WHERE {where}($playerId = '' OR player_id = $playerId) ORDER BY year, day";
            cmd.Parameters.AddWithValue("$playerId", playerId);

            var fish = new List<FishRecord>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                fish.Add(MapFishRecord(reader));
            }
            return fish;
        }

        // === Mappers ===

        private static DailyRecord MapDailyRecord(SqliteDataReader reader)
        {
            return new DailyRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                Season = reader.GetString(reader.GetOrdinal("season")),
                Year = reader.GetInt32(reader.GetOrdinal("year")),
                Day = reader.GetInt32(reader.GetOrdinal("day")),
                FarmingIncome = reader.GetInt64(reader.GetOrdinal("farming_income")),
                ForagingIncome = reader.GetInt64(reader.GetOrdinal("foraging_income")),
                FishingIncome = reader.GetInt64(reader.GetOrdinal("fishing_income")),
                MiningIncome = reader.GetInt64(reader.GetOrdinal("mining_income")),
                OtherIncome = reader.GetInt64(reader.GetOrdinal("other_income")),
                TotalExpenses = reader.GetInt64(reader.GetOrdinal("total_expenses")),
                PlayerId = reader.GetString(reader.GetOrdinal("player_id"))
            };
        }

        private static ItemTransaction MapItemTransaction(SqliteDataReader reader)
        {
            return new ItemTransaction
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                DailyRecordId = reader.GetInt64(reader.GetOrdinal("daily_record_id")),
                ItemName = reader.GetString(reader.GetOrdinal("item_name")),
                ItemId = reader.GetString(reader.GetOrdinal("item_id")),
                Category = reader.GetString(reader.GetOrdinal("category")),
                Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                UnitPrice = reader.GetInt32(reader.GetOrdinal("unit_price")),
                TotalPrice = reader.GetInt64(reader.GetOrdinal("total_price")),
                CostBasis = reader.GetInt64(reader.GetOrdinal("cost_basis")),
                Season = reader.GetString(reader.GetOrdinal("season")),
                Year = reader.GetInt32(reader.GetOrdinal("year")),
                Day = reader.GetInt32(reader.GetOrdinal("day")),
                PlayerId = reader.GetString(reader.GetOrdinal("player_id"))
            };
        }

        private static SeasonSummary MapSeasonSummary(SqliteDataReader reader)
        {
            return new SeasonSummary
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                Season = reader.GetString(reader.GetOrdinal("season")),
                Year = reader.GetInt32(reader.GetOrdinal("year")),
                FarmingTotal = reader.GetInt64(reader.GetOrdinal("farming_total")),
                ForagingTotal = reader.GetInt64(reader.GetOrdinal("foraging_total")),
                FishingTotal = reader.GetInt64(reader.GetOrdinal("fishing_total")),
                MiningTotal = reader.GetInt64(reader.GetOrdinal("mining_total")),
                OtherTotal = reader.GetInt64(reader.GetOrdinal("other_total")),
                TotalExpenses = reader.GetInt64(reader.GetOrdinal("total_expenses")),
                BestDay = reader.GetInt32(reader.GetOrdinal("best_day")),
                BestDayIncome = reader.GetInt64(reader.GetOrdinal("best_day_income")),
                PlayerId = reader.GetString(reader.GetOrdinal("player_id"))
            };
        }

        private static FishRecord MapFishRecord(SqliteDataReader reader)
        {
            return new FishRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                FishName = reader.GetString(reader.GetOrdinal("fish_name")),
                FishId = reader.GetString(reader.GetOrdinal("fish_id")),
                IsLegendary = reader.GetInt32(reader.GetOrdinal("is_legendary")) == 1,
                Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                TotalRevenue = reader.GetInt64(reader.GetOrdinal("total_revenue")),
                Season = reader.GetString(reader.GetOrdinal("season")),
                Year = reader.GetInt32(reader.GetOrdinal("year")),
                Day = reader.GetInt32(reader.GetOrdinal("day")),
                PlayerId = reader.GetString(reader.GetOrdinal("player_id"))
            };
        }
    }
}
