# Soak loop: deploy mod, launch VTOL VR, wait for result JSON or timeout.
param(
    [int]$Iterations = 1,
    [int]$TimeoutSec = 480,
    [int]$LaunchDelaySec = 15,
    [int]$Seed,
    [switch]$SkipDeploy,
    [switch]$NoLaunch,
    [switch]$RestoreLoSWhenDone,
    [string]$ConfigPath
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

$defaultsPath = if ($ConfigPath) { $ConfigPath } else { Join-Path $root "config\soak.defaults.json" }
$cfg = Get-Content -LiteralPath $defaultsPath -Raw | ConvertFrom-Json
if ($Iterations -eq 1 -and $cfg.iterations) { } # keep param default unless user passed; param wins
$vtolPath = $cfg.vtolVrPath
$resultsDir = if ($cfg.resultsDir) { $cfg.resultsDir } else { Join-Path $env:USERPROFILE "Documents\vtolvr-connect\results" }
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

$env:VTOLVR_CONNECT_RESULTS = $resultsDir

$backup = & (Join-Path $PSScriptRoot "mod-profile.ps1") -Backup -UserdataId $cfg.steamUserdataId
& (Join-Path $PSScriptRoot "mod-profile.ps1") -EnsureApiAndLocalSoak -UserdataId $cfg.steamUserdataId -LocalItemKey $cfg.localModName

if (-not $SkipDeploy) {
    & (Join-Path $PSScriptRoot "deploy.ps1") -VtolVrPath $vtolPath
}

function Get-VtolProcess {
    Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -match '^(VTOLVR|VTOL VR)$' -or $_.Path -like "*VTOL VR*"
    }
}

function Stop-Vtol {
    Get-VtolProcess | ForEach-Object {
        Write-Host "Stopping $($_.ProcessName) pid=$($_.Id)"
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 2
}

$summary = @()
for ($i = 1; $i -le $Iterations; $i++) {
    $runId = Get-Date -Format "yyyyMMdd-HHmmss" 
    $runId = "$runId-i$i"
    $runSeed = if ($PSBoundParameters.ContainsKey("Seed")) { $Seed + $i - 1 } else { Get-Random }
    $env:VTOLVR_CONNECT_RUN_ID = $runId
    $env:VTOLVR_CONNECT_SEED = "$runSeed"
    $resultPath = Join-Path $resultsDir "$runId.json"

    Write-Host ""
    Write-Host "=== Soak $i / $Iterations runId=$runId seed=$runSeed ==="

    Stop-Vtol

    if (-not $NoLaunch) {
        $uri = "steam://run/$($cfg.steamAppId)"
        Write-Host "Launching $uri"
        Start-Process $uri
        Start-Sleep -Seconds $LaunchDelaySec
    } else {
        Write-Host "NoLaunch: waiting for existing game + result $resultPath"
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $result = $null
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $resultPath) {
            $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            break
        }
        # Also accept any newer result if game used fallback run id
        Start-Sleep -Seconds 2
    }

    if (-not $result) {
        $result = [pscustomobject]@{
            runId  = $runId
            status = "timeout"
            stage  = "runner"
            detail = "no result file within ${TimeoutSec}s"
            seed   = $runSeed
            utc    = (Get-Date).ToUniversalTime().ToString("o")
        }
        ($result | ConvertTo-Json) | Set-Content -LiteralPath $resultPath -Encoding UTF8
    }

    # Archive logs
    $logDir = Join-Path $resultsDir $runId
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $playerLog = Join-Path $env:USERPROFILE "AppData\LocalLow\Boundless Studios\VTOL VR\Player.log"
    if (Test-Path $playerLog) {
        Copy-Item $playerLog (Join-Path $logDir "Player.log") -Force -ErrorAction SilentlyContinue
    }
    $mlLogs = Join-Path $vtolPath "@Mod Loader\Logs"
    if (Test-Path $mlLogs) {
        Copy-Item $mlLogs (Join-Path $logDir "ModLoaderLogs") -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host ("Result: {0} stage={1} detail={2}" -f $result.status, $result.stage, $result.detail)
    $summary += $result

    Stop-Vtol
    Start-Sleep -Seconds 3
}

Write-Host ""
Write-Host "=== Summary ==="
$summary | Format-Table runId, status, stage, seed -AutoSize
$pass = @($summary | Where-Object { $_.status -eq "pass" }).Count
Write-Host "Pass $pass / $($summary.Count)"

if ($RestoreLoSWhenDone -and $backup) {
    & (Join-Path $PSScriptRoot "mod-profile.ps1") -Restore -BackupPath $backup
}
