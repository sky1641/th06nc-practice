# v3.3 implementation notes

These hooks are original implementation code for the exact New Classic executable hash in the README. The original TH06 decompilation below is used to understand its timing behavior, not as copied implementation code.

## Hitbox indicator

The GUI callback at RVA `3CC56` normally skips HUD updates during native time stop. Extending that gate to our stop also froze the two focus-indicator ANM positions. The player movement/collision path continued running.

While our stop is active, the hook now copies player XYZ (`4FF3A0 + 7730`) into the GUI inner object's two indicator positions (`+1E8` and `+308`). Null GUI/storage pointers are ignored. All other HUD updates remain frozen; the native enemy time-stop flag is never written. This fixes position tracking, not focus-animation playback; changing focus during a stop still requires live regression.

## Brightness

The draw functions at `2A80`, `36C0` and `4DC0` build two sets of four vertex colors. Hooks at `3187`, `3E24` and `554C` complete the original last color store, then scale RGB at `A6EAF0/A6EB0C/A6EB28/A6EB44` and `A6E9D8/A6EA08/A6EA38/A6EA68`. Alpha is preserved. The color-setting calls at `34FA` and `412D` cover the alternate draw path.

Classification uses the current ANM VM address in RBX: the player's 80-shot pool spans player `+410..+7710`; the bullet manager's projectile/laser pool spans manager `+8..+FF638`. The update hook captures the current manager address each tick. There is no dereference of a stale manager pointer in the drawing hook. Other VMs and non-gameplay scenes are unchanged. Bomb effects are outside the player-shot range.

No VM color is permanently modified, so there is no cumulative dimming or animation-color restoration to race with. No new remote calls or cave return addresses are left on the stack. Default percentages are initialized before hooks become executable. Tests exercise all five hook paths, category boundaries, RGB scaling and alpha preservation. Actual rendering across all player types still needs live testing.

## Overdrive and the original runaway-speed behavior

In the [original TH06 decompilation's GameWindow.cpp](https://github.com/GensokyoClub/th06/blob/master/src/GameWindow.cpp), `Render` has a software frame-time gate for windowed/force-60 operation. Another fullscreen path relies on `Present`; rendering setup requests a one-refresh presentation interval. Calculation is advanced by loop iterations. It follows that losing the presentation wait can also accelerate game logic. This explains a mechanism for the commonly described runaway FPS, not every possible configuration-specific failure.

New Classic has a separate native frame deadline. Overdrive reuses the existing interval hook: original interval × 100 / 1600, bounded below by one clock tick. It reproduces the fast-gameplay feel without deliberately breaking V-Sync or removing every pacing limit. At a nominal 60 Hz baseline the target is 960 FPS; this is not measured throughput.

Overdrive is opt-in, session-only, paused in menus/loading, and retains the previous 25–200% selection. Any regular speed command clears it; restoring 1× immediately restores the owned native interval. Watchdog leases use 1920 frames while Overdrive is selected and 120 otherwise; these are frame counts, not wall-clock guarantees. On speed expiry the flag is cleared and normal pacing resumes. The player watchdog additionally restores both brightness choices to 100%.

New control fields: `+48` Overdrive byte, `+52/+56` player/enemy brightness integers, `+64` bullet-manager pointer. Existing fields and hook indices are preserved. All 25 sites are validated before any installation, and restored on normal removal.

## Verification boundary

The self-test executes the generated x64 hooks in private mapped game images, with selected native continuations/graphics effects replaced only in the test image. Timing tests additionally run the native deadline loop against a deterministic clock. A separate active test process verifies safe installation/removal and restoration. The tests do not establish live visual quality, replay compatibility, display-setting behavior or actual 960 FPS.
