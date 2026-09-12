# v3.4.1 repaint fix and practice identity

The custom controls used GDI TextRenderer without PreserveGraphicsClipping or PreserveGraphicsTranslateTransform. During clipped or translated painting, text could escape the intended bounds and be drawn at the wrong origin. A complete DrawToBitmap preview did not exercise that path. The regression reproduces the original defect at pixel (24,14) despite a drawing offset of (37,29).

Theme.PaintText now specifies both preservation flags, consistent no-padding measurement/drawing and no mnemonic interpretation. Check-box preferred size includes the actual glyph, gap and measured text, reducing truncation. UserPaint, AllPaintingInWmPaint and double buffering are explicit. Existing WinForms input/accessibility behavior is retained.

UiPaintSelfTest compares translated and partially clipped painting pixel-for-pixel against an original render, and verifies pixels outside the clip remain untouched. Its 48 combinations cover buttons, check boxes, checked/unchecked, enabled/disabled, full/partial clips and three font sizes. This validates the specific clipping/translation defect, not every physical-monitor DPI configuration.

The application icon is now an original vector-drawn red P badge, not an edit of the game icon. PracticeIcon.cs deterministically generates nine PNG-backed ICO sizes from 16 to 256 px. icons/practice.ico is the executable icon; the same generator supplies the window/header icon. The background and all game hooks are unchanged. No icon-cache deletion or system appearance changes are performed.
