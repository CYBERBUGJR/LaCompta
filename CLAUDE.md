# LaCompta - Development Guidelines

## Project Overview
LaCompta is a SMAPI mod for Stardew Valley (v1.6.15) that tracks farm economics.

## Build
```bash
dotnet build
```

## Project Structure
- `LaCompta/` — Main mod project (C# .NET 6)
  - `Models/` — Data models (DailyRecord, ItemTransaction, etc.)
  - `Data/` — SQLite database context and repository
  - `Services/` — Business logic (tracking, calculations)
  - `Web/` — Embedded web server and dashboard
  - `ModEntry.cs` — SMAPI entry point
  - `manifest.json` — SMAPI mod manifest

## Conventions
- Target: .NET 6 (Stardew Valley runtime)
- Use SMAPI events, not Harmony patches (when possible)
- SQLite via Microsoft.Data.Sqlite for all persistence
- Cross-platform paths: always use Path.Combine()
- Log levels: Info for user-facing, Debug for development, Trace for verbose
