<p align="center">
  <img src="LaCompta/Web/Assets/favicon.png" alt="LaCompta Logo" width="120">
</p>

<h1 align="center">LaCompta</h1>

<p align="center">
  <a href="https://github.com/CYBERBUGJR/LaCompta/actions/workflows/build.yml"><img src="https://github.com/CYBERBUGJR/LaCompta/actions/workflows/build.yml/badge.svg" alt="Build"></a>
</p>

<p align="center"><em>"Salut salut, c'est Valérie de la compta, ouais, ouais, super écoute, je t'appelle par rapport aux poireaux que tu as oublié de déclarer à l'URSSAF"</em></p>

A SMAPI mod for Stardew Valley that tracks your farm's financial performance across seasons. Because every gold coin counts.

> **Warning:** This mod was made by a French person. Don't be afraid of some private cultural references (URSSAF, Valérie de la compta, poireaux...). It's all part of the charm.

## Features

- Daily income tracking by category: Farming, Foraging, Fishing, Mining, Other
- Season-by-season statistics with best day of the season
- Item profitability analysis with revenue vs seed/fertilizer costs
- Legendary fish tracking — know when you've sold a legend
- Expense tracking — where does all that gold go?
- Local web dashboard with Stardew Valley pixel art style (5 pages: Overview, Season Compare, Profitability, Sales Ledger, Legendary Fish)
- Excel XLSX export — multi-sheet report with Overview, per-season data, and Sales Ledger
- In-game config via GMCM (Generic Mod Config Menu) — optional
- Split-screen multiplayer support with per-player data isolation
- Item sprites from the game's spritesheet in Sales Ledger
- Google Sheets integration — sync your stats to the cloud (coming soon)

## Requirements

- [Stardew Valley](https://store.steampowered.com/app/413150/Stardew_Valley/) 1.6.15+
- [SMAPI](https://smapi.io/) 4.0+ (the modding framework)
- Optional: [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) for in-game settings

## Installation (from release)

1. Install [SMAPI](https://smapi.io/) if you haven't already
2. Download the latest `LaCompta-x.x.x.zip` from the [Releases page](https://github.com/CYBERBUGJR/LaCompta/releases)
3. Extract the zip into your Stardew Valley `Mods/` folder:
   - **Windows:** `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\`
   - **Linux:** `~/.local/share/Steam/steamapps/common/Stardew Valley/Mods/`
   - **macOS:** `~/Library/Application Support/Steam/steamapps/common/Stardew Valley/Mods/`
4. Launch the game through SMAPI
5. Open the dashboard: type `lacompta_open` in the SMAPI console, or visit http://localhost:5555/

Your folder structure should look like:
```
Stardew Valley/
  Mods/
    LaCompta/
      LaCompta.dll
      manifest.json
      Assets/
        dashboard.html
        style.css
        app.js
        ...
      ClosedXML.dll
      Microsoft.Data.Sqlite.dll
      ...
```

## Building from Source

### Prerequisites

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [Stardew Valley](https://store.steampowered.com/app/413150/Stardew_Valley/) installed (for game references)
- [SMAPI](https://smapi.io/) installed in the game folder

### Build & deploy

```bash
git clone https://github.com/CYBERBUGJR/LaCompta.git
cd LaCompta
dotnet restore
dotnet build
```

The mod auto-deploys to your `Stardew Valley/Mods/LaCompta/` folder via SMAPI's ModBuildConfig.

### Frontend development (no game needed)

```bash
python3 scripts/dev-server.py
# Dashboard at http://localhost:5555/ with 3 years of mock data
```

### SMAPI console commands

| Command | Description |
|---------|-------------|
| `lacompta_open` | Open the web dashboard in your browser |
| `lacompta_export` | Export an Excel (.xlsx) report to the mod folder |
| `lacompta_test` | Run integration tests |
| `lacompta_status` | Show tracked seasons and stats |

See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for full dev setup and PR guidelines.

## Documentation

- [Architecture](docs/ARCHITECTURE.md) How the mod works
- [Contributing](docs/CONTRIBUTING.md) Dev setup and PR guidelines
- [Troubleshooting](docs/TROUBLESHOOTING.md) Common issues and fixes
- [Modding Notes](docs/MODDING-NOTES.md) SMAPI tricks and game data quirks

## License

MIT
