# Crusader Trial Manager 1.0 — source

Source for the Windows application and BepInEx plugin that customize opponents, AI profiles and castles in Stronghold Crusader: Definitive Edition's original singleplayer trails.

This repository includes the C# source, application icon, build script and installation/removal scripts. It does not include compiled binaries, game assemblies, BepInEx, Workshop assets or personal settings.

## Build requirements

- Windows x64 with .NET Framework 4.5 or later.
- The .NET Framework C# compiler at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. The build script uses this compiler directly; Visual Studio and the modern .NET SDK are not required.
- A legally installed copy of Stronghold Crusader: Definitive Edition, Steam build **24816905 / V2.8.2**.
- The official [SHCDE BepInEx loader](https://gitlab.com/rawra-stronghold-crusader/shcde-bepinex/-/releases), loader release **1.1.0** with BepInEx **5.4.23.5**. Obtain the loader package, not the source archive.

The game and loader assemblies are referenced locally during compilation and are not redistributed here. Script Extender is not required.

## Build steps

1. Download or clone the source and extract it to a local folder.
2. Install the SHCDE BepInEx loader into your game folder, or extract its package to a separate folder.
3. Open PowerShell in the source folder.
4. Run the following, replacing the example game path with your installation:

```powershell
.\Build.ps1 -GamePath 'C:\Games\Stronghold Crusader Definitive Edition'
```

If the loader is in a separate directory, provide its root folder as well:

```powershell
.\Build.ps1 -GamePath 'C:\Games\Stronghold Crusader Definitive Edition' -LoaderPath 'C:\Tools\SHCDE-BepInEx'
```

If your local PowerShell execution policy blocks the script, it can be invoked with a process-scoped policy override:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -GamePath 'C:\Games\Stronghold Crusader Definitive Edition'
```

This does not change the machine's stored execution policy. The build script does not install the mod or download dependencies.

Successful compilation writes these files beside `Build.ps1`:

- `CrusaderTrialManager-1.0.exe`
- `CrusaderTrailPlugin.dll`

To compile only the plugin, add `-PluginOnly`.

## Install a build

Close the game. Keep the EXE, DLL, `Install.ps1` and `Uninstall.ps1` together, then open the app, choose the game folder and click **Install mod from this package**. Alternatively:

```powershell
.\Install.ps1 -GamePath 'C:\Games\Stronghold Crusader Definitive Edition'
```

The installer checks the game build and tracked plugin hash, backs up an existing tracked plugin, copies the new plugin and updates its installation manifest. It preserves mission profiles. The game must already have the official loader installed.

## Source layout

| File | Purpose |
| --- | --- |
| `source/Manager.cs` | Windows Forms interface and per-mission selection |
| `source/Theme.cs` | Drawing, colors and styled controls |
| `source/Profile.cs` | XML settings and validation |
| `source/Catalog.cs` | Verified mission rosters, lord packs and asset discovery/import |
| `source/Plugin.cs` | BepInEx/Harmony game integration |
| `source/ScopedIntPatch.cs` | Validated temporary four-byte memory patches and restoration |
| `source/AssemblyInfo.cs` | Product and version metadata |

See [REVIEW.md](REVIEW.md) for runtime behavior relevant to security review and [RELEASE-SHA256.txt](RELEASE-SHA256.txt) for hashes of the submitted release package.

## Build verification

The source package was rebuilt on 9 October 2026 using the compiler and local references listed above. Its type definitions, field definitions and initialization data, and method instructions matched the release EXE and DLL after normalizing compiler-generated private GUIDs.

This older compiler does not produce byte-for-byte deterministic binaries. Module identifiers, compiler-generated private identifiers and binary metadata can differ between compilations, so a rebuild's SHA-256 hash is not expected to equal the published binary hash. The release hashes identify the submitted files; they are not a malware scan result.

The submitted release ZIP was preserved without changes while preparing this source package.
