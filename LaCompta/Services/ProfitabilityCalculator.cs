using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;

namespace LaCompta.Services
{
    public static class ProfitabilityCalculator
    {
        // Cache fertilizer cost per game day to avoid re-scanning farm tiles for every item
        private static int _cachedFertilizerCost;
        private static int _cachedFertilizerDay = -1;
        private static int _cachedFertilizerYear = -1;

        // Fertilizer shop prices (what the player actually pays)
        private static readonly Dictionary<string, int> FertilizerPrices = new()
        {
            { "368", 2 },    // Basic Fertilizer
            { "369", 10 },   // Quality Fertilizer
            { "919", 70 },   // Deluxe Fertilizer
            { "465", 20 },   // Speed-Gro
            { "466", 40 },   // Deluxe Speed-Gro
            { "918", 70 },   // Hyper Speed-Gro
            { "370", 4 },    // Basic Retaining Soil
            { "371", 5 },    // Quality Retaining Soil
            { "920", 30 },   // Deluxe Retaining Soil
        };

        public static int GetCostBasis(Item item)
        {
            if (item is not StardewValley.Object obj)
                return 0;

            // Only crops have meaningful cost basis
            if (obj.Category != StardewValley.Object.VegetableCategory &&
                obj.Category != StardewValley.Object.FruitsCategory &&
                obj.Category != StardewValley.Object.flowersCategory)
                return 0;

            var seedCost = FindSeedCostForCrop(obj.ItemId);
            var fertilizerCost = EstimateAverageFertilizerCost();

            return seedCost + fertilizerCost;
        }

        public static int CalculateProfit(Item item, int quantity = 1)
        {
            var sellPrice = GetSellPrice(item) * quantity;
            var costBasis = GetCostBasis(item) * quantity;
            return sellPrice - costBasis;
        }

        public static int GetSellPrice(Item item)
        {
            if (item is StardewValley.Object obj)
                return obj.sellToStorePrice();

            return item.salePrice() / 2;
        }

        public static int EstimateAverageFertilizerCost()
        {
            // Return cached value if already computed this game day
            if (Context.IsWorldReady && Game1.dayOfMonth == _cachedFertilizerDay && Game1.year == _cachedFertilizerYear)
                return _cachedFertilizerCost;

            var farm = Game1.getFarm();
            if (farm == null)
                return 0;

            int totalFertilizerCost = 0;
            int fertilizedTiles = 0;
            int totalCropTiles = 0;

            foreach (var pair in farm.terrainFeatures.Pairs)
            {
                if (pair.Value is not HoeDirt hoeDirt)
                    continue;

                // Only count tiles that have a crop
                if (hoeDirt.crop == null)
                    continue;

                totalCropTiles++;

                var fertilizer = hoeDirt.fertilizer.Value;
                if (fertilizer != null && FertilizerPrices.TryGetValue(fertilizer, out var price))
                {
                    totalFertilizerCost += price;
                    fertilizedTiles++;
                }
            }

            if (totalCropTiles == 0)
                return 0;

            // Average fertilizer cost per crop tile — cache for this game day
            _cachedFertilizerCost = totalFertilizerCost / totalCropTiles;
            if (Context.IsWorldReady)
            {
                _cachedFertilizerDay = Game1.dayOfMonth;
                _cachedFertilizerYear = Game1.year;
            }
            return _cachedFertilizerCost;
        }

        public static int GetFertilizerPrice(string fertilizerId)
        {
            return FertilizerPrices.GetValueOrDefault(fertilizerId, 0);
        }

        private static int FindSeedCostForCrop(string cropItemId)
        {
            if (Game1.cropData == null)
                return 0;

            foreach (var kvp in Game1.cropData)
            {
                var cropData = kvp.Value;
                if (cropData.HarvestItemId == cropItemId)
                {
                    var seedId = kvp.Key;
                    if (Game1.objectData != null && Game1.objectData.TryGetValue(seedId, out var seedData))
                    {
                        return seedData.Price;
                    }
                }
            }

            return 0;
        }
    }
}
