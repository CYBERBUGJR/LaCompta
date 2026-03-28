# LaCompta

> *"Salut salut, c'est Valérie de la compta, ouais, ouais, super écoute, je t'appelle par rapport aux poireaux que tu as oublié de déclarer à l'URSSAF"*

A SMAPI mod for Stardew Valley that tracks your farm's financial performance across seasons. Because every gold coin counts.

## Features

- **Daily income tracking** by category: Farming, Foraging, Fishing, Mining, Other
- **Season-by-season statistics** with best day of the season
- **Item profitability analysis** — revenue vs seed/fertilizer costs
- **Legendary fish tracking** — know when you've sold a legend
- **Expense tracking** — where does all that gold go?
- **Local web dashboard** with Stardew Valley pixel art style (coming soon)
- **Google Sheets integration** — sync your stats to the cloud (coming soon)
- **PDF export** — share your farm's performance with other players (coming soon)

## Installation

1. Install [SMAPI](https://smapi.io/) (4.x+)
2. Download the latest release from the [Releases page](https://github.com/bcalvet/LaCompta/releases)
3. Extract the zip into your `Stardew Valley/Mods/` folder
4. Launch the game through SMAPI

## Requirements

- Stardew Valley 1.6.15+
- SMAPI 4.0+

## Building from Source

```bash
# Prerequisites: .NET 6 SDK, SMAPI installed
dotnet restore
dotnet build
# Mod auto-deploys to your Mods/ folder
```

See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for full dev setup.

## Documentation

- [Architecture](docs/ARCHITECTURE.md) — How the mod works
- [Contributing](docs/CONTRIBUTING.md) — Dev setup and PR guidelines
- [Troubleshooting](docs/TROUBLESHOOTING.md) — Common issues and fixes
- [Modding Notes](docs/MODDING-NOTES.md) — SMAPI tricks and game data quirks

## License

MIT
