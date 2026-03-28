# Contributing to LaCompta

## Development Setup

### Prerequisites
- .NET 6 SDK
- Stardew Valley (v1.6.15+)
- SMAPI 4.x
- sqlite3 CLI (for seed data)

### Build & Test
1. Clone the repo
2. Run `dotnet restore`
3. Run `dotnet build`
4. The mod auto-deploys to your Stardew Valley Mods/ folder via ModBuildConfig
5. Launch Stardew Valley through SMAPI

### Scripts

| Script | Purpose |
|--------|---------|
| `./scripts/test-mod.sh` | Build, deploy, launch game, tail log |
| `./scripts/test-mod.sh --test` | Build, launch, auto-run integration tests |
| `./scripts/test-mod.sh --log` | Tail SMAPI log for LaCompta entries |
| `./scripts/seed-data.sh` | Populate SQLite DB with realistic test data (3 years, 336 days, legendaries) |
| `python3 ./scripts/dev-server.py` | **Standalone dev server** — no game needed! Seeds DB + serves dashboard on port 5555 |
| `python3 ./scripts/dev-server.py --no-seed` | Dev server without re-seeding (use existing data) |
| `python3 ./scripts/dev-server.py --port 8080` | Dev server on custom port |

### SMAPI Console Commands

Type these in the SMAPI console while the game is running:

| Command | Purpose |
|---------|---------|
| `lacompta_test` | Run 6 integration tests (farming, fishing, mining, legendary fish, expenses, classifier) |
| `lacompta_status` | Show DB stats (seasons tracked, legendary fish, per-season breakdown) |
| `lacompta_open` | Open the web dashboard in your default browser |

### Frontend Development

**Standalone dev server (recommended):**
```bash
python3 ./scripts/dev-server.py
# Dashboard at http://localhost:5555/ — no game needed!
```

This seeds a separate `dev-lacompta.db` (doesn't touch your game data) and serves the dashboard with mock API endpoints.

**With the game:**
1. Run `dotnet build` to deploy the mod
2. Launch Stardew Valley, load a save
3. Open `http://localhost:5555/`

The seed data includes:
- 3 full years (12 seasons, 336 daily records)
- Realistic income progression (early → mid → late game)
- 32+ item transactions (parsnips to ancient fruit wine)
- 16 fish records including 10 legendary fish

### Project Structure
See CLAUDE.md for directory layout.

### Pull Requests
- One feature per PR
- Branch naming: `phase/XX-name`
- Include description of what changed and why
- Test in-game before submitting (or use `lacompta_test`)
