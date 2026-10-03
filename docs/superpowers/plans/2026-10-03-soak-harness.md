# Soak Harness Implementation Plan

> **For agentic workers:** Implement task-by-task. This plan assumes Goal 1 only.

**Goal:** Ship a local Mod Loader mod + PowerShell runner that soak-tests random SP missions to “cockpit ready for start.”

**Architecture:** `SoakHarness` DLL (Unity/Mono, Mod Loader) + `runner/soak.ps1` (orchestration).

## File map

| Path | Responsibility |
|------|----------------|
| `src/SoakHarness/SoakHarness.csproj` | netstandard2.0 build; refs to Framework, VTOLAPI, Unity, Assembly-CSharp |
| `src/SoakHarness/Main.cs` | `VtolMod` entry, SceneLoaded hook |
| `src/SoakHarness/SoakController.cs` | State machine + UI automation |
| `src/SoakHarness/ResultWriter.cs` | Atomic JSON result write |
| `src/SoakHarness/item.json` | Mod metadata |
| `runner/deploy.ps1` | Build + copy to `@Mod Loader\Mods\SoakHarness` |
| `runner/soak.ps1` | Launch / poll / timeout / archive |
| `runner/mod-profile.ps1` | Load-on-Start backup/restore/patch |
| `config/soak.defaults.json` | Paths, timeouts, iteration count |
| `README.md` | Build/run instructions |

## Tasks

### Task 1: Scaffold + design docs

- [x] Spec + plan under `docs/superpowers/`
- [x] `.gitignore`, README skeleton

### Task 2: SoakHarness mod

- [x] csproj with Steam HintPaths + env overrides
- [x] Main + controller state machine:
  - ReadyRoom → pick vehicle/mission
  - VehicleConfiguration → optional LoadRecommended → LaunchMission
  - Briefing → FlyButton
  - Flight map → wait isFlightReady / player GO → PASS
- [x] ResultWriter to `%USERPROFILE%\Documents\vtolvr-connect\results\` (and optional env override)
- [x] Timeout / exception → FAIL JSON

### Task 3: Runner scripts

- [x] `deploy.ps1` builds with portable or system `dotnet`
- [x] `mod-profile.ps1` backs up LoS; ensures API + local soak
- [x] `soak.ps1` loops: deploy → launch → poll → kill → archive

### Task 4: Verify build + GitHub

- [x] `dotnet build` succeeds
- [x] Deploy folder created
- [x] `gh repo create` + push → https://github.com/EyWuddup/vtolvr-connect

### Task 5: First live soak (manual kick)

- User starts SteamVR / headset as needed
- Run `.\runner\soak.ps1 -Iterations 1`
- Confirm result JSON

## Test plan

1. Build produces `SoakHarness.dll`
2. Deploy copies into local Mods
3. Single iteration writes pass or structured fail (not silent hang beyond timeout)
4. Process kill on timeout leaves LoS backup restorable
