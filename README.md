# TH06 New Classic Practice Helper v3.2

English | [简体中文](README.zh-CN.md)

An experimental single-player practice helper for **TH06 New Classic**, running on Windows x64. All resource locks and gameplay modes are disabled by default. It is intended for practice and exploration, not as a replacement for an unassisted clear.

This is a personal project developed with AI assistance, with features and usage feedback provided by the project owner. It is not an official game tool and is not affiliated with thprac. Full compatibility and a regular maintenance schedule are not promised.

The repository contains tool source code and documentation only: no game executable, assets, or personal settings. Version **3.2.0** still needs further live-game testing. No open-source license has been selected; public release and licensing remain separate decisions. **The application UI is currently in Simplified Chinese.**

## Build

Install the .NET 9 SDK on Windows x64, then run this from the repository directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The output is `artifacts/app/TH06NCTrainer.exe`. Running it requires the **.NET 9 Desktop Runtime x64**. Keep all output files together; do not copy only the EXE. Building does not require game files and does not connect to or modify the game.

Alternatively:

```powershell
dotnet build .\TH06NCTrainer.csproj -c Release
```

Hooks support only the exact game build identified below. Changing addresses without validating the underlying code is not a supported way to add compatibility.

## Usage and hotkeys

1. Close any older trainer normally, then launch the newly built helper. Do not run Cheat Engine or other tools that modify the same instructions at the same time.
2. Launch New Classic through Steam and start a run. Enable features after the helper passes its version check.
3. Resources can be set once without keeping them locked. POWER accepts 0–128; lives and bombs accept 0–8.

| Key | Action |
| --- | --- |
| F5 / F6 / F7 | Toggle lives / bombs / POWER locks |
| F8 | Invincibility against ordinary bullets, enemy contact, and lasers |
| F9 | Peaceful sightseeing: suppress enemy bullets and lasers, clear existing enemy projectiles, protect against contact, and filter enemy firing sounds |
| F10 | Select or disable Sakuya mode with attacks enabled |
| F11 | Select or disable Sakuya mode with attacks disabled |
| In-game Bomb action | With Sakuya mode selected, press once to stop time and again to resume; consumes no bombs and works at zero bombs |
| F12 | Not registered or intercepted; left available for Steam screenshots |

The Bomb action is the game's configured input, usually **X**, not the letter B. Controller mappings should follow the same input path but have not been tested in live gameplay.

## Game speed

In the game-speed section (`游戏速度`), enter 25–200 percent and click Apply (`应用`), or choose 0.5×, 0.75×, 1×, 1.5×, or 2×. The default is 1×; the choice is not saved between launches.

- Adjusts native whole-frame pacing: player, enemies, projectiles, and in-game timers change pace together, not just movement speed.
- Music is not stretched or pitch-shifted and may drift out of sync with stage progression. Performance, rendering waits, and VSync can limit acceleration; selecting 2× does not guarantee it is reached.
- The selected speed persists across stages and retries. Title and loading scenes temporarily use normal speed, with the selected rate reapplied during gameplay.
- Restoring 1× does not disable resource locks, peace mode, or time stop. Speed and Sakuya mode can be used together.
- There is no "disable all" button or hotkey. Normal exit, reconnection, and error cleanup still restore the memory modifications.
- If the speed heartbeat expires, normal pacing returns after roughly 120 rendered frames. This takes longer in wall-clock time at slower speeds; it is not immediate. Closing the game discards its process memory.
- There is no artificial score cap, score reset, or results-screen block. The game may still record scores or achievements. Assisted results should be distinguished from unassisted results; separate score storage is not implemented.

Native pacing has been checked in isolated game images and a separate test process. Live stages, pause/background behavior, display settings, native fast-forward, and audiovisual behavior still need regression testing.

## Sakuya time stop

The two modes are mutually exclusive and can be switched while time is stopped. **F10/F11 select the mode; the in-game Bomb action triggers time stop.**

- **Attacks enabled:** the player can move and shoot, and normal damage is processed. Enemy movement and bullet scripts stop; enemy projectiles, items, and related updates freeze. This does not multiply damage.
- **Attacks disabled:** the player can move, but player shooting and enemy updates are paused.
- Stage transitions and retry loading retain peace mode and the selected Sakuya mode, but release the active time stop. The Bomb action remains available in the next stage.
- Dialogue, player respawn/hit states, an existing native bomb, and boss HP crossing a phase threshold or reaching zero release the active stop while keeping the mode ready. Returning to the title or other non-gameplay scenes disables peace and Sakuya modes.
- Disabling Sakuya mode restores the native Bomb action. The enemy Sakuya's own native time-stop flag is not overwritten.

## Peace-mode audio

Firing sounds and projectile spawning are separate script actions. Version 3 suppressed spawning and cleared objects without suppressing sound requests.

Since v3.1, confirmed enemy-bullet/laser sound IDs are filtered before playback, and the queue is compacted so empty slots do not suppress unrelated sounds. Master volume and BGM are unchanged; ordinary player shots, item pickup, and menu sounds remain.

Filtering is based on sound IDs, so special effects reusing those IDs may also be muted. Music, enemy deaths, and explosions may still be audible; this does not necessarily mean peace mode failed.

## Safety and limitations

- Only running process memory is modified; the helper does not write the game EXE or save files. The game itself may still save scores or unlocks. Score isolation is absent, and Steam achievements are not guaranteed to remain unaffected.
- Replays recorded with modifications may desynchronize. Replay metadata and compatibility guarantees are not implemented. Do not present assisted results as unassisted clears.
- Normal exit restores original instructions. If recovery fails, close the game before exiting the helper. Mode heartbeat protection expires after about 120 player-update frames, not a precise wall-clock interval. It does not replace normal cleanup and does not cover the older invincibility patch.
- Unsupported game versions are rejected. Only `th06nc.exe` with this SHA-256 is supported:

  `07850C8C6E469C0E82C13423E6D0D096A88D693455BDACACBB44C0AA3BCCE473`

- Runtime support and distribution options must be reassessed and retested before a public release.

## Tests

Automated self-test sources are included. They execute actual x64 instructions in private mapped game images and check hook installation, time-stop activation, and removal in a separate actively updating test process. They do not inject test data into a running game. Generated reports are not uploaded to the repository.

The attack-enabled test executes native enemy-damage code and verifies HP changing from 100 to 76 while position and timers remain unchanged. Audio tests check filtering, preservation, and queue compaction. Particle effects and some test exits are stubbed in private test images; these are not live-game tests.

Still requiring live regression tests: consecutive stages, all player types and POWER levels, laser patterns, boss deaths and phase changes, the enemy Sakuya's native time stop, controllers, and replays.

After building with `dotnet build`, run tests only against your own legally installed game file:

```powershell
.\bin\Release\net9.0-windows\TH06NCTrainer.exe --self-test "FULL_PATH_TO_GAME\th06nc.exe" "FULL_PATH_TO_REPORT.txt"
```

## Repository layout

- `Program.cs`: window, hotkeys, and personal preferences.
- `MemorySession.cs`: game connection, version checks, resources, and invincibility.
- `RemoteModes.cs` / `ModeCode.cs`: peace mode, time stop, remote hooks, and restoration.
- `SpeedControl.cs`: basic game-speed control.
- `*SelfTest.cs` / `RemoteModeTest.cs`: isolated-image and separate-process tests.
- [Implementation notes for v3.2](docs/implementation-v3.2.txt): speed-control rationale and limitations, currently in Chinese.

The helper has no networking, advertising, account requirement, or follow-to-unlock mechanism. No game files or assets are distributed. It remains an experimental project, not a public release.

When reporting a problem, describe the game version, enabled features, and reproduction steps. Do not upload game executables, assets, saves, logs containing personal information, or account credentials.
