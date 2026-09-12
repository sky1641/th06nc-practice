# TH06 New Classic Practice Helper

English | [简体中文](README.zh-CN.md)

A Windows x64 practice and sightseeing helper for **TH06 New Classic**. Features are off by default. The application UI is in Simplified Chinese.

Current release: **v1.0.0**, promoting development build **3.4.3** with no gameplay changes. See the [release notes](RELEASE_NOTES.md).

Personal project developed with AI assistance. Not an official tool or part of thprac. No networking, advertising, or account requirement. A reuse license has not yet been selected. The release includes a user-supplied red-moon background; see [asset notes](assets/README.md). The original red P practice icon distinguishes the helper from the game.

## Getting started

1. Extract the application package and keep its files together. The standard package requires **.NET 9 Desktop Runtime x64**; a self-contained package includes its runtime.
2. Close older helpers normally. Launch this helper and your Steam copy of New Classic, then start a run.
3. Enable the features you want after the helper connects. Do not run another tool that patches the same game instructions.

Only this `th06nc.exe` SHA-256 is supported; other builds are rejected:

`07850C8C6E469C0E82C13423E6D0D096A88D693455BDACACBB44C0AA3BCCE473`

## Controls

| Control | Action |
| --- | --- |
| F5 / F6 / F7 | Lock lives / Bomb / POWER. Values can also be set once. Lives and Bomb: 0–8; POWER: 0–128. |
| F8 | Invincibility against ordinary bullets, contact, and lasers. |
| F9 | Peaceful sightseeing: suppress enemy projectiles and firing sounds; protect against contact. |
| F10 | Select or disable attack-enabled Sakuya mode. |
| F11 | Select or disable attack-disabled Sakuya mode. |
| In-game Bomb action | Toggle time stop while Sakuya mode is selected. No Bomb consumption; works at zero Bomb. |

The Bomb action is usually **X**, not the letter B. F10/F11 select mutually exclusive modes; they do not directly stop time. During a stop, movement remains available; the attack-disabled mode also freezes player shooting.

Peace and Sakuya selections persist across stages and retries. Transitions release the current stop; dialogue, respawn, an existing native bomb, and boss phase changes also resume time. Returning to the title disables these two modes. The enemy Sakuya's own time-stop flag is unchanged.

Peace mode retains music, pickup and menu sounds. Deaths and explosions can remain audible. Sound-ID filtering can also mute special effects that reuse a firing sound.

## Speed and Overdrive

Set **25–200%** under `游戏速度` and click `应用`, or use a preset. **Disable V-Sync (vertical synchronization) before accelerating**, including any driver override. The helper does not change display settings for you.

- Speed changes whole-frame pacing, including movement and game timers. Music stays at its original rate.
- The selected rate persists across stages; menus and loading use normal speed.
- **Overdrive · 娱乐** is a separate, opt-in entertainment mode targeting **16×**, nominally **960 FPS** from a 60 FPS baseline. It is bounded acceleration, not the original game's uncapped bug, and cannot guarantee that frame rate.
- Unchecking Overdrive restores the previous regular rate. Any speed preset exits Overdrive; `恢复 1×` restores normal speed without disabling other features.
- Performance, V-Sync, rendering waits and native fast-forward can affect the actual rate. The UI displays a target, not measured FPS. Speed is not saved between launches.

## Projectile opacity

Under `弹幕透明度`, set independent **0–100% visibility** values for player (`自机`) and enemy (`敌机`) projectiles, then click `应用`. **0% hides them; 100% keeps the original appearance.** `恢复` resets both to 100%.

The feature scales rendered alpha, preserving RGB and the game's existing fades. Enemy lasers are included; player bodies, backgrounds, HUD and items are excluded. It does not change collisions, damage or movement. **Transparent projectiles remain dangerous.** Zero visibility is not peace mode. Choices persist across stages, but reset on reconnection or a new helper session. Bomb effects are not included in the player-shot control.

## Safety and testing

- Only process memory is patched. The helper does not write the game executable or save files, but the game itself may still save scores, unlocks or achievements. There is no score isolation or artificial score cap.
- Assisted replays may desynchronize. Keep assisted results distinct from unassisted clears.
- Close the helper normally to restore its patches. If restoration fails, close the game. Frame-count watchdogs are a fallback, not a substitute for cleanup: normally 120 frames, or 1920 while Overdrive is selected. Recovery takes longer when actual FPS is low; the older invincibility patch is not covered by the watchdog.
- Version 3.4 tests indicator tracking, opacity categories and RGB preservation, alpha-capable rendering paths, Overdrive, and installation/removal. A shutdown regression closes the test game with features installed, then executes the helper's actual form-closing handler. Tests use private images and a test host, **not live gameplay**.
- Live regression is still needed for all player types, laser patterns, stage transitions, focus changes during time stop, display settings, controllers and replays. Opacity rendering and Overdrive's actual frame rate need visual confirmation in the game.
- Recovery failures are written to local `error.log`. A terminated game no longer needs memory restoration, and must not prevent the helper from closing. If the game is still alive and restoration fails, the helper retains its recovery handle for another attempt.

## Build and test

Install the .NET 9 SDK on Windows x64, then run from the repository directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `artifacts/app/TH06NCTrainer.exe`. Building requires no game files and does not attach to the game. To run the isolated tests against your own installed copy:

```powershell
dotnet build .\TH06NCTrainer.csproj -c Release
.\bin\Release\net9.0-windows\TH06NCTrainer.exe --self-test "FULL_PATH_TO_GAME\th06nc.exe" "FULL_PATH_TO_REPORT.txt"
```

[Implementation notes](docs/implementation-v3.4.md) cover opacity and shutdown recovery. [v3.3 notes](docs/implementation-v3.3.md) retain the original TH06 runaway-speed research. The Overdrive caption `还原千帧乡（笑）` is playful; the mode remains bounded 16× acceleration.

The red-moon theme embeds `assets/background.png`. Release builds require this asset; development builds can omit it and use a plain dark background. `icons/practice.ico` and its vector source `PracticeIcon.cs` provide the helper's own red P icon at 16–256 px, without game character artwork. Run `build-release.ps1` to build both release packages, run UI checks and generate SHA-256 checksums. The background's rights are separate from the original P icon; see the asset notes.

Version 3.4.3 adds a near-black native title bar with light text and a muted-red window border on Windows 11. Inactive windows use a subtler border and caption text. Native window controls, resizing and the normal close/restore workflow are retained. High contrast restores system frame colors; earlier Windows versions keep their standard frame. This only styles the helper window, not Windows or the game. See [frame notes and test limits](docs/implementation-v3.4.3.md).

Version 3.4.2 replaces transparent text/container surfaces with opaque backgrounds and draws switch text with GDI+ using the same clip and transform as its background. The previous 3.4.1 patch did not resolve the reported live-window overlap. Run the rendering regression with `TH06NCTrainer.exe --ui-self-test "FULL_PATH_TO_REPORT.txt"`. It checks opaque surfaces, translated/clipped painting, repeated dirty-background repainting, enabled/disabled and checked states at three font sizes. Offline previews cover normal and widened windows. These checks do not attach to the game or replace live desktop resize/occlusion and monitor-DPI testing. See [v3.4.2 notes](docs/implementation-v3.4.2.md). When reporting bugs, include the game version, enabled features and reproduction steps; do not upload game files, saves or credentials.
