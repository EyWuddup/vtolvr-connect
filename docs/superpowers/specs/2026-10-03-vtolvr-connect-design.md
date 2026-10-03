# vtolvr-connect — Design Spec

**Date:** 2026-10-03  
**Status:** Approved (conversation); Goal 1 soak harness first  
**Owner:** EyWuddup

## Problem

VTOL VR + Mod Loader Workshop setups crash often (AssetBundle loads on the wrong thread after mid-session Steam downloads, missing DLLs, bad dependency chains). Manually finding a stable Load-on-Start set is slow. We need automated soak testing that launches missions and reports whether the cockpit reaches “ready for start procedure.”

## Goals

### Goal 1 (this spec)

Automated soak / load testing:

- Launch VTOL VR with a controlled mod profile
- Randomize jet / mission / (best-effort) loadout
- Drive the UI into a single-player mission until the player is in the cockpit ready to begin the start procedure (engines off)
- Record pass/fail/timeout/crash
- Support enabling/disabling Workshop items between runs for stability bisect

### Goal 2 (later, out of scope for v1)

DM-style web panel (local first, then Docker on `192.168.1.118`) for mission control. Same repo; separate milestone.

## Approach

**In-game Mod Loader mod + PowerShell runner (Approach A).**

| Piece | Role |
|-------|------|
| `SoakHarness` mod | Subscribes to `VTOLAPI.VTAPI.SceneLoaded`, drives Ready Room → vehicle config → briefing → flight, writes a JSON result file |
| `runner/soak.ps1` | Deploys mod, mutates Load-on-Start, launches game via Steam, polls result / process exit, archives logs |
| Results dir | `results/<run-id>.json` consumed by the runner |

Pass criteria: player vehicle exists and `FlightSceneManager.isFlightReady` (or equivalent) with no engine-start automation.

## Game API surface (v1)

- `VTOLAPI.VTAPI.SceneLoaded` / `VTScenes`
- `PilotSelectUI` — vehicle/pilot selection
- `CampaignSelectorUI.SelectScenario` / `StartMission`
- `VehicleConfigSceneSetup.LaunchMission` (+ optional `LoadoutConfigurator.LoadRecommended`)
- `MissionBriefingUI.FlyButton`
- `FlightSceneManager.instance` / `isFlightReady` / `VTAPI.GetPlayersVehicleGameObject()`

## Mod packaging

- Local mod under `VTOL VR\@Mod Loader\Mods\SoakHarness\`
- `item.json` with `AllowLoadOnStart: true`
- Depends on Workshop VTOLAPI (`3265689427`)
- `[ItemId("eywuddup.vtolvr-connect.soak")]` on `VtolMod` subclass

## Runner behavior

1. Build + copy DLL/`item.json` to local Mods folder
2. Backup and optionally patch `Steam\userdata\<id>\3018410\remote\Load on Start`
3. Ensure VTOLAPI + SoakHarness load on start; optional profile for Workshop items
4. `steam://run/667970` (or configured app id)
5. Poll `results/` and game process; timeout → fail + kill
6. Copy `Player.log` / Mod Loader logs into `results/`
7. Loop N iterations with new seeds

## Non-goals (v1)

- Multiplayer / DM panel
- Engine start / taxi / combat automation
- Publishing SoakHarness to Workshop (local-only is fine)
- Repairing third-party crashy mods (only detect and bisect)

## Risks

- Ready Room UI timing races — mitigate with coroutines + retries
- Custom aircraft campaigns may lack scenarios — skip / fail with clear reason
- Broken system `dotnet` install — use portable SDK under `%LOCALAPPDATA%\dotnet-sdk` for builds
- Mid-session Workshop downloads — runner should not subscribe new items during a soak loop

## Success metric

Unattended loop of ≥10 random missions with clear pass/fail artifacts, without requiring VR interaction after the game window is up (headset may still be required by the game).
