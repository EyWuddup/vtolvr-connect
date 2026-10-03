# Load-on-Start helpers for VTOL VR Mod Loader (app 3018410).
param(
    [string]$UserdataId = "57417170",
    [string]$SteamRoot = "C:\Program Files (x86)\Steam",
    [switch]$Backup,
    [switch]$Restore,
    [string]$BackupPath,
    [switch]$EnsureApiAndLocalSoak,
    [string]$LocalItemKey = "SoakHarness"
)

$ErrorActionPreference = "Stop"

function Get-LoSPath {
    param([string]$UserdataId, [string]$SteamRoot)
    Join-Path $SteamRoot "userdata\$UserdataId\3018410\remote\Load on Start"
}

$losPath = Get-LoSPath -UserdataId $UserdataId -SteamRoot $SteamRoot
if (-not (Test-Path $losPath)) {
    throw "Load on Start not found: $losPath"
}

if ($Backup) {
    if (-not $BackupPath) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $BackupPath = Join-Path $env:USERPROFILE "Documents\VTOLVR-mod-fix-backup-$stamp\Load on Start"
    }
    $dir = Split-Path $BackupPath -Parent
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Copy-Item -LiteralPath $losPath -Destination $BackupPath -Force
    Write-Host "Backed up LoS -> $BackupPath"
    return $BackupPath
}

if ($Restore) {
    if (-not $BackupPath -or -not (Test-Path -LiteralPath $BackupPath)) {
        throw "Restore requires -BackupPath to an existing file"
    }
    Copy-Item -LiteralPath $BackupPath -Destination $losPath -Force
    Write-Host "Restored LoS from $BackupPath"
    return
}

if ($EnsureApiAndLocalSoak) {
    $raw = Get-Content -LiteralPath $losPath -Raw -Encoding UTF8
    $json = $raw | ConvertFrom-Json
    if (-not $json.WorkshopItems) {
        $json | Add-Member -NotePropertyName WorkshopItems -NotePropertyValue ([pscustomobject]@{}) -Force
    }
    if (-not $json.LocalItems) {
        $json | Add-Member -NotePropertyName LocalItems -NotePropertyValue ([pscustomobject]@{}) -Force
    }

    # VTOLAPI workshop id
    $json.WorkshopItems | Add-Member -NotePropertyName "3265689427" -NotePropertyValue $true -Force

    # Local soak harness (folder name under @Mod Loader\Mods)
    $json.LocalItems | Add-Member -NotePropertyName $LocalItemKey -NotePropertyValue $true -Force

    $out = $json | ConvertTo-Json -Compress -Depth 8
    Set-Content -LiteralPath $losPath -Value $out -Encoding UTF8
    Write-Host "Patched LoS: VTOLAPI + LocalItems.$LocalItemKey = true"
}
