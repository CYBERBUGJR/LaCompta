# Stardew Valley Modding Notes

Technical discoveries and tricks learned during LaCompta development.

*This is a living document - updated each phase with new findings.*

## SMAPI Event System

- **No dedicated shipping/selling event**: SMAPI doesn't fire an event when items are sold. Use `DayEnding` to read the shipping bin before it's cleared.
- **Shipping bin access**: `Game1.getFarm().getShippingBin(player)` returns items queued for shipping. Read this in `DayEnding` - by next day, it's cleared.
- **Event order matters**: `DayEnding` fires before the day ends. `DayStarted` fires after the new day begins. Money from shipping is added AFTER `DayEnding`.
- **Expense tracking**: No purchase event exists. Track money delta between `DayStarted` (snapshot) and `DayEnding` (compare). Negative delta = spending.

## Game Data Access

- **Item categories**: Use numeric category IDs, not named constants. Named constants like `maboreMaterialCategory` may not exist in all SMAPI versions. Safe categories: -4 (Fish), -75 (Vegetables), -79 (Fruit), -80 (Flowers), -26 (Artisan), -2 (Gems), -12 (Minerals), -15 (Metals), -81 (Greens/Forage).
- **Sell price**: `obj.sellToStorePrice()` gives the actual sell price accounting for quality and professions. `item.salePrice()` gives base catalog price.
- **Crop data**: `Game1.cropData` maps seed item IDs to `CropData` objects. `CropData.HarvestItemId` links back to the harvested item. Use this to find seed cost for profitability calculation.
- **Object data**: `Game1.objectData` contains item metadata including base prices.
- **Legendary fish IDs** (Stardew Valley 1.6): 159 (Crimsonfish), 160 (Angler), 163 (Legend), 775 (Glacierfish), 682 (Mutant Carp), 898 (Son of Crimsonfish), 899 (Ms. Angler), 900 (Legend II), 901 (Radioactive Carp), 902 (Glacierfish Jr.)
- **Player ID for multiplayer**: `Game1.player.UniqueMultiplayerID.ToString()` gives a stable player identifier.

## Fertilizer Tracking

- **HoeDirt.fertilizer.Value**: Returns the fertilizer item ID string on a tile, or null if none.
- **Can't track per-item fertilizer**: When items are in the shipping bin, we don't know which tile they came from. Instead, we scan all farm HoeDirt tiles with crops and calculate the average fertilizer cost per crop tile.
- **Fertilizer IDs**: 368 (Basic), 369 (Quality), 919 (Deluxe), 465 (Speed-Gro), 466 (Deluxe Speed-Gro), 918 (Hyper Speed-Gro), 370/371/920 (Retaining Soil).
- **Access pattern**: `foreach (var pair in farm.terrainFeatures.Pairs) { if (pair.Value is HoeDirt hd && hd.crop != null) ... }`

## Performance Considerations

- **SQLite in SMAPI**: SQLite operations are fast enough for end-of-day processing. No threading needed for the data layer.
- **Shipping bin iteration**: Happens once per day at DayEnding. Even with 100+ items, negligible performance impact.
- **Farm terrain scan**: Iterating all HoeDirt tiles on DayEnding for fertilizer estimation is fast — typical farms have <500 crop tiles.
