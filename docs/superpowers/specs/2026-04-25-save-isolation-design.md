# Save Isolation — Design

**Date:** 2026-04-25
**Target release:** v0.1.3
**Status:** Approved (pending implementation)

## Problem

LaCompta currently writes all data to a single `lacompta.db` file in the mod folder. The schema has a `player_id` column for split-screen multiplayer isolation within a save, but no notion of which Stardew save the data belongs to.

When a player loads multiple saves, the records merge into the same tables. Symptoms observed in v0.1.2:

- Loaded a Spring Y1 D9 save (test farm), then a fresh Spring Y1 D1 save (Ben's farm).
- Net profit and expenses on the dashboard reflected the union of both saves — negative net because expenses from one save bled into the other's income totals.

There is no way for the user to keep the financials of two parallel playthroughs separate.

## Goals

1. Each Stardew save has its own SQLite database. Loading save A only ever reads/writes A's DB.
2. Creating a new Stardew save automatically creates a new DB on first load — no manual setup.
3. Deleting a Stardew save (via the title menu's delete-save UI) removes the corresponding DB. Mirrored lifecycle.
4. Existing v0.1.2 users with mixed data in `lacompta.db` get a clean slate on upgrade — the legacy file is removed.
5. Split-screen multiplayer behaviour within a save is unchanged (`player_id` keeps doing its job).

## Non-goals

- Migrating mixed legacy data into a specific save's DB. The data is already corrupted (rows from multiple saves co-mingled with no way to disambiguate). Drop and start fresh.
- Cross-save reporting or aggregated dashboards. One dashboard reflects the currently loaded save.
- Manual recovery UI for accidentally-deleted saves' DBs. If the Stardew save is gone, our DB goes too.
- Cloud sync, backups, or anything external.

## Design

### Storage layout

```
Mods/LaCompta/
  data/
    <SaveFolderName>.db        # one per save, e.g. BenLaCompta_148302148.db
    <OtherSaveFolderName>.db
  LaCompta.dll
  manifest.json
  ...
```

The DB filename is `Constants.SaveFolderName` from SMAPI — the same unique identifier Stardew uses for its own save folders under `%AppData%\StardewValley\Saves\`. It is unique per save, stable across loads of the same save, and safe for filesystem use (no need for sanitisation).

The `data/` subfolder keeps DBs separate from the mod's other files (DLLs, assets) so users can `rm -rf data/` if they want to wipe everything.

### Database lifecycle

| Event                | Action                                                                                          |
|----------------------|-------------------------------------------------------------------------------------------------|
| `GameLaunched`       | Run legacy cleanup + orphan reconciliation (see below).                                         |
| `SaveLoaded`         | Build `Path.Combine(modPath, "data", $"{Constants.SaveFolderName}.db")`. SQLite creates the file if it doesn't exist. Initialise `DatabaseContext` and dependent services on this path. |
| `DayEnding` / others | Existing code path. Repository writes to the active DB. No changes.                             |
| `ReturnedToTitle`    | Stop the web server (already done). Dispose the active DB context (which clears its SQLite connection pool — see below). Run orphan reconciliation. |

The schema does not change. No new columns. No requirement to filter every query by save — isolation is at the file level.

### SQLite connection pool

`Microsoft.Data.Sqlite` pools connections by connection string. On Windows, a pooled connection holds a file handle on the DB file even after `connection.Close()` returns — so `File.Delete` against a recently-used DB throws `IOException`.

`DatabaseContext` will implement `IDisposable`. `Dispose()` calls `SqliteConnection.ClearPool(_canonicalConnection)` (or `SqliteConnection.ClearAllPools()` as a coarser hammer) to release the handle before the pruner runs.

Without this, the following scenario would leak a file:

1. Load save A → DB context for A opens connections, pool warms.
2. Return to title → context disposed, but pool still warm for A.
3. User deletes save A in the title-menu UI.
4. User loads save B; on later `ReturnedToTitle`, reconciliation tries to prune A's DB → `File.Delete` fails with handle-still-held → orphan persists until process exit.

Disposing the context at step 2 (and clearing the pool there) makes step 4 succeed.

### Legacy `lacompta.db` cleanup

At `GameLaunched`, before reconciliation: if `Path.Combine(modPath, "lacompta.db")` exists, delete it and log at Info level. One-shot per launch — once deleted, this branch becomes a no-op.

If deletion fails (file locked, permission denied), log a Warn and continue. The mod functions normally; the orphan file just sits there until the user removes it manually.

### Orphan reconciliation

A new `SaveCleanupService` exposes one method:

```csharp
public void PruneOrphanDatabases()
{
    var dataDir = Path.Combine(_modPath, "data");
    if (!Directory.Exists(dataDir)) return;

    var liveSaves = Directory.EnumerateDirectories(Constants.SavesPath)
                              .Select(Path.GetFileName)
                              .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var dbPath in Directory.EnumerateFiles(dataDir, "*.db"))
    {
        var saveName = Path.GetFileNameWithoutExtension(dbPath);
        if (liveSaves.Contains(saveName)) continue;

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
```

Called from:

- `GameLaunched` — catches saves deleted while Stardew was closed.
- `ReturnedToTitle` — catches saves deleted from the title menu without quitting Stardew.

The active save's DB cannot be orphaned at `ReturnedToTitle` because we've already disposed the context for it (its file remains in `liveSaves` since the save folder still exists — only deleting the save through the title menu removes the folder).

### Component changes

| File                                           | Change                                                                                                          |
|------------------------------------------------|-----------------------------------------------------------------------------------------------------------------|
| `LaCompta/Data/DatabaseContext.cs`             | Constructor takes the full DB file path instead of a folder. Class implements `IDisposable`; `Dispose()` clears the SQLite connection pool for its connection string. |
| `LaCompta/Services/SaveCleanupService.cs`      | New file. `PruneOrphanDatabases()` and `CleanupLegacyDatabase()` methods.                                       |
| `LaCompta/ModEntry.cs`                         | Wire `GameLaunched` → cleanup. Wire `ReturnedToTitle` → reconciliation. Update `SaveLoaded` to build save-specific path. Dispose DB on title return. |

No changes to `Repository`, `TrackingService`, `SeasonSummaryService`, `ApiController`, `WebServer`, or any frontend asset *except* the version-footer fix below.

### Bundled fixes (same release)

These are unrelated to save isolation but ship in v0.1.3 alongside it:

1. **Dashboard version footer is dynamic.** `dashboard.html` line 185 currently reads `LaCompta v0.1.0` hardcoded. Replace with `LaCompta v{{VERSION}}`. `ApiController` constructor takes a `version` string (passed `this.ModManifest.Version.ToString()` from `ModEntry`); `ServeHomePage` does `html.Replace("{{VERSION}}", _version)` before writing the response. The version then auto-tracks future bumps.
2. **Manifest version bump.** `LaCompta/manifest.json` from `0.1.2` to `0.1.3`.

## Error handling

- **DB file creation fails** (disk full, permission denied): SQLite throws on first `Open()`. Log Error, mod becomes inert for that save (tracker won't write). User-facing: dashboard returns empty data. Not new behaviour — same failure mode as before.
- **Reconciliation `File.Delete` fails**: log Warn, leave file in place, continue. Will retry next reconciliation trigger. No mod crash.
- **`Constants.SavesPath` doesn't exist** (somehow): `Directory.EnumerateDirectories` throws. Catch, log Warn, skip reconciliation this round. Next launch retries.
- **`Constants.SaveFolderName` empty/null at SaveLoaded** (shouldn't happen — SMAPI guarantees it's valid by then): log Error, skip DB init. Deferred consequence: tracker stays null, dashboard shows empty. Not silent — the Error log makes it diagnosable.

## Testing

No automated test infrastructure exists for this mod (game is required at runtime). Verification is manual.

**Smoke test protocol (post-build, in-game):**

1. Fresh install: confirm `Mods/LaCompta/data/` is created on first save load.
2. Load Save A, play one day, ship items. Note net profit X.
3. Return to title, load Save B (different farm). Net profit on dashboard should be 0 (or B's own values), not X.
4. Confirm `Mods/LaCompta/data/` contains two `.db` files matching the two save folder names under `%AppData%\StardewValley\Saves\`.
5. Return to title, delete Save B via the title menu's delete UI. Quit and relaunch — confirm B's `.db` is gone, A's remains.
6. Repeat step 5 but without quitting (delete from title, load A). Confirm B's `.db` was pruned at `ReturnedToTitle`.
7. Upgrade test: place a fake old `lacompta.db` at the mod root, launch — confirm it's deleted on `GameLaunched` with an Info log line.

**Existing `lacompta_test` integration command** continues to work — it operates on the active save's DB, which is the new layout.

## Risks and mitigations

| Risk                                                                                          | Mitigation                                                                                              |
|-----------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------|
| User has legitimate mono-save data in legacy `lacompta.db` they wanted to keep.               | None at code level (decision was: drop). Release notes call this out so users can back up before upgrading. |
| `Constants.SaveFolderName` differs from save folder names in `Constants.SavesPath` (case, encoding). | Use `StringComparer.OrdinalIgnoreCase` on the HashSet lookup. Empirically these match exactly on Windows. |
| Reconciliation runs while Stardew is mid-deletion of a save (race condition).                 | `File.Delete` on a non-existent file is wrapped in try/catch already. Worst case: log Warn, retry next time. |

## Out of scope (future work)

- Per-save export of XLSX with the save name in the filename. Currently `lacompta_export` uses `farmName`; could be enhanced to include save folder for clarity, but not part of this release.
- Cross-save aggregation page. If users want to compare multiple saves, that's a separate feature.
- Database upgrade/migration framework. Schema is stable; if it ever changes, that's a separate spec.
