# Project: LaCompta

## What This Is

A SMAPI mod for Stardew Valley (v1.6.15) that automatically tracks all income and expenses by category (Farming, Foraging, Fishing, Mining, Other), stores time-series data locally, and presents statistics via Google Sheets integration or a local webserver with a game-styled dashboard — with PDF export and optional Prometheus metrics.

## Core Value

Stardew Valley players can track, visualize, and share their farm's financial performance across seasons — seeing exactly what's profitable and where their money goes.

## Current State

| Attribute | Value |
|-----------|-------|
| Version | 0.4.0 |
| Status | Multiplayer + Config Complete |
| Last Updated | 2026-03-29 |

## Requirements

### Must Have
- Track daily income/expenses by category (Farming, Foraging, Fishing, Mining, Other)
- Season-by-season statistics with best day of season tracking
- Item profitability analysis (revenue vs seed/fertilizer costs)
- Legendary fish sold tracking
- User expenses tracking
- Two output modes: Google Sheets (online) or local webserver (offline)
- Local dashboard with Stardew Valley pixel art style
- Time-series visualization of production over entire game history
- Dynamic Pie Charts / Historigrams (clickable to sort for example)
- Compatibility with Multi-Player games
- Mod configuration UI in-game (beyond basic toggle, button to open browser to the statistic page.)
- GitHub Actions CI/CD: build on push/PR, publish release artifacts on tag

### Should Have
- PDF export of statistics for sharing with other players
- Distinguished category breakdowns with color coding
- Line/area charts for daily trends per category
- Bar charts for category comparisons
- Summary totals per season
- Capitalist funny comedy mood


### Nice to Have
- Prometheus-like metrics exporter
- Lightweight time-series DB for historical data

### Out of Scope
- Mobile companion app

## Target Users

**Primary:** Stardew Valley players (PC) who want to optimize their farm economy
- Min-maxers tracking profitability
- Completionists tracking legendary fish and rare items
- Players who enjoy the business/farming simulation aspect

**Secondary:** Stardew Valley content creators
- Sharing stats with viewers via PDF or Google Sheets

## Context

**Technical Context:**
- Game: Stardew Valley v1.6.15 (build 24356), .NET 6 runtime
- Modding API: SMAPI 4.x with Pathoschild.Stardew.ModBuildConfig NuGet
- Platform: Linux primary, cross-platform via SMAPI
- Game data access: SMAPI events (DayStarted, DayEnding, SaveLoaded, InventoryChanged) + game state polling
- No dedicated shipping/selling events — requires change monitoring

**Business Context:**
- Open source mod, GitHub repo "LaCompta"
- French-inspired naming ("La Compta" = the ledger/accounting)

## Constraints

### Technical Constraints
- Must target .NET 6 (game runtime)
- Must use SMAPI mod framework
- Local webserver must not conflict with game performance
- SQLite for local storage (lightweight, no external DB dependency)
- Google Sheets API requires OAuth2 flow

### Business Constraints
- Open source (MIT or similar)
- Must not violate Stardew Valley modding guidelines

## Key Decisions

| Decision | Rationale | Date | Status |
|----------|-----------|------|--------|
| SQLite for time-series storage | Lightweight, no external deps, embedded in mod | 2026-03-28 | Active |
| Dual mode (Google Sheets / Local) | User choice: cloud convenience or privacy | 2026-03-28 | Active |
| SMAPI C# mod (not Content Patcher) | Need runtime logic, event handling, web server | 2026-03-28 | Active |
| Single-file SPA dashboard | No build step, inline CSS/JS, hash routing | 2026-03-28 | Active |
| Chart.js via CDN | Lightweight, no npm, works in single HTML file | 2026-03-28 | Active |
| Custom dropdown components | Native `<select>` unstyable for pixel art theme | 2026-03-28 | Active |
| Standalone Python dev server | Frontend dev without game, separate dev DB | 2026-03-28 | Active |
| GMCM optional soft dependency | Copy interface, no NuGet, graceful null check | 2026-03-29 | Active |
| PerScreen<T> for split-screen | Network multiplayer: each machine has own DB | 2026-03-29 | Active |
| Runtime sprite extraction | /api/sprite/{id} from Game1.objectSpriteSheet, cached | 2026-03-29 | Active |
| $(GamePath) for deploy | Cross-platform mod deploy via SMAPI property | 2026-03-29 | Active |

## Tech Stack

| Layer | Technology | Notes |
|-------|------------|-------|
| Framework | SMAPI 4.x | Stardew Valley modding API |
| Language | C# / .NET 6 | Game runtime |
| Storage | SQLite | Embedded time-series data |
| Web Server | HttpListener or Kestrel | Local dashboard serving |
| Frontend | HTML/CSS/JS | Stardew Valley pixel art style |
| Charts | Chart.js or similar | Lightweight, no deps |
| Cloud | Google Sheets API | Optional online mode |
| Export | PDF generation library | Statistics sharing |
| Metrics | Prometheus exporter | Optional, nice-to-have |

## Specialized Flows

See: .paul/SPECIAL-FLOWS.md

Quick Reference:
- /frontend-design (required) → Local web dashboard UI, Stardew Valley pixel art style
- /code-review (required) → PR review and code quality checks
- /simplify (optional) → Code refactoring and cleanup

## Links

| Resource | URL |
|----------|-----|
| Repository | https://github.com/bcalvet/LaCompta |
| Stardew Modding Wiki | https://stardewvalleywiki.com/Modding:Index |
| SMAPI Docs | https://stardewvalleywiki.com/Modding:Modder_Guide/Get_Started |

---
*PROJECT.md — Updated when requirements or context change*
*Last updated: 2026-03-28*
