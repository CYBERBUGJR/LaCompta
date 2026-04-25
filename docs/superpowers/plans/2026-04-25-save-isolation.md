# Save Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give each Stardew save its own SQLite database file so that loading multiple saves no longer mixes their financial records, and ship the change as v0.1.3 alongside a dynamic dashboard version footer.

**Architecture:** One DB file per save under `Mods/LaCompta/data/<SaveFolderName>.db`. `DatabaseContext` is constructed against a full file path (built from `Constants.SaveFolderName` at `SaveLoaded`) and implements `IDisposable` to release SQLite's connection pool when the save is unloaded. A new `SaveCleanupService` deletes the legacy `lacompta.db` (one-shot) and prunes orphan DB files at `GameLaunched` and `ReturnedToTitle`.

**Tech Stack:** C# .NET 6, SMAPI 4.x events, `Microsoft.Data.Sqlite`, ModBuildConfig (auto-deploy + release zip).

**Spec reference:** `docs/superpowers/specs/2026-04-25-save-isolation-design.md`

**Test infrastructure note:** This project has no automated test runner; the only existing test is the in-game `lacompta_test` console command. Verification steps are therefore "build to confirm compile" and a final manual smoke-test in Stardew. TDD steps are not included because there is no harness to run them in.

---

## Task 1: Create `SaveCleanupService`

This task is first because it's purely additive (new file, no existing code touched), so it leaves the build green.

**Files:**
- Create: `LaCompta/Services/SaveCleanupService.cs`

- [ ] **Step 1: Create the new file with both cleanup methods**

Write this file at `LaCompta/Services/SaveCleanupService.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;

namespace LaCompta.Services
{
    /// <summary>
    /// Removes legacy single-DB files and prunes per-save DBs whose Stardew save
    /// has been deleted. Stardew has no save-deletion event, so we reconcile at
    /// every GameLaunched and ReturnedToTitle.
    /// </summary>
    public class SaveCleanupService
    {
        private readonly string _modPath;
        private readonly IMonitor _monitor;

        public SaveCleanupService(string modPath, IMonitor monitor)
        {
            _modPath = modPath;
            _monitor = monitor;
        }

        /// <summary>
        /// Delete the legacy <c>Mods/LaCompta/lacompta.db</c> from the v0.1.2 era.
        /// One-shot per launch — once gone, this is a no-op. Mixed-save data in
        /// that file is unrecoverable, so users get a clean slate per the spec.
        /// </summary>
        public void CleanupLegacyDatabase()
        {
            var legacyPath = Path.Combine(_modPath, "lacompta.db");
            if (!File.Exists(legacyPath))
                return;

            try
            {
                File.Delete(legacyPath);
                _monitor.Log("Removed legacy lacompta.db (pre-0.1.3 mono-save layout).", LogLevel.Info);
            }
            catch (IOException ex)
            {
                _monitor.Log($"Could not remove legacy lacompta.db: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>
        /// Delete any <c>data/*.db</c> whose save folder no longer exists under
        /// <see cref="Constants.SavesPath"/>. Safe to call repeatedly.
        /// </summary>
        public void PruneOrphanDatabases()
        {
            var dataDir = Path.Combine(_modPath, "data");
            if (!Directory.Exists(dataDir))
                return;

            HashSet<string> liveSaves;
            try
            {
                liveSaves = Directory.EnumerateDirectories(Constants.SavesPath)
                                     .Select(Path.GetFileName)
                                     .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            }
            catch (System.Exception ex)
            {
                _monitor.Log($"Could not enumerate Stardew saves at '{Constants.SavesPath}': {ex.Message}. Skipping prune.", LogLevel.Warn);
                return;
            }

            foreach (var dbPath in Directory.EnumerateFiles(dataDir, "*.db"))
            {
                var saveName = Path.GetFileNameWithoutExtension(dbPath);
                if (liveSaves.Contains(saveName))
                    continue;

                try
                {
                    File.Delete(dbPath);
                    _monitor.Log($"Pruned orphan DB for deleted save '{saveName}'.", LogLevel.Info);
                }
                catch (IOException ex)
                {
                    _monitor.Log($"Could not prune orphan DB '{saveName}': {ex.Message}", LogLevel.Warn);
                }
            }
        }
    }
}
```

- [ ] **Step 2: Build to confirm compile**

Run: `dotnet build LaCompta/LaCompta.csproj --configuration Release`
Expected: `La génération a réussi.` (Build succeeded), 0 errors.

- [ ] **Step 3: Commit**

```bash
git add LaCompta/Services/SaveCleanupService.cs
git commit -m "feat(save-isolation): add SaveCleanupService for legacy + orphan pruning

Pure additive change. CleanupLegacyDatabase() removes the v0.1.2 mono-save
lacompta.db one-shot. PruneOrphanDatabases() compares files in data/ to
Stardew's Saves folder and deletes any DB whose save no longer exists.

Not yet wired into ModEntry; subsequent commit will switch DatabaseContext
to per-save paths and call this service from GameLaunched / ReturnedToTitle.
"
```

---

## Task 2: Make `DatabaseContext` save-aware and disposable, and wire it into `ModEntry`

These two changes are coupled — `DatabaseContext`'s constructor signature changes, so `ModEntry`'s call site must change in the same commit or the build breaks.

**Files:**
- Modify: `LaCompta/Data/DatabaseContext.cs` (constructor signature + new `Dispose`)
- Modify: `LaCompta/ModEntry.cs` (path construction in `OnSaveLoaded`, dispose in `OnReturnedToTitle`, hook `GameLaunched` for cleanup, add reconciliation to `OnReturnedToTitle`)

- [ ] **Step 1: Replace the body of `DatabaseContext.cs`**

Overwrite `LaCompta/Data/DatabaseContext.cs` with this content. Schema and `CheckAndVacuum` are unchanged; only the constructor signature, `_dbPath` source, and the new `Dispose` differ.

```csharp
using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace LaCompta.Data
{
    public class DatabaseContext : IDisposable
    {
        private const long MaxDbSizeBytes = 2L * 1024 * 1024 * 1024; // 2 GiB
        private readonly string _connectionString;
        private readonly string _dbPath;
        private bool _disposed;

        /// <param name="dbFilePath">Full path to the .db file (caller is responsible for path construction).</param>
        public DatabaseContext(string dbFilePath)
        {
            _dbPath = dbFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            _connectionString = $"Data Source={_dbPath}";
            InitializeDatabase();
            CheckAndVacuum();
        }

        public SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Microsoft.Data.Sqlite pools connections by connection string; on Windows the
        /// pooled connection holds a file handle even after Close(). ClearAllPools()
        /// releases that handle so the file can be deleted by the cleanup service.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SqliteConnection.ClearAllPools();
            GC.SuppressFinalize(this);
        }

        private void InitializeDatabase()
        {
            using var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS daily_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    season TEXT NOT NULL,
                    year INTEGER NOT NULL,
                    day INTEGER NOT NULL,
                    farming_income INTEGER NOT NULL DEFAULT 0,
                    foraging_income INTEGER NOT NULL DEFAULT 0,
                    fishing_income INTEGER NOT NULL DEFAULT 0,
                    mining_income INTEGER NOT NULL DEFAULT 0,
                    other_income INTEGER NOT NULL DEFAULT 0,
                    total_expenses INTEGER NOT NULL DEFAULT 0,
                    player_id TEXT NOT NULL DEFAULT '',
                    UNIQUE(season, year, day, player_id)
                );

                CREATE TABLE IF NOT EXISTS item_transactions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    daily_record_id INTEGER NOT NULL,
                    item_name TEXT NOT NULL,
                    item_id TEXT NOT NULL,
                    category TEXT NOT NULL,
                    quantity INTEGER NOT NULL,
                    unit_price INTEGER NOT NULL,
                    total_price INTEGER NOT NULL,
                    cost_basis INTEGER NOT NULL DEFAULT 0,
                    season TEXT NOT NULL,
                    year INTEGER NOT NULL,
                    day INTEGER NOT NULL,
                    player_id TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY (daily_record_id) REFERENCES daily_records(id)
                );

                CREATE TABLE IF NOT EXISTS season_summaries (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    season TEXT NOT NULL,
                    year INTEGER NOT NULL,
                    farming_total INTEGER NOT NULL DEFAULT 0,
                    foraging_total INTEGER NOT NULL DEFAULT 0,
                    fishing_total INTEGER NOT NULL DEFAULT 0,
                    mining_total INTEGER NOT NULL DEFAULT 0,
                    other_total INTEGER NOT NULL DEFAULT 0,
                    total_expenses INTEGER NOT NULL DEFAULT 0,
                    best_day INTEGER NOT NULL DEFAULT 0,
                    best_day_income INTEGER NOT NULL DEFAULT 0,
                    player_id TEXT NOT NULL DEFAULT '',
                    UNIQUE(season, year, player_id)
                );

                CREATE TABLE IF NOT EXISTS fish_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    fish_name TEXT NOT NULL,
                    fish_id TEXT NOT NULL,
                    is_legendary INTEGER NOT NULL DEFAULT 0,
                    quantity INTEGER NOT NULL DEFAULT 1,
                    total_revenue INTEGER NOT NULL DEFAULT 0,
                    season TEXT NOT NULL,
                    year INTEGER NOT NULL,
                    day INTEGER NOT NULL,
                    player_id TEXT NOT NULL DEFAULT '',
                    UNIQUE(fish_id, season, year, day, player_id)
                );

                CREATE INDEX IF NOT EXISTS idx_daily_records_season_year ON daily_records(season, year, player_id);
                CREATE INDEX IF NOT EXISTS idx_item_transactions_daily ON item_transactions(daily_record_id);
                CREATE INDEX IF NOT EXISTS idx_fish_records_legendary ON fish_records(is_legendary);
            ";
            command.ExecuteNonQuery();
        }

        private void CheckAndVacuum()
        {
            if (!File.Exists(_dbPath))
                return;

            var fileSize = new FileInfo(_dbPath).Length;
            if (fileSize < MaxDbSizeBytes)
                return;

            using var conn = GetConnection();

            var pruneCmd = conn.CreateCommand();
            pruneCmd.CommandText = @"
                DELETE FROM item_transactions
                WHERE daily_record_id IN (
                    SELECT dr.id FROM daily_records dr
                    WHERE dr.year < (SELECT MAX(year) - 4 FROM daily_records)
                );

                DELETE FROM fish_records
                WHERE year < (SELECT MAX(year) - 4 FROM fish_records);
            ";
            pruneCmd.ExecuteNonQuery();

            var vacuumCmd = conn.CreateCommand();
            vacuumCmd.CommandText = "VACUUM;";
            vacuumCmd.ExecuteNonQuery();
        }
    }
}
```

- [ ] **Step 2: Add the cleanup-service field and the GameLaunched hook in `ModEntry`**

In `LaCompta/ModEntry.cs`, find the field declarations near the top of the class (around line 18-24) and add a `_saveCleanup` field. Then add a new event subscription and a new handler.

Find this block:

```csharp
        private ModConfig Config;
        private DatabaseContext _db = null!;
        private Repository _repo = null!;
        private readonly PerScreen<TrackingService> _tracker = new();
        private readonly PerScreen<SeasonSummaryService> _seasonSummary = new();
        private WebServer _webServer = null!;
        private ExcelExportService _excelService;
```

Replace with:

```csharp
        private ModConfig Config;
        private DatabaseContext _db = null!;
        private Repository _repo = null!;
        private readonly PerScreen<TrackingService> _tracker = new();
        private readonly PerScreen<SeasonSummaryService> _seasonSummary = new();
        private WebServer _webServer = null!;
        private ExcelExportService _excelService;
        private SaveCleanupService _saveCleanup = null!;
```

Then find the `Entry` method's event subscriptions:

```csharp
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
```

These already include `GameLaunched`. We'll extend the existing `OnGameLaunched` handler in the next step rather than adding a new subscription.

- [ ] **Step 3: Initialize `_saveCleanup` and run legacy + orphan cleanup at `GameLaunched`**

In `LaCompta/ModEntry.cs`, find `OnGameLaunched` (currently starts around line 45). Insert the cleanup service initialization and invocations at the *top* of the method, before the GMCM block. The current method:

```csharp
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
            {
                this.Monitor.Log("GMCM not installed -- config via config.json only.", LogLevel.Debug);
                return;
            }
```

Becomes:

```csharp
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            _saveCleanup = new SaveCleanupService(this.Helper.DirectoryPath, this.Monitor);
            _saveCleanup.CleanupLegacyDatabase();
            _saveCleanup.PruneOrphanDatabases();

            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null)
            {
                this.Monitor.Log("GMCM not installed -- config via config.json only.", LogLevel.Debug);
                return;
            }
```

- [ ] **Step 4: Rewrite `OnSaveLoaded` to use a per-save DB path**

In `LaCompta/ModEntry.cs`, find `OnSaveLoaded` (currently around line 90). The current method:

```csharp
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // Initialize database (shared across screens)
            var dataPath = this.Helper.DirectoryPath;
            _db = new DatabaseContext(dataPath);
            _repo = new Repository(_db);
```

Replace those four lines (the comment, the `dataPath` var, and the two `new` lines) with:

```csharp
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // One DB per Stardew save. SMAPI guarantees Constants.SaveFolderName
            // is valid by SaveLoaded. SQLite creates the file if absent.
            var dbPath = System.IO.Path.Combine(
                this.Helper.DirectoryPath,
                "data",
                $"{Constants.SaveFolderName}.db"
            );
            _db = new DatabaseContext(dbPath);
            _repo = new Repository(_db);
```

Leave the rest of `OnSaveLoaded` (per-screen services, web server start, log, auto-open) unchanged.

- [ ] **Step 5: Update `OnReturnedToTitle` to dispose the DB and reconcile orphans**

In `LaCompta/ModEntry.cs`, find the current `OnReturnedToTitle`:

```csharp
        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            _webServer?.Stop();
        }
```

Replace with:

```csharp
        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            _webServer?.Stop();

            // Release the SQLite pool so PruneOrphanDatabases can delete files.
            _db?.Dispose();
            _db = null!;
            _repo = null!;

            _saveCleanup?.PruneOrphanDatabases();
        }
```

- [ ] **Step 6: Build to confirm compile**

Run: `dotnet build LaCompta/LaCompta.csproj --configuration Release`
Expected: 0 errors. The build also auto-deploys to `Mods/LaCompta/`.

If you see `error CS0103: The name 'Constants' does not exist`, add `using StardewModdingAPI;` at the top of `ModEntry.cs` — but it's already there in the existing imports, so this should not happen.

- [ ] **Step 7: Commit**

```bash
git add LaCompta/Data/DatabaseContext.cs LaCompta/ModEntry.cs
git commit -m "feat(save-isolation): per-save DB at data/<SaveFolderName>.db

DatabaseContext now takes the full DB file path and implements IDisposable,
clearing SqliteConnection's pool on dispose so the cleanup service can
delete files Windows would otherwise hold open via the pooled handle.

ModEntry constructs the path as data/<Constants.SaveFolderName>.db at
SaveLoaded, disposes the context at ReturnedToTitle, and runs legacy +
orphan cleanup via SaveCleanupService at GameLaunched. ReturnedToTitle
also runs orphan reconciliation to catch saves deleted from the title
menu mid-session.

Existing schema, queries, Repository, and per-screen split-screen
services are unchanged. The player_id column continues to isolate
split-screen players within a single save's DB.
"
```

---

## Task 3: Wire the dynamic version footer

Currently `dashboard.html` line 185 hardcodes `LaCompta v0.1.0`. Make the dashboard read the manifest version on every page load.

**Files:**
- Modify: `LaCompta/Web/Assets/dashboard.html` (line 185 only)
- Modify: `LaCompta/Web/ApiController.cs` (constructor + `ServeHomePage`)
- Modify: `LaCompta/ModEntry.cs` (the call site that constructs `ApiController`)

- [ ] **Step 1: Replace the hardcoded version in `dashboard.html`**

In `LaCompta/Web/Assets/dashboard.html`, find line 185:

```html
      LaCompta v0.1.0 &mdash; <a href="https://github.com/CYBERBUGJR/LaCompta" target="_blank">GitHub</a>
```

Replace with:

```html
      LaCompta v{{VERSION}} &mdash; <a href="https://github.com/CYBERBUGJR/LaCompta" target="_blank">GitHub</a>
```

- [ ] **Step 2: Add the `_version` field and constructor parameter to `ApiController`**

In `LaCompta/Web/ApiController.cs`, find the constructor (around line 30):

```csharp
        private readonly Repository _repo;
        private readonly IMonitor _monitor;
        private readonly string _assetsPath;
        private ExcelExportService _excelService;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public ApiController(Repository repo, IMonitor monitor, string modPath)
        {
            _repo = repo;
            _monitor = monitor;
            _assetsPath = Path.Combine(modPath, "Assets");
            _excelService = new ExcelExportService(repo, monitor);
        }
```

Replace with:

```csharp
        private readonly Repository _repo;
        private readonly IMonitor _monitor;
        private readonly string _assetsPath;
        private readonly string _version;
        private ExcelExportService _excelService;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public ApiController(Repository repo, IMonitor monitor, string modPath, string version)
        {
            _repo = repo;
            _monitor = monitor;
            _assetsPath = Path.Combine(modPath, "Assets");
            _version = version;
            _excelService = new ExcelExportService(repo, monitor);
        }
```

- [ ] **Step 3: Substitute `{{VERSION}}` in `ServeHomePage`**

Still in `LaCompta/Web/ApiController.cs`, find `ServeHomePage` (around line 326):

```csharp
        private void ServeHomePage(HttpListenerResponse response)
        {
            var dashboardPath = Path.Combine(_assetsPath, "dashboard.html");
            if (File.Exists(dashboardPath))
            {
                ServeHtml(response, File.ReadAllText(dashboardPath));
            }
            else
            {
                ServeHtml(response, "<html><body><h1>LaCompta</h1><p>Dashboard file not found. Check Assets/dashboard.html</p></body></html>");
                _monitor.Log("Dashboard HTML not found at: " + dashboardPath, LogLevel.Warn);
            }
        }
```

Replace with:

```csharp
        private void ServeHomePage(HttpListenerResponse response)
        {
            var dashboardPath = Path.Combine(_assetsPath, "dashboard.html");
            if (File.Exists(dashboardPath))
            {
                var html = File.ReadAllText(dashboardPath).Replace("{{VERSION}}", _version);
                ServeHtml(response, html);
            }
            else
            {
                ServeHtml(response, "<html><body><h1>LaCompta</h1><p>Dashboard file not found. Check Assets/dashboard.html</p></body></html>");
                _monitor.Log("Dashboard HTML not found at: " + dashboardPath, LogLevel.Warn);
            }
        }
```

- [ ] **Step 4: Pass the manifest version from `ModEntry`**

In `LaCompta/ModEntry.cs`, find the line in `OnSaveLoaded` that constructs `ApiController` (currently around line 107):

```csharp
            var api = new ApiController(_repo, this.Monitor, this.Helper.DirectoryPath);
```

Replace with:

```csharp
            var api = new ApiController(_repo, this.Monitor, this.Helper.DirectoryPath, this.ModManifest.Version.ToString());
```

- [ ] **Step 5: Build to confirm compile**

Run: `dotnet build LaCompta/LaCompta.csproj --configuration Release`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add LaCompta/Web/Assets/dashboard.html LaCompta/Web/ApiController.cs LaCompta/ModEntry.cs
git commit -m "feat(dashboard): inject manifest version into footer

The footer text was hardcoded to v0.1.0 in dashboard.html and never
updated through 0.1.1 / 0.1.2. Replace with a {{VERSION}} placeholder
that ApiController.ServeHomePage substitutes at request time, sourced
from this.ModManifest.Version. Future bumps update the dashboard for
free.
"
```

---

## Task 4: Bump manifest to 0.1.3

**Files:**
- Modify: `LaCompta/manifest.json`

- [ ] **Step 1: Edit `LaCompta/manifest.json`**

Change the `Version` field from `"0.1.2"` to `"0.1.3"`. The file should read:

```json
{
    "Name": "LaCompta",
    "Author": "bcalvet",
    "Version": "0.1.3",
    "Description": "Farm economics tracker — track income, expenses, and profitability across seasons. La Compta, because every gold coin counts!",
    "UniqueID": "bcalvet.LaCompta",
    "EntryDll": "LaCompta.dll",
    "MinimumApiVersion": "4.0.0",
    "UpdateKeys": []
}
```

- [ ] **Step 2: Build to produce the v0.1.3 release zip**

Run: `dotnet build LaCompta/LaCompta.csproj --configuration Release`
Expected: 0 errors. ModBuildConfig produces `LaCompta/bin/Release/net6.0/LaCompta 0.1.3.zip` and auto-deploys to `Mods/LaCompta/`.

- [ ] **Step 3: Commit**

```bash
git add LaCompta/manifest.json
git commit -m "release: v0.1.3 (per-save DB isolation, dynamic version footer)

Closes the cross-save data leak users hit in v0.1.2 when loading two
different saves. Each Stardew save now has its own DB at
Mods/LaCompta/data/<SaveFolderName>.db.

Breaking: the legacy Mods/LaCompta/lacompta.db is deleted on first
launch. Mixed-save data in that file is unrecoverable. Single-save
users with valuable history should back up before upgrading.
"
```

---

## Task 5: Manual smoke test in-game

Cannot be automated — run by the maintainer. The protocol is the one in the spec, repeated here for convenience. Do NOT proceed to release until every step passes.

- [ ] **Step 1: Launch Stardew via SMAPI and confirm clean LaCompta load**

Look for `[INFO LaCompta] LaCompta loaded bitch ! Time to count those coins!` in the SMAPI console with no `Could not load` / `DllNotFoundException` / `Skipped LaCompta` lines.

Look for `[INFO LaCompta] Removed legacy lacompta.db (pre-0.1.3 mono-save layout).` — this fires once on the first 0.1.3 launch because of the leftover `lacompta.db` from 0.1.2.

- [ ] **Step 2: Load Save A (existing test farm), play one day, confirm data**

Open `http://localhost:5555/`. After day-end, dashboard should show this day's income for Save A. Footer should read `LaCompta v0.1.3 — GitHub`.

Confirm `Mods/LaCompta/data/<SaveAFolderName>.db` exists.

- [ ] **Step 3: Return to title, load Save B (different farm), confirm isolation**

Save B's dashboard should show ONLY Save B's data. Net profit should not include Save A's totals.

Confirm `Mods/LaCompta/data/` now contains two `.db` files matching the two save folder names under `%AppData%\StardewValley\Saves\`.

- [ ] **Step 4: Return to title, delete Save B via the Stardew title-menu UI, then load Save A**

When loading Save A, watch the SMAPI log for `[INFO LaCompta] Pruned orphan DB for deleted save '<SaveBFolderName>'.` — this confirms the `ReturnedToTitle` reconciliation removed Save B's DB after the deletion.

Confirm `Mods/LaCompta/data/` now contains only Save A's DB.

- [ ] **Step 5: Quit Stardew, relaunch, confirm `GameLaunched` reconciliation is a no-op (no orphans this run)**

No `Pruned orphan DB` log line should appear. Save A's DB still present.

If any step fails, stop and diagnose before continuing to release.

---

## Task 6: Release v0.1.3

**Files:** None (git/release operations).

- [ ] **Step 1: Push the commits to `main`**

```bash
git push origin main
```

Expected: Build CI workflow runs and passes (no SonarCloud anymore).

- [ ] **Step 2: Tag v0.1.3 and push the tag**

```bash
git tag -a v0.1.3 -m "Release v0.1.3"
git push origin v0.1.3
```

Pushing the tag triggers `.github/workflows/release.yml` which creates the release shell.

- [ ] **Step 3: Wait for the Release workflow to finish**

```bash
gh run list --workflow=release.yml --limit=1
gh run watch <run-id-from-above> --exit-status
```

Expected: `release-shell` job succeeds in ~10s.

- [ ] **Step 4: Upload the release zip**

```bash
cp "LaCompta/bin/Release/net6.0/LaCompta 0.1.3.zip" "LaCompta-0.1.3.zip"
gh release upload v0.1.3 "LaCompta-0.1.3.zip"
rm -f "LaCompta-0.1.3.zip"
```

- [ ] **Step 5: Verify the published release**

```bash
gh release view v0.1.3 --json tagName,assets,url
```

Expected: `tagName: v0.1.3`, one asset `LaCompta-0.1.3.zip`, `assets[0].state: uploaded`.

- [ ] **Step 6: Hand back to user for the Discord announcement**

The Discord message is humorous in tone (per established pattern). Suggested headline: "🪙 LaCompta v0.1.3 — chacun sa compta". Highlight: per-save isolation (no more mixed numbers between farms), dynamic version footer, legacy `lacompta.db` is auto-cleaned. Note the breaking change for users with single-save history.

---

## Self-review notes

- **Spec coverage:** Storage layout (Task 2 step 4), DB lifecycle (Task 2 steps 4–5), legacy cleanup (Task 1 + Task 2 step 3), orphan reconciliation at GameLaunched + ReturnedToTitle (Task 2 steps 3, 5), SQLite pool clear (Task 2 step 1, `Dispose`), bundled version footer (Task 3), bundled version bump (Task 4), error handling (logged Warn paths in Task 1 step 1), manual testing (Task 5). All sections of the spec map to tasks.
- **Type/method consistency:** `SaveCleanupService.CleanupLegacyDatabase()` and `PruneOrphanDatabases()` declared in Task 1 are the exact names called from `OnGameLaunched` (Task 2 step 3) and `OnReturnedToTitle` (Task 2 step 5). `DatabaseContext.Dispose()` declared in Task 2 step 1 is the exact name called in Task 2 step 5. `ApiController` constructor signature declared in Task 3 step 2 (`Repository, IMonitor, string, string`) matches the call site in Task 3 step 4.
- **No placeholders:** Each code-changing step shows the actual code; each command step shows the exact command and expected output.
