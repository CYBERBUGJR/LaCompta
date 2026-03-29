# Troubleshooting

## Common Issues

*This file is updated as issues are discovered during development.*

### Build Issues

**CS9057 Analyzer version warning**
The ModBuildConfig analyzer targets a newer compiler than .NET 6 SDK ships. This is harmless — the analyzer still works, just warns about version mismatch. Safe to ignore.

### SMAPI Issues

**"DLL couldn't be loaded" — Missing third-party dependencies**
If SMAPI says your mod's DLL couldn't be loaded, you likely have NuGet dependencies (like Microsoft.Data.Sqlite) that aren't being bundled with the mod.

**Fix:** Add `<BundleExtraAssemblies>ThirdParty, System</BundleExtraAssemblies>` to your csproj `<PropertyGroup>`.

- `ThirdParty` bundles non-Microsoft NuGet packages (e.g., SQLitePCLRaw)
- `System` bundles Microsoft packages that aren't part of the game runtime (e.g., Microsoft.Data.Sqlite)
- Valid values: `None`, `Game`, `System`, `ThirdParty` (comma-separated)
- `All` is NOT a valid value despite what you might expect

Without this, only your mod's DLL and manifest.json get deployed — dependency DLLs stay in bin/ and SMAPI's assembly rewriter fails with `AssemblyResolutionException`.

**SMAPI installer on Linux**
The SMAPI installer can be run non-interactively by piping inputs: `printf '1\n1\n1\n' | ./install\ on\ Linux.sh`. The three inputs are: color scheme, game path selection (auto-detected), and install action.

### Runtime Issues

**"No longer compatible" after adding NuGet packages**
Some NuGet packages (e.g., QuestPDF, SkiaSharp) include native DLLs that break SMAPI's assembly rewriter. Stick to pure .NET packages. If SMAPI marks your mod as incompatible after adding a package:
1. Remove the package from csproj
2. `rm -rf bin/ obj/` for a clean rebuild
3. Clean old DLLs from the deploy folder: `Stardew Valley/Mods/LaCompta/`
4. Rebuild with `dotnet build`

**Dashboard shows "Loading..." or empty charts**
- Check the SMAPI console for errors
- Try opening `http://localhost:5555/api/seasons` directly — if empty, no data is tracked yet
- Run `lacompta_test` to create test data
- If data exists but dashboard is empty, hard-refresh the browser (Ctrl+Shift+R)

**Excel export downloads a JSON file instead of XLSX**
- This means no data was found for your player. The mod falls back to all players' data, but if the DB is empty, it returns an error JSON
- Play some days and ship items to generate data first
