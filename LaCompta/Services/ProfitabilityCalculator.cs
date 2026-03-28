using StardewValley;
using StardewValley.GameData.Crops;
using System.Collections.Generic;
using System.Linq;

namespace LaCompta.Services
{
    public static class ProfitabilityCalculator
    {
        /// <summary>
        /// Calculate the cost basis for a harvested crop item.
        /// Returns the estimated seed cost. For non-crop items, returns 0.
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

            // Try to find the seed that produces this crop
            var seedCost = FindSeedCostForCrop(obj.ItemId);
            return seedCost;
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
                    // kvp.Key is the seed item ID
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
