using LaCompta.Data;
using LaCompta.Models;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using System.Linq;

namespace LaCompta.Services
{
    public class TrackingService
    {
        private readonly Repository _repo;
        private readonly IMonitor _monitor;
        private int _previousMoney;

        // Known legendary fish IDs in Stardew Valley 1.6
        private static readonly HashSet<string> LegendaryFishIds = new()
        {
            "159",  // Crimsonfish
            "160",  // Angler
            "163",  // Legend
            "775",  // Glacierfish
            "682",  // Mutant Carp
            "898",  // Son of Crimsonfish
            "899",  // Ms. Angler
            "900",  // Legend II
            "901",  // Radioactive Carp
            "902"   // Glacierfish Jr.
        };

        public TrackingService(Repository repo, IMonitor monitor)
        {
            _repo = repo;
            _monitor = monitor;
        }

        /// <summary>
        /// Call at start of day to snapshot current money for expense tracking.
        /// </summary>
        public void OnDayStarted()
        {
            _previousMoney = Game1.player.Money;
        }

        /// <summary>
        /// Process end-of-day: read shipping bin, classify items, record transactions.
        /// </summary>
        public void OnDayEnding()
        {
            var player = Game1.player;
            var season = Game1.currentSeason;
            var year = Game1.year;
            var day = Game1.dayOfMonth;
            var playerId = player.UniqueMultiplayerID.ToString();

            // Categorized income accumulators
            long farmingIncome = 0;
            long foragingIncome = 0;
            long fishingIncome = 0;
            long miningIncome = 0;
            long otherIncome = 0;

            // Process shipping bin
            var shippingBin = Game1.getFarm().getShippingBin(player);
            var transactions = new List<ItemTransaction>();

            foreach (var item in shippingBin)
            {
                var category = CategoryClassifier.Classify(item);
                var sellPrice = ProfitabilityCalculator.GetSellPrice(item);
                var totalPrice = (long)sellPrice * item.Stack;
                var costBasis = (long)ProfitabilityCalculator.GetCostBasis(item) * item.Stack;

                // Accumulate by category
                switch (category)
                {
                    case CategoryClassifier.Farming: farmingIncome += totalPrice; break;
                    case CategoryClassifier.Foraging: foragingIncome += totalPrice; break;
                    case CategoryClassifier.Fishing: fishingIncome += totalPrice; break;
                    case CategoryClassifier.Mining: miningIncome += totalPrice; break;
                    default: otherIncome += totalPrice; break;
                }

                // Create item transaction
                transactions.Add(new ItemTransaction
                {
                    ItemName = item.DisplayName,
                    ItemId = item.ItemId,
                    Category = category,
                    Quantity = item.Stack,
                    UnitPrice = sellPrice,
                    TotalPrice = totalPrice,
                    CostBasis = costBasis,
                    Season = season,
                    Year = year,
                    Day = day,
                    PlayerId = playerId
                });

                // Check for legendary fish
                if (item is StardewValley.Object obj && obj.Category == StardewValley.Object.FishCategory)
                {
                    var isLegendary = LegendaryFishIds.Contains(item.ItemId);
                    _repo.AddFishRecord(new FishRecord
                    {
                        FishName = item.DisplayName,
                        FishId = item.ItemId,
                        IsLegendary = isLegendary,
                        Quantity = item.Stack,
                        TotalRevenue = totalPrice,
                        Season = season,
                        Year = year,
                        Day = day,
                        PlayerId = playerId
                    });

                    if (isLegendary)
                    {
                        _monitor.Log($"LEGENDARY FISH SOLD: {item.DisplayName} for {totalPrice}g!", LogLevel.Info);
                    }
                }
            }

            // Estimate expenses from money delta
            // Money at end of day (before shipping) minus money at start of day
            // Negative delta = spending (purchases, tool upgrades, etc.)
            long expenses = 0;
            int currentMoney = Game1.player.Money;
            long totalShippingIncome = farmingIncome + foragingIncome + fishingIncome + miningIncome + otherIncome;

            // Money change = (current money - previous money)
            // If player spent money: current < previous (before shipping income is added)
            // Expenses = max(0, previousMoney - currentMoney) — but shipping hasn't been added yet at DayEnding
            if (_previousMoney > currentMoney)
            {
                expenses = _previousMoney - currentMoney;
            }

            // Save daily record
            var dailyRecord = new DailyRecord
            {
                Season = season,
                Year = year,
                Day = day,
                FarmingIncome = farmingIncome,
                ForagingIncome = foragingIncome,
                FishingIncome = fishingIncome,
                MiningIncome = miningIncome,
                OtherIncome = otherIncome,
                TotalExpenses = expenses,
                PlayerId = playerId
            };

            var recordId = _repo.AddDailyRecord(dailyRecord);

            // Save item transactions
            foreach (var tx in transactions)
            {
                tx.DailyRecordId = recordId;
                _repo.AddItemTransaction(tx);
            }

            // Log summary
            var totalIncome = farmingIncome + foragingIncome + fishingIncome + miningIncome + otherIncome;
            _monitor.Log(
                $"Day {day} {season} Y{year} | " +
                $"Income: {totalIncome}g (Farm:{farmingIncome} Forage:{foragingIncome} Fish:{fishingIncome} Mine:{miningIncome} Other:{otherIncome}) | " +
                $"Expenses: {expenses}g | Net: {totalIncome - expenses}g | " +
                $"Items shipped: {shippingBin.Count}",
                LogLevel.Info
            );
        }
    }
}
