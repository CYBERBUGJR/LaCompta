using LaCompta.Data;
using LaCompta.Models;
using StardewModdingAPI;
using StardewValley;
using System.Linq;

namespace LaCompta.Services
{
    public class SeasonSummaryService
    {
        private readonly Repository _repo;
        private readonly IMonitor _monitor;

        public SeasonSummaryService(Repository repo, IMonitor monitor)
        {
            _repo = repo;
            _monitor = monitor;
        }

        /// <summary>
        /// Generate season summary on the last day of the season (day 28).
        /// Call this from DayEnding AFTER TrackingService has recorded today's data.
        /// </summary>
        public void OnDayEnding()
        {
            // Only generate summary on last day of season
            if (Game1.dayOfMonth != 28)
                return;

            var season = Game1.currentSeason;
            var year = Game1.year;
            var playerId = Game1.player.UniqueMultiplayerID.ToString();

            var dailyRecords = _repo.GetDailyRecords(season, year, playerId);

            if (!dailyRecords.Any())
            {
                _monitor.Log($"No records found for {season} Y{year}, skipping summary.", LogLevel.Warn);
                return;
            }

            // Aggregate totals
            long farmingTotal = dailyRecords.Sum(r => r.FarmingIncome);
            long foragingTotal = dailyRecords.Sum(r => r.ForagingIncome);
            long fishingTotal = dailyRecords.Sum(r => r.FishingIncome);
            long miningTotal = dailyRecords.Sum(r => r.MiningIncome);
            long otherTotal = dailyRecords.Sum(r => r.OtherIncome);
            long totalExpenses = dailyRecords.Sum(r => r.TotalExpenses);

            // Find best day
            var bestDay = dailyRecords.OrderByDescending(r => r.TotalIncome).First();

            var summary = new SeasonSummary
            {
                Season = season,
                Year = year,
                FarmingTotal = farmingTotal,
                ForagingTotal = foragingTotal,
                FishingTotal = fishingTotal,
                MiningTotal = miningTotal,
                OtherTotal = otherTotal,
                TotalExpenses = totalExpenses,
                BestDay = bestDay.Day,
                BestDayIncome = bestDay.TotalIncome,
                PlayerId = playerId
            };

            _repo.SaveSeasonSummary(summary);

            var totalIncome = farmingTotal + foragingTotal + fishingTotal + miningTotal + otherTotal;
            _monitor.Log(
                $"=== SEASON SUMMARY: {season} Y{year} ===" +
                $"\n  Total Income: {totalIncome}g | Expenses: {totalExpenses}g | Net: {totalIncome - totalExpenses}g" +
                $"\n  Farming: {farmingTotal}g | Foraging: {foragingTotal}g | Fishing: {fishingTotal}g | Mining: {miningTotal}g | Other: {otherTotal}g" +
                $"\n  Best Day: Day {bestDay.Day} with {bestDay.TotalIncome}g" +
                $"\n  Ka-ching! Another season in the books!",
                LogLevel.Info
            );
        }
    }
}
