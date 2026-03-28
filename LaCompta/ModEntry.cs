using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace LaCompta
{
    internal sealed class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            this.Monitor.Log("LaCompta loaded - time to count those gold coins!", LogLevel.Info);
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            this.Monitor.Log($"Day {StardewValley.Game1.dayOfMonth} of {StardewValley.Game1.currentSeason}, Year {StardewValley.Game1.year} - Let's see those profits!", LogLevel.Debug);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            this.Monitor.Log($"Save loaded for {StardewValley.Game1.player.Name}'s farm. Initializing LaCompta...", LogLevel.Info);
        }
    }
}
