---
phase: 01-scaffolding
plan: 02
subsystem: core-engine
tags: [smapi-events, category-classifier, profitability, sqlite, legendary-fish, testing]

requires:
  - phase: 01-01
    provides: mod skeleton, data models, SQLite repository
provides:
  - Core tracking engine (income/expenses by category)
  - Category classifier (Farming/Foraging/Fishing/Mining/Other)
  - Profitability calculator (revenue - seed cost)
  - Season summary generation
  - Legendary fish detection
  - Integration test suite (6 tests)
affects: [02-dashboard, 03-google-sheets, 04-multiplayer]

tech-stack:
  added: []
  patterns: [SMAPI event-driven tracking, numeric category IDs, money delta expense tracking, SMAPI console test commands]

key-files:
  created: [LaCompta/Services/TrackingService.cs, LaCompta/Services/CategoryClassifier.cs, LaCompta/Services/ProfitabilityCalculator.cs, LaCompta/Services/SeasonSummaryService.cs, scripts/test-mod.sh]
  modified: [LaCompta/ModEntry.cs, LaCompta/Data/DatabaseContext.cs, LaCompta/Data/Repository.cs, LaCompta/LaCompta.csproj]

key-decisions:
  - "Use numeric category IDs instead of named constants (cross-version compatibility)"
  - "Use system e_sqlite3 native lib copied to mod folder (SMAPI sandbox workaround)"
  - "Microsoft.Data.Sqlite 6.0.x with e_sqlite3 provider (not system sqlite3)"
  - "INSERT ON CONFLICT DO UPDATE instead of INSERT OR REPLACE (preserves row IDs)"
  - "In-mod console test commands for integration testing"

patterns-established:
  - "Services in LaCompta.Services namespace, injected with Repository + IMonitor"
  - "SMAPI console commands for testing: lacompta_test, lacompta_status"
  - "test-mod.sh script for build/deploy/test cycle"

duration: ~40min
started: 2026-03-28T10:05:00Z
completed: 2026-03-28T10:55:00Z
---

# Phase 1 Plan 02: Core Tracking Engine Summary

**Complete income/expense tracking by category with legendary fish detection, profitability analysis, and 6/6 integration tests passing**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~40 min |
| Started | 2026-03-28 10:05 |
| Completed | 2026-03-28 10:55 |
| Tasks | 3 completed |
| Files modified | 11 |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Daily income tracked by category | Pass | Farming:175g, Fishing:200g, Mining:750g verified |
| AC-2: Item transactions recorded | Pass | Each item saved with name, category, price, cost basis |
| AC-3: Legendary fish detected | Pass | Legend (ID 163) correctly flagged and recorded |
| AC-4: Season summaries generated | Pass | SeasonSummaryService triggers on day 28 |
| AC-5: Expenses tracked | Pass | 500g simulated purchase detected via money delta |

## Accomplishments

- Full tracking pipeline: game events -> classifier -> calculator -> SQLite
- 6/6 integration tests passing via `lacompta_test` console command
- Native SQLite loading resolved for Linux/SMAPI sandbox
- DB vacuum safeguard (prunes data > 4 years when DB exceeds 2 GiB)
- Automated test script with build/deploy/launch/tail modes

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Numeric category IDs | Named constants vary across SMAPI versions | More reliable, documented in MODDING-NOTES.md |
| Copy libe_sqlite3.so to mod root | SMAPI sandbox doesn't resolve runtimes/ folder | Cross-platform: needs different native lib per OS |
| INSERT ON CONFLICT DO UPDATE | INSERT OR REPLACE deletes row, breaking foreign keys | Safer for repeated DayEnding on same day |
| Money delta for expenses | No SMAPI purchase event exists | Simple but effective: snapshot at DayStarted, diff at DayEnding |

## Deviations from Plan

### Summary

| Type | Count | Impact |
|------|-------|--------|
| Auto-fixed | 3 | Essential fixes for SQLite native lib loading |
| Scope additions | 1 | Added test commands (not in original plan) |

### Auto-fixed Issues

**1. CategoryClassifier named constants don't exist**
- Found: `maboreMaterialCategory` not in SMAPI 4.5.2
- Fix: Switched to numeric category IDs
- Verification: All 4 categories correctly classified in tests

**2. SQLite native library not found in SMAPI sandbox**
- Found: Three attempts needed (DllImportResolver, NativeLibrary.Load, system sqlite3)
- Fix: Copy libe_sqlite3.so from NuGet cache to mod output + deploy folder via csproj target
- Verification: DB initializes on SaveLoaded

**3. INSERT OR REPLACE breaks foreign keys**
- Found: FOREIGN KEY constraint failed on daily_records
- Fix: Changed to INSERT ON CONFLICT DO UPDATE
- Verification: Multiple day cycles work without errors

## Lessons Learned

- SMAPI's pressure-vessel sandbox on Linux doesn't resolve `runtimes/{rid}/native/` paths. Native libs must be in the mod root.
- ModBuildConfig only copies `.dll` files, not `.so`/`.dylib`. Use a custom MSBuild target to copy native libs.
- `NativeLibrary.SetDllImportResolver` doesn't work when the library is loaded by a static constructor before your resolver is registered.
- SMAPI console commands are excellent for in-mod testing. Register with `helper.ConsoleCommands.Add()`.
- `Game1.getFarm().getShippingBin(player)` gives access to items before they're cleared at day end.

## Next Phase Readiness

**Ready:**
- All data collection and storage working
- Repository provides all query methods needed for dashboard
- Test infrastructure in place

**Concerns:**
- Expense tracking is basic (money delta) — misses income from non-shipping sources
- Season summary only tested via code path, not full 28-day cycle

**Blockers:** None

---
*Phase: 01-scaffolding, Plan: 02*
*Completed: 2026-03-28*
