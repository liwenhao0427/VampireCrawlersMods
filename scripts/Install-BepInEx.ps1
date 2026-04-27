param(
    [string]$GameRoot = "$PSScriptRoot\..\Vampire Crawlers",
    [string]$BepInExSource = "$PSScriptRoot\..\.tools\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755+3fab71a"
)

$ErrorActionPreference = "Stop"

$gameRootPath = (Resolve-Path -LiteralPath $GameRoot).Path
$sourcePath = (Resolve-Path -LiteralPath $BepInExSource).Path

foreach ($required in @("BepInEx", "dotnet", "doorstop_config.ini", "winhttp.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $required))) {
        throw "BepInEx source is missing '$required': $sourcePath"
    }
}

Copy-Item -LiteralPath (Join-Path $sourcePath "BepInEx") -Destination $gameRootPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $sourcePath "dotnet") -Destination $gameRootPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $sourcePath ".doorstop_version") -Destination $gameRootPath -Force
Copy-Item -LiteralPath (Join-Path $sourcePath "doorstop_config.ini") -Destination $gameRootPath -Force
Copy-Item -LiteralPath (Join-Path $sourcePath "winhttp.dll") -Destination $gameRootPath -Force
if (Test-Path -LiteralPath (Join-Path $sourcePath "changelog.txt")) {
    Copy-Item -LiteralPath (Join-Path $sourcePath "changelog.txt") -Destination $gameRootPath -Force
}

Write-Host "BepInEx installed to $gameRootPath"
