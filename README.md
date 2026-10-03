# vtolvr-connect

Automation toolkit for **VTOL VR** + **Mod Loader**: soak-test random single-player missions to “cockpit ready for start,” then (later) a DM-style control panel.

## Goal 1 — Soak harness

| Piece | Path |
|-------|------|
| In-game mod | `src/SoakHarness` |
| Deploy | `runner/deploy.ps1` |
| Soak loop | `runner/soak.ps1` |
| LoS helpers | `runner/mod-profile.ps1` |

**Pass:** player vehicle present and flight-ready (engines not started).

### Prerequisites

- VTOL VR + Mod Loader installed via Steam
- Workshop **VTOLAPI** (`3265689427`)
- .NET 8 SDK (portable install under `%LOCALAPPDATA%\dotnet-sdk` is fine)

### Build & deploy

```powershell
.\runner\deploy.ps1
```

Copies `SoakHarness.dll` + `item.json` to:

`VTOL VR\@Mod Loader\Mods\SoakHarness\`

### Run one soak

Headset / SteamVR as required by the game, then:

```powershell
.\runner\soak.ps1 -Iterations 1
```

Results land in `%USERPROFILE%\Documents\vtolvr-connect\results\`.

### Config

See `config/soak.defaults.json` for Steam paths, userdata id, and timeouts.

## Goal 2 — DM panel (later)

Local web UI first, then Docker on the LAN host. Not started in v1.

## License

Personal / WIP — clarify before publishing mods to Workshop.
