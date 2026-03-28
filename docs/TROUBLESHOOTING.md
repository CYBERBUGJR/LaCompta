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
- TBD
