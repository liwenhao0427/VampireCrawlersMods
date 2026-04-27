param(
    [string]$GameRoot = "$PSScriptRoot\..\Vampire Crawlers",
    [string]$OutputPath = "$PSScriptRoot\..\analysis\image-resources",
    [string]$AssetStudioCli = "$PSScriptRoot\..\.tools\AssetStudio-net8.0-win\AssetStudio.CLI.exe",
    [ValidateSet("Addressables", "Data", "All")]
    [string]$Source = "Addressables"
)

$ErrorActionPreference = "Stop"

$gameRootPath = (Resolve-Path -LiteralPath $GameRoot).Path
$assetStudioCliPath = (Resolve-Path -LiteralPath $AssetStudioCli).Path
$outputFullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)

switch ($Source) {
    "Addressables" {
        $inputPath = Join-Path $gameRootPath "Vampire Crawlers_Data\StreamingAssets\aa\StandaloneWindows64"
    }
    "Data" {
        $inputPath = Join-Path $gameRootPath "Vampire Crawlers_Data"
    }
    "All" {
        $inputPath = Join-Path $gameRootPath "Vampire Crawlers_Data"
    }
}

if (-not (Test-Path -LiteralPath $inputPath)) {
    throw "Input path was not found: $inputPath"
}

New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

& $assetStudioCliPath `
    $inputPath `
    $outputFullPath `
    --game Normal `
    --types Sprite `
    --group_assets ByType `
    --export_type Convert `
    --image_format Png

Write-Host "Sprite images exported to $outputFullPath"
