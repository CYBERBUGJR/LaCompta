using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using LaCompta.Data;
using LaCompta.Services;
using LaCompta.Web;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace LaCompta
{
    internal sealed class ModEntry : Mod
    {
        private DatabaseContext _db = null!;
        private Repository _repo = null!;
        private TrackingService _tracker = null!;
        private SeasonSummaryService _seasonSummary = null!;
        private WebServer _webServer = null!;

        public override void Entry(IModHelper helper)
        {
            this.Monitor.Log("LaCompta loaded bitch ! Time to count those coins!", LogLevel.Info);

            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;

            // Console commands
            helper.ConsoleCommands.Add("lacompta_test", "Run LaCompta integration tests", this.RunTests);
            helper.ConsoleCommands.Add("lacompta_status", "Show current DB stats", this.ShowStatus);
            helper.ConsoleCommands.Add("lacompta_open", "Open LaCompta dashboard in browser", this.OpenDashboard);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // Initialize database in mod's data folder
            var dataPath = this.Helper.DirectoryPath;
            _db = new DatabaseContext(dataPath);
            _repo = new Repository(_db);
            _tracker = new TrackingService(_repo, this.Monitor);
            _seasonSummary = new SeasonSummaryService(_repo, this.Monitor);

            // Start web server
            var api = new ApiController(_repo, this.Monitor);
            _webServer = new WebServer(api, this.Monitor);
            _webServer.Start();

            this.Monitor.Log($"LaCompta initialized for {Game1.player.Name}'s farm. Let the accounting begin!", LogLevel.Info);
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            _webServer?.Stop();
        }

        private void OpenDashboard(string command, string[] args)
        {
            var url = "http://localhost:5555";
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start(new ProcessStartInfo("open", url) { UseShellExecute = false });
                else
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

                this.Monitor.Log($"Opening dashboard at {url}", LogLevel.Info);
            }
            catch (System.Exception ex)
            {
                this.Monitor.Log($"Failed to open browser: {ex.Message}. Open {url} manually.", LogLevel.Warn);
            }
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            _tracker?.OnDayStarted();
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            if (_tracker == null)
                return;

            // Track daily income/expenses first
            _tracker.OnDayEnding();

            // Then generate season summary if it's end of season
            _seasonSummary.OnDayEnding();
        }

        /// <summary>
        /// Integration test: populate shipping bin with known items, simulate day cycle, verify DB.
        /// Usage: type "lacompta_test" in SMAPI console after loading a save.
        /// </summary>
        private void RunTests(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first!", LogLevel.Warn);
                return;
            }

            if (_tracker == null)
            {
                this.Monitor.Log("LaCompta not initialized. Load a save first!", LogLevel.Warn);
                return;
            }

            this.Monitor.Log("=== LaCompta Integration Tests ===", LogLevel.Info);
            int passed = 0;
            int failed = 0;

            // Test 1: Crop tracking (Farming category)
            {
                var bin = Game1.getFarm().getShippingBin(Game1.player);
                bin.Clear();

                // Parsnip (ID 24) = vegetable = Farming
                var parsnip = ItemRegistry.Create("(O)24", 5);
                bin.Add(parsnip);

                // Snapshot money, then process
                _tracker.OnDayStarted();
                _tracker.OnDayEnding();

                var records = _repo.GetDailyRecords(Game1.currentSeason, Game1.year, Game1.player.UniqueMultiplayerID.ToString());
                var today = records.FirstOrDefault(r => r.Day == Game1.dayOfMonth);

                if (today != null && today.FarmingIncome > 0)
                {
                    this.Monitor.Log($"  PASS: Farming income = {today.FarmingIncome}g (5 parsnips)", LogLevel.Info);
                    passed++;
                }
                else
                {
                    this.Monitor.Log($"  FAIL: Expected farming income > 0, got {today?.FarmingIncome ?? 0}", LogLevel.Error);
                    failed++;
                }

                bin.Clear();
            }

            // Test 2: Fish tracking (Fishing category)
            {
                var bin = Game1.getFarm().getShippingBin(Game1.player);

                // Largemouth Bass (ID 136) = fish = Fishing
                var fish = ItemRegistry.Create("(O)136", 2);
                bin.Add(fish);

                _tracker.OnDayStarted();
                _tracker.OnDayEnding();

                var records = _repo.GetDailyRecords(Game1.currentSeason, Game1.year, Game1.player.UniqueMultiplayerID.ToString());
                var today = records.FirstOrDefault(r => r.Day == Game1.dayOfMonth);

                if (today != null && today.FishingIncome > 0)
                {
                    this.Monitor.Log($"  PASS: Fishing income = {today.FishingIncome}g (2 bass)", LogLevel.Info);
                    passed++;
                }
                else
                {
                    this.Monitor.Log($"  FAIL: Expected fishing income > 0, got {today?.FishingIncome ?? 0}", LogLevel.Error);
                    failed++;
                }

                bin.Clear();
            }

            // Test 3: Gem tracking (Mining category)
            {
                var bin = Game1.getFarm().getShippingBin(Game1.player);

                // Diamond (ID 72) = gem = Mining
                var diamond = ItemRegistry.Create("(O)72", 1);
                bin.Add(diamond);

                _tracker.OnDayStarted();
                _tracker.OnDayEnding();

                var records = _repo.GetDailyRecords(Game1.currentSeason, Game1.year, Game1.player.UniqueMultiplayerID.ToString());
                var today = records.FirstOrDefault(r => r.Day == Game1.dayOfMonth);

                if (today != null && today.MiningIncome > 0)
                {
                    this.Monitor.Log($"  PASS: Mining income = {today.MiningIncome}g (1 diamond)", LogLevel.Info);
                    passed++;
                }
                else
                {
                    this.Monitor.Log($"  FAIL: Expected mining income > 0, got {today?.MiningIncome ?? 0}", LogLevel.Error);
                    failed++;
                }

                bin.Clear();
            }

            // Test 4: Legendary fish detection
            {
                var bin = Game1.getFarm().getShippingBin(Game1.player);

                // Legend (ID 163) = legendary fish
                var legend = ItemRegistry.Create("(O)163", 1);
                bin.Add(legend);

                _tracker.OnDayStarted();
                _tracker.OnDayEnding();

                var legendaryFish = _repo.GetLegendaryFish(Game1.player.UniqueMultiplayerID.ToString());
                if (legendaryFish.Any(f => f.FishId == "163"))
                {
                    this.Monitor.Log($"  PASS: Legendary fish 'Legend' detected and recorded", LogLevel.Info);
                    passed++;
                }
                else
                {
                    this.Monitor.Log("  FAIL: Legendary fish not recorded", LogLevel.Error);
                    failed++;
                }

                bin.Clear();
            }

            // Test 5: Expense tracking
            {
                _tracker.OnDayStarted();
                int moneyBefore = Game1.player.Money;
                Game1.player.Money -= 500; // simulate a purchase
                _tracker.OnDayEnding();

                var records = _repo.GetDailyRecords(Game1.currentSeason, Game1.year, Game1.player.UniqueMultiplayerID.ToString());
                var today = records.FirstOrDefault(r => r.Day == Game1.dayOfMonth);

                if (today != null && today.TotalExpenses >= 500)
                {
                    this.Monitor.Log($"  PASS: Expenses = {today.TotalExpenses}g (simulated 500g purchase)", LogLevel.Info);
                    passed++;
                }
                else
                {
                    this.Monitor.Log($"  FAIL: Expected expenses >= 500, got {today?.TotalExpenses ?? 0}", LogLevel.Error);
                    failed++;
                }

                // Restore money
                Game1.player.Money = moneyBefore;
            }

            // Test 6: Category classifier
            {
                var parsnip = ItemRegistry.Create("(O)24");  // Vegetable
                var bass = ItemRegistry.Create("(O)136");    // Fish
                var diamond = ItemRegistry.Create("(O)72");  // Gem
                var leek = ItemRegistry.Create("(O)20");     // Forage

                bool allCorrect = true;
                string cat;

                cat = CategoryClassifier.Classify(parsnip);
                if (cat != "Farming") { this.Monitor.Log($"  FAIL: Parsnip classified as {cat}, expected Farming", LogLevel.Error); allCorrect = false; }

                cat = CategoryClassifier.Classify(bass);
                if (cat != "Fishing") { this.Monitor.Log($"  FAIL: Bass classified as {cat}, expected Fishing", LogLevel.Error); allCorrect = false; }

                cat = CategoryClassifier.Classify(diamond);
                if (cat != "Mining") { this.Monitor.Log($"  FAIL: Diamond classified as {cat}, expected Mining", LogLevel.Error); allCorrect = false; }

                cat = CategoryClassifier.Classify(leek);
                if (cat != "Foraging") { this.Monitor.Log($"  FAIL: Leek classified as {cat}, expected Foraging", LogLevel.Error); allCorrect = false; }

                if (allCorrect)
                {
                    this.Monitor.Log("  PASS: All items correctly classified (Farming/Fishing/Mining/Foraging)", LogLevel.Info);
                    passed++;
                }
                else
                {
                    failed++;
                }
            }

            this.Monitor.Log($"=== Results: {passed} passed, {failed} failed ===", LogLevel.Info);
        }

        /// <summary>
        /// Show current database statistics.
        /// Usage: type "lacompta_status" in SMAPI console.
        /// </summary>
        private void ShowStatus(string command, string[] args)
        {
            if (_repo == null)
            {
                this.Monitor.Log("LaCompta not initialized. Load a save first!", LogLevel.Warn);
                return;
            }

            var playerId = Context.IsWorldReady ? Game1.player.UniqueMultiplayerID.ToString() : "";
            var summaries = _repo.GetAllSeasonSummaries(playerId);
            var legendaryFish = _repo.GetLegendaryFish(playerId);

            this.Monitor.Log("=== LaCompta Status ===", LogLevel.Info);
            this.Monitor.Log($"  Seasons tracked: {summaries.Count}", LogLevel.Info);
            this.Monitor.Log($"  Legendary fish sold: {legendaryFish.Count}", LogLevel.Info);

            foreach (var s in summaries)
            {
                var total = s.FarmingTotal + s.ForagingTotal + s.FishingTotal + s.MiningTotal + s.OtherTotal;
                this.Monitor.Log($"  {s.Season} Y{s.Year}: {total}g income, {s.TotalExpenses}g expenses, best day: {s.BestDay} ({s.BestDayIncome}g)", LogLevel.Info);
            }
        }
    }
}
