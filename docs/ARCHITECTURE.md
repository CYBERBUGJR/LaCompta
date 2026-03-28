# LaCompta Architecture

## Data Flow
```
Game Events (SMAPI) → Tracker Service → SQLite Database
                                              ↓
                              ┌────────────────┼────────────────┐
                              ↓                ↓                ↓
                     Local Dashboard    Google Sheets      PDF Export
                     (HttpListener)     (Sheets API)      (QuestPDF)
```

## Components
- **ModEntry**: SMAPI entry point, wires up event handlers
- **TrackingService**: Captures income/expenses from game events
- **CategoryClassifier**: Maps items to Farming/Foraging/Fishing/Mining/Other
- **Repository**: SQLite CRUD layer
- **WebServer**: Embedded HttpListener serving dashboard
- **GoogleSheetsClient**: OAuth2 + Sheets API sync
- **PdfExporter**: Statistics report generation

## Data Models
- `DailyRecord`: Per-day income/expense totals by category
- `ItemTransaction`: Individual item sales with profitability
- `SeasonSummary`: Aggregated season statistics
- `FishRecord`: Fish caught/sold with legendary tracking
