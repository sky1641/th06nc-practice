using System.Drawing.Drawing2D;
using System.Reflection;

namespace TH06NCTrainer;

internal static class Theme
{
    internal static readonly Color Ink = Color.FromArgb(244, 232, 224);
    internal static readonly Color Muted = Color.FromArgb(185, 165, 173);
    internal static readonly Color Accent = Color.FromArgb(234, 103, 115);
    internal static readonly Color Success = Color.FromArgb(146, 223, 183);
    internal static readonly Color Error = Color.FromArgb(255, 151, 151);
    internal static readonly Color Input = Color.FromArgb(34, 25, 33);
    internal static readonly Color Surface = Color.FromArgb(26, 18, 28);
    internal static readonly Color Canvas = Color.FromArgb(12, 8, 15);
    internal const TextFormatFlags PaintText = TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
    internal static Stream? Asset(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream("TH06NCTrainer.assets." + name);
    internal static void Apply(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            control.ForeColor = control.ForeColor == Color.DimGray || control.ForeColor == Muted ? Muted : Ink;
            switch (control)
            {
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = Color.FromArgb(53, 29, 39);
                    button.FlatAppearance.BorderColor = Color.FromArgb(120, 65, 78);
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(87, 40, 53);
                    button.FlatAppearance.MouseDownBackColor = Color.FromArgb(127, 46, 65);
                    button.Cursor = Cursors.Hand;
                    break;
                case CheckBox check:
                    // Each switch owns an opaque background; no parent text is replayed.
                    check.FlatStyle = FlatStyle.Flat; check.BackColor = Surface;
                    check.UseVisualStyleBackColor = false;
                    check.Cursor = Cursors.Hand;
                    break;
                case NumericUpDown number:
                    number.BackColor = Input; number.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case Label: control.BackColor = parent is MoonForm ? Canvas : Surface; break;
                case TableLayoutPanel: control.BackColor = Surface; break;
                case GlassPanel: control.BackColor = Surface; break;
            }
            if (control is not NumericUpDown) Apply(control);
        }
    }
}

internal class MoonForm : Form
{
    private readonly Image? moon;
    private bool frameActive;
    internal WindowFrame.AppliedAttribute[] FrameAttributes { get; private set; } = [];
    protected MoonForm()
    {
        DoubleBuffered = true;
        using var source = Theme.Asset("background.png");
        if (source is not null) { using var loaded = Image.FromStream(source); moon = new Bitmap(loaded); }
        Icon = PracticeIcon.Create();
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyFrame();
    }
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        frameActive = true;
        ApplyFrame();
    }
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        frameActive = false;
        ApplyFrame();
    }
    private void ApplyFrame()
    {
        if (IsHandleCreated) FrameAttributes = WindowFrame.Apply(Handle, frameActive, SystemInformation.HighContrast);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // Reapply after Windows theme/accessibility settings change.
        if (m.Msg is 0x031A or 0x001A) ApplyFrame();
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var background = new SolidBrush(Theme.Canvas);
        e.Graphics.FillRectangle(background, ClientRectangle);
        if (moon is not null)
        {
            float scale = Math.Max(ClientSize.Width / (float)moon.Width, ClientSize.Height / (float)moon.Height);
            float w = moon.Width * scale, h = moon.Height * scale;
            e.Graphics.DrawImage(moon, (ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
        }
        if (ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            using var shade = new LinearGradientBrush(ClientRectangle, Color.FromArgb(80, 9, 5, 14), Color.FromArgb(125, 9, 5, 14), 90F);
            e.Graphics.FillRectangle(shade, ClientRectangle);
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) moon?.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class GlassPanel : Panel
{
    internal GlassPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var rectangle = new Rectangle(0, 0, Width - 1, Height - 1);
        using var fill = new SolidBrush(Theme.Surface);
        using var edge = new Pen(Color.FromArgb(100, 155, 71, 89));
        e.Graphics.FillRectangle(fill, rectangle); e.Graphics.DrawRectangle(edge, rectangle);
        using var accent = new Pen(Color.FromArgb(170, 198, 69, 86), 2);
        e.Graphics.DrawLine(accent, 1, 12, 1, Height - 12);
        base.OnPaint(e);
    }
}

internal sealed class ThemedButton : Button
{
    internal ThemedButton() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        bool hover = Enabled && ClientRectangle.Contains(PointToClient(Cursor.Position));
        using var fill = new SolidBrush(hover ? Color.FromArgb(88, 40, 55) : Color.FromArgb(49, 27, 37));
        using var edge = new Pen(Enabled ? Color.FromArgb(139, 74, 91) : Color.FromArgb(83, 53, 65));
        e.Graphics.FillRectangle(fill, ClientRectangle);
        e.Graphics.DrawRectangle(edge, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? Theme.Ink : Theme.Muted, Theme.PaintText | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8), Theme.Ink, BackColor);
    }
}

internal sealed class ThemedCheckBox : CheckBox
{
    internal ThemedCheckBox()
    {
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        BackColor = Theme.Surface;
        ForeColor = Theme.Ink;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // Keep WinForms input and accessibility, but avoid themed disabled black text.
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        return new Size(text.Width + 28, Math.Max(text.Height + 6, 24));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var state = e.Graphics.Save();
        try
        {
            e.Graphics.SetClip(ClientRectangle, CombineMode.Intersect);
            using var fill = new SolidBrush(Theme.Surface);
            e.Graphics.FillRectangle(fill, ClientRectangle);
            int glyph = Math.Max(12, (int)Math.Round(Font.Height * 0.75));
            var box = new Rectangle(1, (Height - glyph) / 2, glyph, glyph);
            using var edge = new Pen(Enabled ? Theme.Accent : Theme.Muted);
            e.Graphics.DrawRectangle(edge, box);
            if (Checked)
            {
                using var tick = new Pen(Enabled ? Theme.Ink : Theme.Muted, 2);
                e.Graphics.DrawLines(tick, [new Point(box.Left + 2, box.Top + glyph / 2), new Point(box.Left + glyph / 2 - 1, box.Bottom - 3), new Point(box.Right - 2, box.Top + 3)]);
            }
            using var ink = new SolidBrush(Enabled ? Theme.Ink : Theme.Muted);
            using var format = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            // GDI+ keeps the same clip/transform as the background, unlike mixed HDC text.
            e.Graphics.DrawString(Text, Font, ink, new RectangleF(glyph + 7, 0, Math.Max(0, Width - glyph - 7), Height), format);
            if (Focused && ShowFocusCues)
            {
                using var focus = new Pen(Theme.Muted) { DashStyle = DashStyle.Dot };
                e.Graphics.DrawRectangle(focus, glyph + 5, 1, Math.Max(0, Width - glyph - 7), Math.Max(0, Height - 3));
            }
        }
        finally { e.Graphics.Restore(state); }
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var background = new SolidBrush(Theme.Surface);
        e.Graphics.FillRectangle(background, ClientRectangle);
    }
}
