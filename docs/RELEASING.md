# Releasing LaCompta

Release artifacts (the `LaCompta-x.y.z.zip` users download) are built **locally** on
a developer machine that has Stardew Valley installed, then uploaded to the GitHub
Release. CI does not produce release artifacts.

## Why local builds?

CI runs on Linux without the real Stardew Valley DLLs. The CI stubs under
`.github/stubs/` are minimal mocks of the SMAPI / Stardew API surface used by the
mod, written just well enough to make the project compile.

If we shipped a CI-built zip, the mod's compiled IL would reference stub types
(wrong namespaces, field-vs-property mismatches, missing members). At runtime,
SMAPI's assembly rewriter would detect those broken references and reject the mod
with **"no longer compatible. Please check for a new version at https://smapi.io/mods"**
even though the mod itself is fine.

A local build references the real `Stardew Valley.dll`, `StardewModdingAPI.dll`,
and `MonoGame.Framework.dll` from the installed game, so the IL matches what
SMAPI actually loads. ModBuildConfig (the `Pathoschild.Stardew.ModBuildConfig`
NuGet package) handles the packaging automatically.

## Prerequisites

- Windows, macOS, or Linux machine with:
  - [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
  - [Stardew Valley](https://store.steampowered.com/app/413150/Stardew_Valley/) installed via Steam (or GOG)
  - [SMAPI](https://smapi.io/) installed in the game folder
  - [GitHub CLI (`gh`)](https://cli.github.com/) authenticated against this repo (`gh auth login`)

ModBuildConfig auto-detects the game folder on standard Steam installs. If yours
is non-standard, set the `GamePath` environment variable or create
`LaCompta/LaCompta.csproj.user` with:

```xml
<Project>
  <PropertyGroup>
    <GamePath>C:\Path\To\Stardew Valley</GamePath>
  </PropertyGroup>
</Project>
```

## Release steps

### 1. Bump the version

Edit `LaCompta/manifest.json` and bump the `Version` field following [SemVer](https://semver.org/):

```json
{
  "Version": "0.1.3"
}
```

Optionally update `CHANGELOG.md` with the changes since the last release.

### 2. Commit and push

```bash
git add LaCompta/manifest.json CHANGELOG.md
git commit -m "chore: release v0.1.3"
git push origin main
```

### 3. Tag and push the tag

```bash
git tag -a v0.1.3 -m "Release v0.1.3"
git push origin v0.1.3
```

Pushing the tag triggers `.github/workflows/release.yml`, which creates a GitHub
Release shell (page + auto-generated notes) but **no asset yet**.

### 4. Build the mod locally

From the repo root:

```bash
dotnet build LaCompta/LaCompta.csproj --configuration Release
```

ModBuildConfig will:

- Compile against the real game/SMAPI DLLs from `$(GamePath)`.
- Bundle all third-party dependencies (ClosedXML, Microsoft.Data.Sqlite, etc.)
  via `BundleExtraAssemblies=ThirdParty,System` declared in the csproj.
- Bundle the native SQLite binaries for Windows/Linux/macOS (declared as
  `<None Include …>` in the csproj).
- Copy the build output to `$(GamePath)/Mods/LaCompta/` for local testing.
- Produce a release zip at:
  - **Windows:** `LaCompta\bin\Release\LaCompta 0.1.3.zip`
  - **macOS / Linux:** `LaCompta/bin/Release/LaCompta 0.1.3.zip`

### 5. Smoke-test the build locally

Before uploading, verify the mod loads cleanly in your local game:

1. Launch Stardew Valley via SMAPI.
2. Open the SMAPI log: `%AppData%\StardewValley\ErrorLogs\SMAPI-latest.txt` (Windows) or `~/.config/StardewValley/ErrorLogs/SMAPI-latest.txt` (Linux/macOS).
3. Confirm:
   - `[SMAPI] LaCompta 0.1.3 by bcalvet …` appears in the loaded mods list.
   - **No** `Could not load file or assembly`, `FileNotFoundException`, `DllNotFoundException`, `Unable to load DLL 'e_sqlite3'`.
   - **No** `Skipped LaCompta because …`.
4. Load a save, play a day, confirm the dashboard at `http://localhost:5555/` works and the Excel export produces a valid `.xlsx`.

### 6. Inspect the zip contents

```bash
unzip -l "LaCompta/bin/Release/LaCompta 0.1.3.zip"
```

The zip must contain at minimum:

- `LaCompta/manifest.json`
- `LaCompta/LaCompta.dll`
- `LaCompta/e_sqlite3.dll`, `LaCompta/libe_sqlite3.so`, `LaCompta/libe_sqlite3.dylib` (the three native binaries)
- `LaCompta/Microsoft.Data.Sqlite.dll`, `LaCompta/SQLitePCLRaw.core.dll`, `LaCompta/SQLitePCLRaw.provider.e_sqlite3.dll`, `LaCompta/SQLitePCLRaw.batteries_v2.dll`
- `LaCompta/ClosedXML.dll`, `LaCompta/DocumentFormat.OpenXml.dll`, plus ClosedXML's transitive dependencies (`ExcelNumberFormat.dll`, `RBush.dll`, `SixLabors.Fonts.dll`, `XLParser.dll`, `Irony.dll`)
- `LaCompta/Assets/...` (web dashboard files)

The zip must **not** contain `Stardew Valley.dll`, `StardewModdingAPI.dll`, `MonoGame.Framework.dll`, or any of the stub assemblies — ModBuildConfig excludes those automatically.

### 7. Upload the zip to the GitHub Release

```bash
gh release upload v0.1.3 "LaCompta/bin/Release/LaCompta 0.1.3.zip"
```

Optionally rename the asset to match the convention `LaCompta-0.1.3.zip` (without
the space) before uploading:

```bash
cp "LaCompta/bin/Release/LaCompta 0.1.3.zip" "LaCompta-0.1.3.zip"
gh release upload v0.1.3 "LaCompta-0.1.3.zip"
```

### 8. Verify the published release

1. Open `https://github.com/CYBERBUGJR/LaCompta/releases/tag/v0.1.3`.
2. Download the zip from the release page in a browser (do not reuse the local file).
3. Run the [fresh-install test protocol](#fresh-install-test-protocol) below.

## Fresh-install test protocol

Reproduce a fresh user's experience to catch missing-DLL and packaging issues
before announcing the release. Ideally use a clean Windows VM or a secondary
user account.

1. Install Steam → Stardew Valley (default install path).
2. Launch the game once via Steam, then quit.
3. Install [SMAPI](https://smapi.io/) using the official installer.
4. Launch via SMAPI, confirm `Loaded 0 mods` in the log, quit.
5. Download the **published** `LaCompta-0.1.3.zip` from the GitHub Releases page.
6. Extract it, copy the `LaCompta/` folder into `Stardew Valley/Mods/`.
7. Launch via SMAPI.
8. In `%AppData%\StardewValley\ErrorLogs\SMAPI-latest.txt` confirm:
   - `[SMAPI] LaCompta 0.1.3 by bcalvet …` is in the loaded mods list.
   - No `Skipped LaCompta` line.
   - No `DllNotFoundException`, `FileNotFoundException`, `Could not load file or assembly`.
9. Load a save, play through one in-game day, quit.
10. Reopen the game, confirm the previous day's data is persisted.
11. Open the dashboard at `http://localhost:5555/`, verify all five pages load and the Excel export works.

If anything fails, **delete the GitHub release and tag**, fix the bug, bump to
the next patch version (e.g., `v0.1.4`), and start over from step 1.

```bash
gh release delete v0.1.3 --yes
git tag -d v0.1.3
git push origin :refs/tags/v0.1.3
```

## Troubleshooting

### `dotnet build` can't find the game

Set `GamePath` in `LaCompta/LaCompta.csproj.user` (see [Prerequisites](#prerequisites)).

### Build succeeds but the zip is missing dependencies

Make sure you built `--configuration Release`, not Debug. ModBuildConfig only
produces the zip in Release configuration.

### SMAPI rejects the mod as "no longer compatible"

This usually means the mod was built against the CI stubs, not the real game
DLLs. Verify that the build log shows `Found Stardew Valley at <real path>`
(not `/tmp/stardew/...`) and that `LaCompta/obj/Release/net6.0/refint/` resolves
references against your installed game, not the stub output.

### `Microsoft.Data.Sqlite` throws `Unable to load DLL 'e_sqlite3'`

The native SQLite binary for the user's OS is missing from the zip. Confirm the
three `<None Include="$(PkgSQLitePCLRaw_lib_e_sqlite3)\runtimes\…native\…">`
items in `LaCompta/LaCompta.csproj` are present and that the corresponding files
exist under `~/.nuget/packages/sqlitepclraw.lib.e_sqlite3/2.1.6/runtimes/`.
