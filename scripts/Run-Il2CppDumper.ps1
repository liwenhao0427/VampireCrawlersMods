param(
    [string]$GameRoot = "$PSScriptRoot\..\Vampire Crawlers",
    [string]$OutputDir = "$PSScriptRoot\..\analysis\il2cppdumper"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath "$PSScriptRoot\..").Path
$dumper = Join-Path $repoRoot ".tools\Il2CppDumper-win-v6.7.46\Il2CppDumper.exe"
$gameRootPath = (Resolve-Path -LiteralPath $GameRoot).Path
$gameAssembly = Join-Path $gameRootPath "GameAssembly.dll"
$metadata = Join-Path $gameRootPath "Vampire Crawlers_Data\il2cpp_data\Metadata\global-metadata.dat"

foreach ($required in @($dumper, $gameAssembly, $metadata)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required file was not found: $required"
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$output = & $dumper $gameAssembly $metadata $OutputDir 2>&1
$exitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
$output | Write-Host

$dumpFile = Join-Path $OutputDir "dump.cs"
$scriptFile = Join-Path $OutputDir "script.json"
if (-not (Test-Path -LiteralPath $dumpFile) -or -not (Test-Path -LiteralPath $scriptFile)) {
    throw "Il2CppDumper did not produce expected outputs in $OutputDir"
}

if ($exitCode -ne 0) {
    Write-Warning "Il2CppDumper exited with code $exitCode after producing outputs. Self-contained builds can throw at final ReadKey in non-interactive shells."
}
