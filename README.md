# Vampire Crawlers Runtime Mod

Runtime mod scaffold for the Windows x64 IL2CPP build of Vampire Crawlers.

## Tool Layout

Expected local-only tools:

- `.tools\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755+3fab71a\`
- `.tools\Cpp2IL-2022.1.0-pre-release.21-Windows.exe`
- `.tools\Il2CppDumper-win-v6.7.46\Il2CppDumper.exe`
- `.tools\AssetStudio.net472.v0.16.47\AssetStudioGUI.exe`

These are ignored by Git.

## Build

```powershell
dotnet build .\VampireCrawlersMods.sln -c Release
```

Plugin DLLs are written to `artifacts\plugins\`. Feature plugins are split by responsibility:

- `VampireCrawlers.RuntimeMod.dll`: BaseLib plugin with compatibility checks, shared constants, shared overlay UI helpers, and a base lifecycle probe.
- `VampireCrawlers.EnemyHealthOverlay.dll`: enemy and front-row health overlays.
- `VampireCrawlers.DpsOverlay.dll`: DPS overlay.
- `VampireCrawlers.DevConsole.dll`: developer console opened with `~`.
- `VampireCrawlers.ImageReplacer.dll`: manifest-driven runtime card and gem image replacement from local PNG files.

## Install

```powershell
.\scripts\Install-BepInEx.ps1
.\scripts\Install-Plugin.ps1
```

Launch `Vampire Crawlers.exe` once after installing BepInEx so it can generate its runtime folders, `BepInEx\LogOutput.log`, and IL2CPP interop cache.

Image replacement checks `BepInEx\plugins\VampireCrawlers.ImageReplacer\images\` for PNG files whose file name matches the original Unity Sprite name. Export references with `.\scripts\Export-ImageResources.ps1`, then place a replacement such as `images\Whip2.png` to override the Sprite named `Whip2`. The plugin logs observed card/gem Sprite names to `BepInEx\LogOutput.log` so the exact replacement filename can be confirmed in-game. `manifest.txt` remains available for precise card/gem mappings.

## Reverse Engineering

```powershell
.\scripts\Run-Cpp2IL.ps1
.\scripts\Run-Il2CppDumper.ps1
.\scripts\Export-ImageResources.ps1
```

Use `docs\reverse-map.md` to record confirmed type and method targets before turning placeholder probes into real gameplay patches.
