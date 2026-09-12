# v3.4: opacity, shutdown recovery and local UI theme

## Recovery defect

The old form-closing handler called `DisableAll`, which refreshed mode and speed labels before disposing the session. When the game had exited, the reset methods returned early but the label getters still read the old remote control block. That read failed; the close handler cancelled closing and stopped its timer. Retrying repeated the same failure.

The closing path now disposes the session directly without any UI state reads. The session attempts both mode and invincibility recovery independently; failures are retained only while the game handle is still alive. A game exiting between native calls no longer prevents handle disposal. Cleanup is idempotent. Dead-process status reads return inactive/default values. Failed recovery while a game is alive is logged locally and can be retried; foreign patches are not blindly overwritten and the game is never automatically killed.

The regression test enables peace, Sakuya, Overdrive, opacity and invincibility in an isolated host, closes that host normally, and invokes the actual form-closing handler. It checks the close is not cancelled, ownership is released, status reads are safe and repeated cleanup succeeds.

## Opacity replaces RGB brightness

Control offsets 52/56 now represent player/enemy opacity: 0 hidden, 100 original. All three vertex-color hooks scale only the alpha byte, leaving RGB and VM state untouched. The first hook moved from RVA `3187` to `31E7`, after the native per-vertex fade calculation; the other two remain at `3E24` and `554C`.

The former RGB-color call hooks were replaced with rendering-route hooks at `33D0` and `4010`. When opacity is below 100%, the corresponding existing vertex-draw path is selected, because the optimized sprite path reloads VM colors and would bypass the changed vertex alpha. Original branch flags/conditions are preserved at 100% and for unrelated sprites. This is not a global blend-mode override.

Projectile-pool classification, laser inclusion, gameplay-only behavior and watchdog defaults remain as documented in v3.3. Tests execute all five hooks, check independent categories, boundaries, zero/100%, RGB preservation, unchanged VM colors, route selection and preservation of native branch decisions. Actual blending/visual quality across every live pattern remains unverified.

## UI

WinForms retains its existing keyboard and native accessibility behavior, with custom-painted dark buttons/check boxes, readable disabled states and high-contrast text. The supplied red-moon PNG is drawn as a background without modifying the source image. The New Classic icon is extracted from the local game executable and embedded in the local build. Assets are optional and git-ignored; source builds fall back to a plain dark theme/default icon.

The playful Overdrive caption is `还原千帧乡（笑）`. Its timing behavior remains unchanged: opt-in 16× target, not an uncapped exploit or guaranteed 960 FPS. The window can scroll on smaller screens.
