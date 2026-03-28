using StardewValley;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;

namespace LaCompta.Services
{
    public static class ProfitabilityCalculator
    {
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

        /// <summary>
        /// Calculate the cost basis for a harvested crop item.
        /// Includes seed cost + estimated fertilizer cost.
        /// For non-crop items, returns 0.
        /// </summary>
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

        /// <summary>
        /// Calculate profit for an item: sell price - cost basis.
        /// </summary>
        public static int CalculateProfit(Item item, int quantity = 1)
        {
            var sellPrice = GetSellPrice(item) * quantity;
            var costBasis = GetCostBasis(item) * quantity;
            return sellPrice - costBasis;
        }

        /// <summary>
        /// Get the sell price for an item.
        /// </summary>
        public static int GetSellPrice(Item item)
        {
            if (item is StardewValley.Object obj)
                return obj.sellToStorePrice();

            return item.salePrice() / 2;
        }

        /// <summary>
        /// Scan all HoeDirt tiles on the farm to estimate the average fertilizer
        /// cost per crop tile. This gives a fair per-item fertilizer cost estimate
        /// since we can't track which specific tile a shipped item came from.
        /// </summary>
        public static int EstimateAverageFertilizerCost()
        {
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

            // Average fertilizer cost per crop tile
            return totalFertilizerCost / totalCropTiles;
        }

        /// <summary>
        /// Get fertilizer price by item ID.
        /// </summary>
        public static int GetFertilizerPrice(string fertilizerId)
        {
            return FertilizerPrices.GetValueOrDefault(fertilizerId, 0);
        }

        /// <summary>
        /// Look up the seed cost for a crop by searching crop data.
        /// Returns 0 if seed not found.
        /// </summary>
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
