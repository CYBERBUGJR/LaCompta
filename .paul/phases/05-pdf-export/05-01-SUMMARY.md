---
phase: 05-pdf-export
plan: 01
subsystem: export, ui
tags: [closedxml, xlsx, excel, export, empty-state]

requires:
  - phase: 04-multiplayer-config/04-01
    provides: Repository, ApiController, dashboard, Sales Ledger
provides:
  - Excel XLSX export with Overview, per-season, and Sales Ledger sheets
  - /api/report/xlsx endpoint
  - Export button in dashboard + lacompta_export console command
  - Clean empty state handling across all pages
affects: [ci-cd]

tech-stack:
  added: [ClosedXML 0.102.x]
  removed: [QuestPDF (SMAPI incompatible), PdfSharpCore (font issues), ScottPlot]

key-decisions:
  - "XLSX over PDF: QuestPDF/SkiaSharp broke SMAPI assembly rewriter, PdfSharpCore had font embedding issues"
  - "ClosedXML for Excel: pure .NET, no native deps, SMAPI compatible"
  - "Three sheets: Overview, per-season, Sales Ledger with auto-filter"
  - "Fallback to all-player data when current playerId has no records"

duration: ~2h
started: 2026-03-29T14:00:00Z
completed: 2026-03-29T16:00:00Z
---

# Phase 5 Plan 01: Export + Polish Summary

**Excel XLSX export replacing PDF (SMAPI compatibility), with clean empty-state handling across dashboard.**

## Acceptance Criteria Results

| Criterion | Status | Notes |
|-----------|--------|-------|
| AC-1: Report generates with season data | Pass | XLSX with Overview + per-season sheets |
| AC-2: Charts in export | N/A | Pivoted from PDF+charts to XLSX+tables (no charts in Excel) |
| AC-3: Dashboard export button | Pass | "Export XLSX" button, hidden when no data |
| AC-4: Bonus statistics | Pass | Overview sheet has totals, best season, net profit |
| AC-5: Console command | Pass | lacompta_export saves .xlsx to mod data folder |

## Deviations

| Type | Count | Impact |
|------|-------|--------|
| Pivot | 1 | PDF → XLSX (SMAPI incompatibility forced change) |
| Scope additions | 2 | Empty state handling, no-data UI cleanup |

### PDF → XLSX Pivot
- QuestPDF + SkiaSharp: SMAPI marked mod as "no longer compatible" (native SkiaSharp DLLs)
- PdfSharpCore: built OK but fonts rendered as unicode boxes (no embedded font resolver on Linux)
- ClosedXML: pure .NET, no native deps, works perfectly with SMAPI

## Files Created/Modified

| File | Change | Purpose |
|------|--------|---------|
| `LaCompta/Services/ExcelExportService.cs` | Created | XLSX generation with 3 sheets |
| `LaCompta/LaCompta.csproj` | Modified | ClosedXML replacing PdfSharpCore |
| `LaCompta/ModEntry.cs` | Modified | lacompta_export command |
| `LaCompta/Web/ApiController.cs` | Modified | /api/report/xlsx endpoint |
| `LaCompta/Web/Assets/app.js` | Modified | Export button, empty state handling |
| `LaCompta/Web/Assets/dashboard.html` | Modified | Export button element |
| `LaCompta/Web/Assets/style.css` | Modified | Export button + empty state styles |
| `docs/CONTRIBUTING.md` | Modified | Export command docs |

---
*Phase: 05-pdf-export, Plan: 01*
*Completed: 2026-03-29*
