# v3.4.2: opaque repaint surfaces

The user's v3.4.1 screenshot still showed neighboring text over switch captions. The earlier isolated test called only OnPaint and did not cover the parent-background painting used by transparent controls. Its passing result was insufficient evidence of a live-window fix.

Text labels, the resource table and section panels now have opaque backgrounds. MoonForm fills its background through the graphics clip instead of Graphics.Clear. The connection label no longer overlaps the resource table. The moon remains visible outside the reading surfaces, and the original red P identity is retained.

Switches explicitly clear their own background and use GDI+ for text, check marks and focus outlines, keeping all drawing within one clip/transform system. A framework-rendered switch was tried but produced unreadably dark disabled text in the offline preview; the final renderer uses explicit enabled/disabled foreground colors. WinForms CheckBox continues to handle keyboard, mouse, checked state and accessibility.

The UI test now invokes both OnPaintBackground and OnPaint. It checks text/container opacity, translated and partial clipping, checked/unchecked and enabled/disabled states at three font sizes, and 12 alternating dirty-background repaints per combination. Preview modes include --preview-wide and --preview-active. These are internal bitmap renders, not desktop screenshots or live UI automation. Physical-monitor DPI changes, live resize and occlusion/minimize recovery still need user-machine confirmation.

No game-memory hooks or gameplay behavior changed in this patch. The isolated native regression is rerun before packaging. No game files are modified or included in source packages.
