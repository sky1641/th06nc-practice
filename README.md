# TH06 New Classic Practice Helper

Version 1.0.1 — English edition

An unofficial Windows x64 practice and sightseeing helper, developed with AI assistance. It is not affiliated with the game developers or thprac. No accounts, advertising or networking. All assistance starts disabled.

## Start

Extract the entire ZIP before running TH06NCTrainer.exe. Keep all files together. The standard package requires .NET 9 Desktop Runtime x64; the self-contained package includes it. Close older helpers normally, launch the game and start a run. Do not run the English and Chinese editions together: they control the same game and share a single-instance guard.

Only the Steam th06nc.exe with this SHA-256 is supported:

`07850C8C6E469C0E82C13423E6D0D096A88D693455BDACACBB44C0AA3BCCE473`

## Controls

| Key | Feature |
| --- | --- |
| F5 / F6 / F7 | Lock lives / Bomb / POWER; Set once writes the target once. |
| F8 | Invincibility against ordinary bullets, enemy contact and lasers. |
| F9 | Peaceful sightseeing: suppress enemy shots, lasers and firing sounds. |
| F10 | Select or disable Sakuya mode with attacks allowed. |
| F11 | Select or disable Sakuya mode without attacks. |
| Game Bomb action | Toggle time stop while a Sakuya mode is selected. Usually X, not B. |

Lives and Bomb targets are 0–8, POWER 0–128. Targets are saved locally in settings.json, but assistance is not enabled automatically.

The two Sakuya modes are mutually exclusive. Time stop costs no Bomb and works at zero Bomb. Movement remains available. No attacks also pauses player shots. Peace and Sakuya selections persist across stages and retries; transitions, dialogue, respawn, an existing native bomb and boss phase changes release the current stop. Returning to the title disables these modes. The enemy Sakuya's native time-stop flag is not modified.

## Speed and opacity

Speed is 25–200%. Disable V-Sync before acceleration, including driver overrides. Music remains at its original rate. Stage changes preserve the selected rate; menus/loading run normally. Speed is not saved between helper sessions.

Overdrive is an opt-in entertainment mode targeting 16×, nominally 960 FPS. Actual speed depends on performance and display settings. This is bounded acceleration, not the original uncapped bug. Disabling it restores the regular rate; speed presets exit it.

Player and enemy opacity are independent: 0% hides projectiles; 100% preserves their original appearance. Enemy lasers are included. Opacity does not change collisions, so hidden bullets remain dangerous. Player Bomb effects are excluded. Opacity persists across stages but resets on reconnection or restart. Peace mode keeps music, pickups and menus audible; effects sharing firing sound IDs may also be muted.

## Safety and limitations

Only process memory is patched; the helper does not write game executables or saves. The game can still save scores, unlocks or achievements. There is no score isolation or artificial score cap. Keep assisted results separate from normal clears; assisted replays may desynchronize.

Close the helper normally to restore its changes. If restoration fails, close the game, then close the helper. Error details are saved to error.log. Frame watchdogs are only a fallback: normally 120 frames, or 1920 in Overdrive; they do not cover the older invincibility patch. Low actual frame rates delay fallback recovery.

Automated UI and isolated-memory tests do not replace full live-game testing. Player types, lasers, continuous stage transitions, focus changes, controllers, display configurations and replays still need broader testing. Include the game build, enabled features and reproduction steps in bug reports. Do not upload game files, saves or credentials.

The red-moon background was supplied by the project owner; inclusion does not claim ownership or grant an additional reuse license. The red P icon is separate original artwork. No reuse license has been selected for the project. The Windows 11 frame is themed; earlier Windows versions retain their standard frame.

[Chinese documentation](README.zh-CN.md) · [Release notes](distribution/en/RELEASE_NOTES.md)
