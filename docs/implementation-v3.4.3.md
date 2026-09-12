# v3.4.3 native window frame

WindowFrame applies per-window DWM dark-mode, border-color, caption-color and text-color attributes. The frame is near-black (#0C080F), with an active muted-red edge (#8B4A5B) and warm light text. Inactive edges/text are muted. MoonForm reapplies these values on handle creation/recreation, activation/deactivation and theme/settings messages.

The Windows title bar is retained: no frameless replacement, custom hit testing or close interception was added. Resizing, native caption buttons, system menu and Snap remain under Windows control. No game hooks, hotkeys, opacity or gameplay behavior changed. Failed appearance calls cannot block startup or restoration. High-contrast mode resets frame attributes to system defaults; this is a frame-level accommodation, not a claim that the entire existing UI is high-contrast accessible. Windows versions before build 22000 retain the standard frame.

The supported attribute IDs, COLORREF encoding and system-default sentinel follow [Microsoft's DWMWINDOWATTRIBUTE documentation](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute).

WindowFrameSelfTest creates only its own non-game test window and verifies native setter HRESULTs and submitted colors through activation, handle recreation, theme changes and a simulated frame-level high-contrast fallback. Dark mode is read back from DWM. Color attributes are documented for setting, so their accepted setter values are checked rather than relying on unsupported color getters. UI paint regression and isolated gameplay regression also run.

These tests validate API acceptance and application behavior, not final desktop compositing. DrawToBitmap does not reliably capture the DWM title bar. Live appearance, snapping and physical-monitor DPI behavior still require desktop confirmation; no synthetic title-bar image is presented as a screenshot.
