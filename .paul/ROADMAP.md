# Roadmap: LaCompta

## Overview

Build a Stardew Valley SMAPI mod from zero to a full-featured farm economics tracker. Consolidated into 7 lean phases to minimize token usage.

## Current Milestone

**v0.1 Initial Release** (v0.1.0)
Status: In progress
Phases: 4 of 7 complete

## Token Cost Estimation (Lean Mode)

| Phase | Plans | Est. Tokens | Notes |
|-------|-------|-------------|-------|
| 1 - Scaffolding + Core Engine | 2 | ~150K | Merged: setup + data collection in one phase |
| 2 - Local Dashboard | 2 | ~180K | Webserver + frontend in 2 plans, use /frontend-design |
| 3 - Google Sheets | 1 | ~80K | Single plan: OAuth + sync + formatting |
| 4 - Multiplayer + Config UI | 1 | ~100K | Merged: MP support + GMCM config in one pass |
| 5 - PDF Export + Polish | 1 | ~60K | PDF gen + comedy mood + final polish |
| 6 - CI/CD | 1 | ~40K | GitHub Actions, straightforward |
| 7 - Prometheus (Nice to have) | 1 | ~40K | Optional, skip if budget tight |
| **Total** | **9 plans** | **~650K** | **~35% leaner than original** |

**Max plan impact:** ~9 sessions spread over time, each ~70K tokens avg.
**Time estimate:** ~15-22 hours active dev (sessions can be short)

## Phases

| Phase | Name | Plans | Status | Completed |
|-------|------|-------|--------|-----------|
| 1 | Scaffolding + Core Engine | 2 | Complete | 2026-03-28 |
| 2 | Local Web Dashboard | 2 | Complete | 2026-03-28 |
| 3 | Google Sheets Integration | 1 | Not started | - |
| 4 | Multiplayer + In-Game Config | 1 | Complete | 2026-03-29 |
| 5 | XLSX Export + Polish | 1 | Complete | 2026-03-29 |
| 6 | CI/CD Pipeline | 1 | Not started | - |
| 7 | Prometheus Exporter | 1 | Not started | - |

## Phase Details

### Phase 1: Scaffolding + Core Engine
**Goal:** Working mod that tracks all income/expenses by category, stores in SQLite, with season summaries
**Depends on:** Nothing
**Research:** Likely (SMAPI setup, game internals for sell/ship detection)

**Scope:**
- Install .NET 6 SDK + SMAPI
- Mod project structure (csproj, manifest, ModEntry.cs)
- SQLite schema + repository layer
- SMAPI event hooks (DayEnding, SaveLoaded, InventoryChanged)
- Category classification (Farming/Foraging/Fishing/Mining/Other)
- Item profitability (revenue - seed - fertilizer costs)
- Legendary fish tracking, expense tracking
- Season summaries + best day calculation

**Plans:**
- [ ] 01-01: Dev environment + mod skeleton + SQLite schema + data models
- [ ] 01-02: Event hooks + category engine + profitability + season summaries

### Phase 2: Local Web Dashboard
**Goal:** Embedded webserver with Stardew-styled interactive statistics dashboard
**Depends on:** Phase 1 (needs data)
**Research:** Likely (HttpListener in SMAPI, Chart.js)
**Required Skills:** /frontend-design

**Scope:**
- HttpListener web server (background thread)
- REST API: /api/seasons, /api/daily, /api/profitability, /api/fish, /api/summary
- Stardew Valley pixel art CSS (custom font, palette, borders)
- Chart.js: line/area/bar/pie charts, clickable/sortable
- Season navigation, capitalist comedy mood

**Plans:**
- [ ] 02-01: Web server + REST API + JSON endpoints
- [ ] 02-02: Full frontend: Stardew-styled layout + all charts + interactivity (/frontend-design)

### Phase 3: Google Sheets Integration
**Goal:** One-click Google account linking, auto-sync to formatted spreadsheet
**Depends on:** Phase 1 (needs data models)
**Research:** Likely (Google Sheets API v4, OAuth2 desktop flow)

**Scope:**
- OAuth2 flow + token storage + refresh
- Create spreadsheet with per-season tabs
- Layout matching reference (daily rows, category columns, totals, conditional formatting)
- Auto-sync on day end

**Plans:**
- [ ] 03-01: Complete Google Sheets integration (OAuth + sync + formatting)

### Phase 4: Multiplayer + In-Game Config
**Goal:** Mod works in multiplayer with per-player tracking + GMCM settings menu with browser launch button
**Depends on:** Phase 2 (dashboard must exist for browser button)
**Research:** Likely (SMAPI multiplayer API)

**Scope:**
- Per-player SQLite isolation
- SMAPI multiplayer message passing (sync to host)
- Combined + individual player views
- GMCM config: output mode, port, auto-sync, "Open Dashboard" button

**Plans:**
- [ ] 04-01: Multiplayer data sync + GMCM config UI + browser launch

### Phase 5: PDF Export + Polish
**Goal:** PDF report generation + final polish with comedy mood and extra stats
**Depends on:** Phase 2 (reuses dashboard layout)
**Research:** Unlikely

**Scope:**
- QuestPDF: export season or full history
- Stardew-styled PDF layout with chart snapshots
- Capitalist comedy Easter eggs throughout UI
- Bonus stats: ROI per crop, gold/hour, shipping streak, category milestones, season-over-season growth

**Plans:**
- [ ] 05-01: PDF generation + comedy mood + bonus statistics

### Phase 6: CI/CD Pipeline
**Goal:** GitHub Actions builds on push/PR, publishes release zip on tag
**Depends on:** Phase 1 (needs buildable project)
**Research:** Unlikely

**Scope:**
- Build workflow (restore, build, test)
- Release workflow (tag → build → zip → GitHub Release)
- README badge

**Plans:**
- [ ] 06-01: GitHub Actions build + release workflows

### Phase 7: Prometheus Exporter (Nice to Have)
**Goal:** Optional /metrics endpoint for external monitoring
**Depends on:** Phase 2 (extends web server)
**Research:** Unlikely

**Scope:**
- /metrics in Prometheus format
- Gauges/counters for gold, categories, items
- GMCM toggle

**Plans:**
- [ ] 07-01: Prometheus exposition endpoint + metrics

## Cross-Cutting: Documentation Knowledge Base

**Every phase must update documentation.** This is not a separate phase — it's built into each plan.

**Deliverables maintained throughout:**
- `README.md` — Project overview, installation guide, screenshots, usage
- `docs/CONTRIBUTING.md` — How to set up dev environment, build, test, contribute
- `docs/ARCHITECTURE.md` — Mod structure, data flow, component diagram
- `docs/TROUBLESHOOTING.md` — Common issues, gotchas, workarounds discovered during dev
- `docs/MODDING-NOTES.md` — SMAPI tricks, Stardew Valley internals learned, undocumented behaviors
- `CHANGELOG.md` — Version history with user-facing changes

**Rule:** Each plan's SUMMARY.md must include a "Lessons Learned" section. Key discoveries get promoted to docs/.

**What goes in docs:**
- Dev environment setup gotchas (Linux-specific, .NET 6 quirks)
- SMAPI event behavior that isn't obvious from docs
- Game data access patterns (how to get sell prices, seed costs, etc.)
- SQLite threading considerations in SMAPI context
- HttpListener vs Kestrel tradeoffs discovered
- Google OAuth2 flow for desktop apps — what worked, what didn't
- Multiplayer data sync edge cases
- Any workarounds for incomplete SMAPI documentation

---
*Roadmap created: 2026-03-28*
*Last updated: 2026-03-28*
