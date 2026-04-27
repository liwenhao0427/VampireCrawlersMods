param(
    [string]$Configuration = "Release",
    [string]$GameRoot = "$PSScriptRoot\..\Vampire Crawlers"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath "$PSScriptRoot\..").Path
$gameRootPath = (Resolve-Path -LiteralPath $GameRoot).Path
$pluginDir = Join-Path $gameRootPath "BepInEx\plugins"
$pluginNames = @(
    "VampireCrawlers.RuntimeMod.dll",
    "VampireCrawlers.EnemyHealthOverlay.dll",
    "VampireCrawlers.DpsOverlay.dll",
    "VampireCrawlers.DevConsole.dll",
    "VampireCrawlers.ImageReplacer.dll"
)
$obsoletePluginNames = @(
    "VampireCrawlers.ShopMoneyButton.dll",
    "VampireCrawlers.UnlockController.dll"
)
$pluginPaths = $pluginNames | ForEach-Object {
    Join-Path $repoRoot (Join-Path "artifacts\plugins" $_)
}

dotnet build (Join-Path $repoRoot "VampireCrawlersMods.sln") -c $Configuration

foreach ($pluginPath in $pluginPaths) {
    if (-not (Test-Path -LiteralPath $pluginPath)) {
        throw "Plugin build output was not found: $pluginPath"
    }
}

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
foreach ($obsoletePluginName in $obsoletePluginNames) {
    $obsoletePluginPath = Join-Path $pluginDir $obsoletePluginName
    if (Test-Path -LiteralPath $obsoletePluginPath) {
        Remove-Item -LiteralPath $obsoletePluginPath -Force
    }
}
foreach ($pluginPath in $pluginPaths) {
    Copy-Item -LiteralPath $pluginPath -Destination $pluginDir -Force
}

$imageReplacerDir = Join-Path $pluginDir "VampireCrawlers.ImageReplacer"
$imageReplacerImagesDir = Join-Path $imageReplacerDir "images"
New-Item -ItemType Directory -Force -Path $imageReplacerImagesDir | Out-Null
$manifestExample = Join-Path $repoRoot "src\VampireCrawlers.ImageReplacer\manifest.example.txt"
Copy-Item -LiteralPath $manifestExample -Destination (Join-Path $imageReplacerDir "manifest.example.txt") -Force

Write-Host "Plugin installed to $pluginDir"
