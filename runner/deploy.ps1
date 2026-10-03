# Build SoakHarness and copy into VTOL VR local Mods folder.
param(
    [string]$VtolVrPath = "C:\Program Files (x86)\Steam\steamapps\common\VTOL VR",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

$csproj = Join-Path $root "src\SoakHarness\SoakHarness.csproj"
$dotnetCandidates = @(
    (Join-Path $env:LOCALAPPDATA "dotnet-sdk\dotnet.exe"),
    "C:\Program Files\dotnet\dotnet.exe",
    "dotnet"
)
$dotnet = $null
foreach ($c in $dotnetCandidates) {
    if ($c -eq "dotnet") {
        $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($cmd) { $dotnet = $cmd.Source; break }
    } elseif (Test-Path $c) {
        $dotnet = $c
        break
    }
}
if (-not $dotnet) { throw "dotnet SDK not found. Install .NET 8 SDK or use %LOCALAPPDATA%\dotnet-sdk" }

Write-Host "Using $dotnet"
& $dotnet build $csproj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "build failed: $LASTEXITCODE" }

$outDir = Join-Path $root "build\SoakHarness"
$dest = Join-Path $VtolVrPath "@Mod Loader\Mods\SoakHarness"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $outDir "SoakHarness.dll") $dest -Force
Copy-Item (Join-Path $outDir "item.json") $dest -Force
if (Test-Path (Join-Path $outDir "SoakHarness.pdb")) {
    Copy-Item (Join-Path $outDir "SoakHarness.pdb") $dest -Force
}

Write-Host "Deployed -> $dest"
Get-ChildItem $dest | Format-Table Name, Length, LastWriteTime
