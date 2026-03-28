---
phase: 01-scaffolding
plan: 01
subsystem: infra
tags: [smapi, dotnet6, sqlite, mod-skeleton]

requires:
  - phase: none
    provides: first plan
provides:
  - Working SMAPI mod skeleton that loads in game
  - SQLite data layer with 4 models and repository
  - Documentation foundation (6 doc files)
affects: [01-02-event-hooks, 02-dashboard, 03-google-sheets]

tech-stack:
  added: [SMAPI 4.5.2, .NET 6.0.428, Microsoft.Data.Sqlite 6.0.x, SQLitePCLRaw]
  patterns: [SMAPI ModEntry pattern, SQLite repository pattern, BundleExtraAssemblies]

key-files:
  created: [LaCompta/ModEntry.cs, LaCompta/Data/DatabaseContext.cs, LaCompta/Data/Repository.cs, LaCompta/Models/*.cs]
  modified: []

key-decisions:
  - "BundleExtraAssemblies must be 'ThirdParty, System' for Microsoft.Data.Sqlite"
  - "Microsoft.Data.Sqlite pinned to 6.0.x (must match game .NET runtime)"
  - "Avoid em dashes in log strings (SMAPI console encoding issue)"

patterns-established:
  - "Repository pattern for all DB access via LaCompta.Data.Repository"
  - "Models in LaCompta.Models with computed properties for derived values"
  - "PlayerId field on all models for future multiplayer support"

duration: ~25min
started: 2026-03-28T09:40:00Z
completed: 2026-03-28T10:05:00Z
---

# Phase 1 Plan 01: Dev Environment + Mod Skeleton + Data Layer Summary

**Working SMAPI mod with SQLite data layer (4 models, repository) deployed and loading in Stardew Valley v1.6.15**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~25 min |
| Started | 2026-03-28 09:40 |
| Completed | 2026-03-28 10:05 |
| Tasks | 3 completed |
| Files modified | 17 |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Dev Environment Ready | Pass | .NET 6.0.428 + SMAPI 4.5.2 installed |
| AC-2: Mod Compiles and Loads | Pass | SMAPI console shows "LaCompta loaded" |
| AC-3: SQLite Schema + Repository | Pass | All models + DB layer compile, tables defined |

## Accomplishments

- .NET 6 SDK (6.0.428) installed via Microsoft install script, SMAPI 4.5.2 installed non-interactively
- LaCompta mod skeleton loads in SMAPI with DayStarted and SaveLoaded event hooks
- Complete data layer: 4 models (DailyRecord, ItemTransaction, SeasonSummary, FishRecord) with SQLite schema and Repository CRUD
- All models include PlayerId field for future multiplayer support
- Documentation foundation: CLAUDE.md, CONTRIBUTING, ARCHITECTURE, TROUBLESHOOTING, MODDING-NOTES, CHANGELOG

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `LaCompta.sln` | Created | Solution file |
| `LaCompta/LaCompta.csproj` | Created | Project with SMAPI + SQLite deps |
| `LaCompta/manifest.json` | Created | SMAPI mod manifest |
| `LaCompta/ModEntry.cs` | Created | SMAPI entry point |
| `LaCompta/Models/DailyRecord.cs` | Created | Daily income/expense by category |
| `LaCompta/Models/ItemTransaction.cs` | Created | Individual item sales + profitability |
| `LaCompta/Models/SeasonSummary.cs` | Created | Season aggregates + best day |
| `LaCompta/Models/FishRecord.cs` | Created | Fish catches + legendary tracking |
| `LaCompta/Data/DatabaseContext.cs` | Created | SQLite init + schema creation |
| `LaCompta/Data/Repository.cs` | Created | CRUD operations for all models |
| `.gitignore` | Created | .NET + SMAPI ignores |
| `LICENSE` | Replaced | MIT license (was Apache 2.0) |
| `CLAUDE.md` | Created | Dev guidelines |
| `CHANGELOG.md` | Created | Version history |
| `docs/CONTRIBUTING.md` | Created | Dev setup + PR guide |
| `docs/ARCHITECTURE.md` | Created | Component diagram + data flow |
| `docs/TROUBLESHOOTING.md` | Created | Known issues (populated during dev) |
| `docs/MODDING-NOTES.md` | Created | SMAPI tricks + game internals |

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Microsoft.Data.Sqlite 6.0.x (not 8.0.x) | v8 fails SMAPI assembly rewriting — must match game .NET 6 runtime | Pin all Microsoft packages to 6.0.x |
| BundleExtraAssemblies = ThirdParty, System | Microsoft packages need "System" flag to deploy; "ThirdParty" alone is insufficient | Must keep this setting for any Microsoft NuGet |
| Avoid special chars in SMAPI logs | Em dashes cause encoding artifacts in SMAPI console | Use ASCII dashes in log strings |

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 2 | Essential fixes, no scope creep |
| Deferred | 0 | - |

### Auto-fixed Issues

**1. SQLite assembly not bundled by ModBuildConfig**
- **Found during:** Task 3 (checkpoint verification)
- **Issue:** SMAPI couldn't load mod — `AssemblyResolutionException` for Microsoft.Data.Sqlite
- **Fix:** Changed BundleExtraAssemblies from "ThirdParty" to "ThirdParty, System"
- **Verification:** Mod loads after rebuild

**2. Microsoft.Data.Sqlite 8.0 incompatible with SMAPI rewriter**
- **Found during:** Task 3 (checkpoint verification)
- **Issue:** SMAPI assembly rewriter failed on v8.0.25.0 reference
- **Fix:** Downgraded to Microsoft.Data.Sqlite 6.0.x
- **Verification:** Mod loads successfully

## Lessons Learned

- **SMAPI's assembly rewriter is strict**: All referenced assemblies must be resolvable. Bundle everything your mod needs.
- **Match NuGet package major versions to game runtime**: Stardew Valley runs .NET 6, so use 6.0.x versions of Microsoft packages.
- **ModBuildConfig BundleExtraAssemblies categories**: `ThirdParty` = non-Microsoft NuGet, `System` = Microsoft NuGet not in game runtime. `All` is NOT valid.
- **SMAPI installer on Linux**: Can be automated with `printf '1\n1\n1\n'` piped to stdin.

## Next Phase Readiness

**Ready:**
- Mod skeleton loads and runs in SMAPI
- Data models and SQLite repository ready for event hooks
- DayStarted and SaveLoaded events already wired (placeholder)

**Concerns:**
- No dedicated SMAPI event for item shipping/selling — will need polling or InventoryChanged monitoring in Plan 01-02

**Blockers:** None

---
*Phase: 01-scaffolding, Plan: 01*
*Completed: 2026-03-28*
