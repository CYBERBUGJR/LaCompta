using StardewValley;

namespace LaCompta.Services
{
    public static class CategoryClassifier
    {
        public const string Farming = "Farming";
        public const string Foraging = "Foraging";
        public const string Fishing = "Fishing";
        public const string Mining = "Mining";
        public const string Other = "Other";

        /// <summary>
        /// Classify a Stardew Valley item into an income category.
        /// Uses numeric category IDs from game data (more reliable than named constants).
        /// Reference: https://stardewvalleywiki.com/Modding:Items#Categories
        /// </summary>
        public static string Classify(Item item)
        {
            if (item is not StardewValley.Object obj)
                return Other;

            return obj.Category switch
            {
                // Fishing
                -4  => Fishing,  // Fish
                -20 => Fishing,  // Trash (fishing junk)
                -21 => Fishing,  // Bait
                -22 => Fishing,  // Tackle

                // Farming: crops, animal products, artisan goods
                -5  => Farming,  // Eggs
                -6  => Farming,  // Milk
                -7  => Farming,  // Cooking (artisan-adjacent)
                -14 => Farming,  // Meat
                -18 => Farming,  // Animal products (sell at Marnie's)
                -26 => Farming,  // Artisan goods
                -74 => Farming,  // Seeds
                -75 => Farming,  // Vegetables
                -79 => Farming,  // Fruit
                -80 => Farming,  // Flowers

                // Foraging
                -23 => Foraging, // Sell at Pierre's (forage)
                -27 => Foraging, // Tree sap/syrup
                -81 => Foraging, // Greens/forage

                // Mining
                -2  => Mining,   // Gems
                -12 => Mining,   // Minerals
                -15 => Mining,   // Metal resources
                -28 => Mining,   // Monster loot

                // Other
                _ => Other
            };
        }

        /// <summary>
        /// Get all valid category names.
        /// </summary>
        public static string[] AllCategories => new[]
        {
            Farming, Foraging, Fishing, Mining, Other
        };
    }
}
