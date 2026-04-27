param(
    [string]$GameRoot = "$PSScriptRoot\..\Vampire Crawlers",
    [string]$OutputDir = "$PSScriptRoot\..\analysis\cpp2il",
    [ValidateSet("diffable-cs", "dll_default", "dll_empty", "dll_throw_null", "dll_il_recovery", "dummydll", "isil")]
    [string]$OutputFormat = "diffable-cs",
    [string]$ForceUnityVersion = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath "$PSScriptRoot\..").Path
$cpp2il = Join-Path $repoRoot ".tools\Cpp2IL-2022.1.0-pre-release.21-Windows.exe"
$gameRootPath = (Resolve-Path -LiteralPath $GameRoot).Path

if (-not (Test-Path -LiteralPath $cpp2il)) {
    throw "Cpp2IL executable was not found: $cpp2il"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$arguments = @(
    "--game-path", $gameRootPath,
    "--exe-name", "Vampire Crawlers",
    "--output-as", $OutputFormat,
    "--output-to", $OutputDir
)

if ($ForceUnityVersion) {
    $arguments += @("--force-unity-version", $ForceUnityVersion)
}

& $cpp2il @arguments
