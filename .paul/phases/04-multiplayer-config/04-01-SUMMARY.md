---
phase: 04-multiplayer-config
plan: 01
subsystem: multiplayer, ui, config
tags: [gmcm, perscreen, split-screen, config, sales-ledger, sprites]

requires:
  - phase: 02-dashboard/02-02
    provides: Web dashboard, ApiController, app.js, style.css
provides:
  - ModConfig with SMAPI persistence (auto-open, port)
  - GMCM integration (optional soft dependency)
  - PerScreen<T> split-screen isolation
  - Player selector for multiplayer dashboard
  - Sales Ledger page with day selector and item sprites
  - Context-aware stat cards with info bubbles
  - Stacked area toggle for trend charts
  - Per Year trend line chart
affects: [pdf-export, ci-cd]

tech-stack:
  added: [GMCM API interface (copied, no NuGet)]
  patterns: [PerScreen<T> for split-screen, /api/sprite/{id} runtime sprite extraction]

key-files:
  created:
    - LaCompta/Models/ModConfig.cs
    - LaCompta/Integrations/IGenericModConfigMenuApi.cs
  modified:
    - LaCompta/ModEntry.cs
    - LaCompta/Data/Repository.cs
    - LaCompta/LaCompta.csproj
    - LaCompta/Web/ApiController.cs
    - LaCompta/Web/Assets/app.js
    - LaCompta/Web/Assets/dashboard.html
    - LaCompta/Web/Assets/style.css
    - scripts/dev-server.py
    - docs/CONTRIBUTING.md

key-decisions:
  - "GMCM as optional soft dependency — copy interface, no NuGet"
  - "PerScreen<T> for split-screen, regular fields for network multiplayer"
  - "Single web server, filter by playerId (no per-screen servers)"
  - "Game1.getOnlineFarmers() not getAllFarmers() for player list"
  - "Port field as TextOption not NumberOption (avoids slider UX)"
  - "$(GamePath) for cross-platform mod deploy path"
  - "Runtime sprite extraction from Game1.objectSpriteSheet with cache"
  - "Stat cards context-aware based on current view selection"

patterns-established:
  - "apiUrl() helper for playerId-aware API calls"
  - "/api/sprite/{id} endpoint for game asset serving"
  - "Info bubble pattern: .stat-info-icon with hover .stat-info-bubble"
  - "Toggle checkbox pattern: .toggle-option for chart options"

duration: ~3h
started: 2026-03-29T09:00:00Z
completed: 2026-03-29T12:00:00Z
---

# Phase 4 Plan 01: Multiplayer + In-Game Config Summary

**GMCM config menu, split-screen multiplayer via PerScreen<T>, Sales Ledger with item sprites, and context-aware dashboard stat cards.**

## Performance

| Metric | Value |
|--------|-------|
| Duration | ~3 hours |
| Started | 2026-03-29T09:00:00Z |
| Completed | 2026-03-29T12:00:00Z |
| Tasks | 3 completed (2 auto + 1 human-verify) |
| Files modified | 11 |

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Split-screen isolates per-player data | Pass | PerScreen<T> wraps TrackingService and SeasonSummaryService |
| AC-2: ModConfig persists via SMAPI config | Pass | config.json created with AutoOpenDashboard + WebServerPort |
| AC-3: GMCM menu registers | Pass | Dashboard section with bool toggle, text port field, paragraph |
| AC-4: Mod works without GMCM | Pass | Graceful null check, logs at Debug level |
| AC-5: Dashboard API supports player filtering | Pass | All endpoints accept playerId, /api/players lists online farmers |

## Accomplishments

- GMCM integration with auto-open toggle and port text field
- Split-screen multiplayer support via PerScreen<T>
- Player selector dropdown (online players only, not historical)
- Sales Ledger page: items grouped by category, day selector (1-28), item sprites from game spritesheet
- Context-aware stat cards that update for season/year/all-time views
- Info icon bubbles explaining each stat
- Stacked area toggle for trend charts
- Per Year view now shows daily trend line chart
- Cross-platform deploy via $(GamePath)
- Init fetch ordering fix: get playerId before loading filtered data

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `LaCompta/Models/ModConfig.cs` | Created | SMAPI config: AutoOpenDashboard, WebServerPort |
| `LaCompta/Integrations/IGenericModConfigMenuApi.cs` | Created | GMCM API interface (copied pattern) |
| `LaCompta/ModEntry.cs` | Modified | PerScreen<T>, GMCM registration, config loading, auto-open |
| `LaCompta/Data/Repository.cs` | Modified | GetAllTransactions with day/category filters |
| `LaCompta/LaCompta.csproj` | Modified | $(GamePath) for cross-platform deploy |
| `LaCompta/Web/ApiController.cs` | Modified | /api/players, /api/transactions, /api/sprite/{id}, farminfo playerId |
| `LaCompta/Web/Assets/app.js` | Modified | Sales Ledger, player selector, apiUrl(), context-aware cards, stacked toggle |
| `LaCompta/Web/Assets/dashboard.html` | Modified | Sales Ledger page, player-selector div, stacked checkbox |
| `LaCompta/Web/Assets/style.css` | Modified | Player selector, info bubbles, toggle, day selector, sprite styles |
| `scripts/dev-server.py` | Modified | /api/players, /api/transactions, seed data spread across days |
| `docs/CONTRIBUTING.md` | Modified | GMCM docs, multiplayer behavior |

## Deviations from Plan

| Type | Count | Impact |
|------|-------|--------|
| Scope additions | 5 | User-requested UX improvements |
| Boundary crossings | 3 | style.css, Repository.cs, csproj modified (boundary said DO NOT CHANGE) |

### Scope Additions (User-Requested)

1. **Sales Ledger page** — not in original plan, requested by user's friend
2. **Item sprites** — /api/sprite/{id} runtime extraction from game spritesheet
3. **Context-aware stat cards** — cards adapt to season/year/all-time view
4. **Stacked area toggle** — checkbox to toggle filled area under trend curves
5. **Info bubbles** — hover (i) icon on stat cards explaining each metric

### Boundary Crossings

1. **style.css** — plan said DO NOT CHANGE, but needed for player selector, info bubbles, sales ledger, toggle styles
2. **Repository.cs** — added GetAllTransactions for Sales Ledger
3. **LaCompta.csproj** — changed GameModsDir to use $(GamePath) for cross-platform

### Issues Fixed During Apply

- `getAllFarmers()` showed offline players from previous multiplayer → switched to `getOnlineFarmers()`
- Init fetched data before playerId was set → reordered to get farminfo first
- GMCM NumberOption rendered as slider → switched to TextOption with validation
- Dev server `api_players()` missing params argument → added `params=None`
- Dev server farminfo playerId mismatch with seed data → aligned to "player1"

## Skill Audit

No specialized skills required for this phase. N/A.

## Next Phase Readiness

**Ready:**
- Dashboard now has 5 pages (Overview, Comparison, Profitability, Sales Ledger, Legendary Fish)
- Multiplayer support in place
- GMCM config working
- Sprite serving infrastructure for future pages

**Concerns:**
- Expense tracking is still money-delta approximation (needs Harmony patches for precision)
- ProfitabilityCalculator stale cache across save reloads still not fixed
- Sprites only available in-game (dev server shows no sprites)

**Blockers:**
- None

---
*Phase: 04-multiplayer-config, Plan: 01*
*Completed: 2026-03-29*
