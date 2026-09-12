using System.Reflection;
using System.Text;

namespace TH06NCTrainer;

internal static class UiPaintSelfTest
{
    internal static void Run(string reportPath)
    {
        var report = new StringBuilder();
        try
        {
            WindowFrameSelfTest.Run(report);
            using (var form = new TrainerForm(false))
            {
                void VerifyTree(Control parent)
                {
                    foreach (Control control in parent.Controls)
                    {
                        if (control is CheckBox or Label or GlassPanel or TableLayoutPanel)
                        {
                            if (control.BackColor.A != 255) throw new InvalidOperationException($"Transparent text/container surface: {control.GetType().Name} {control.Text}");
                            report.AppendLine($"PASS: opaque surface {control.GetType().Name} {control.Text}");
                        }
                        VerifyTree(control);
                    }
                }
                VerifyTree(form);
            }
            foreach (float fontSize in new[] { 10F, 12.5F, 15F })
            foreach (bool enabled in new[] { true, false })
            foreach (bool check in new[] { false, true })
            foreach (bool button in new[] { false, true })
            {
                using Control control = button ? new ThemedButton() : new ThemedCheckBox { Checked = check };
                control.Size = new Size(220, 38); control.Text = "锁 POWER  [F7]";
                control.Font = new Font("Microsoft YaHei UI", fontSize); control.Enabled = enabled;
                var bounds = new Rectangle(Point.Empty, control.Size);
                using var original = Render(control, Point.Empty, bounds);
                var offset = new Point(37, 29);
                foreach (Rectangle clip in new[] { bounds, new Rectangle(41, 5, 120, 23) })
                {
                    using var translated = Render(control, offset, clip);
                    for (int y = 0; y < translated.Height; y++)
                    for (int x = 0; x < translated.Width; x++)
                    {
                        int localX = x - offset.X, localY = y - offset.Y;
                        var expected = clip.Contains(localX, localY) ? original.GetPixel(localX, localY) : Color.Black;
                        if (translated.GetPixel(x, y).ToArgb() != expected.ToArgb())
                            throw new InvalidOperationException($"Paint escaped clipping/translation: {control.GetType().Name}, font={fontSize}, enabled={enabled}, checked={check}, clip={clip}, pixel={x},{y}");
                    }
                    report.AppendLine($"PASS: {control.GetType().Name}, font={fontSize}, enabled={enabled}, checked={check}, clip={clip}");
                }
                using var dirty = new Bitmap(280, 90);
                using (var graphics = Graphics.FromImage(dirty))
                {
                    for (int step = 0; step < 12; step++)
                    {
                        graphics.ResetTransform(); graphics.ResetClip();
                        graphics.Clear(step % 2 == 0 ? Color.Magenta : Color.Lime);
                        graphics.TranslateTransform(offset.X, offset.Y); graphics.SetClip(bounds);
                        Paint(control, graphics, bounds);
                        for (int y = 0; y < bounds.Height; y++)
                        for (int x = 0; x < bounds.Width; x++)
                            if (dirty.GetPixel(x + offset.X, y + offset.Y).ToArgb() != original.GetPixel(x, y).ToArgb())
                                throw new InvalidOperationException($"Background was not replaced: {control.GetType().Name}, step={step}, pixel={x},{y}");
                    }
                }
                report.AppendLine($"PASS: 12 dirty-background repaints {control.GetType().Name}, font={fontSize}, enabled={enabled}, checked={check}");
            }
        }
        catch (Exception error) { report.AppendLine("FAIL: " + error); Environment.ExitCode = 1; }
        File.WriteAllText(reportPath, report.ToString());
    }
    private static Bitmap Render(Control control, Point offset, Rectangle clip)
    {
        var bitmap = new Bitmap(280, 90);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.TranslateTransform(offset.X, offset.Y);
        graphics.SetClip(clip);
        Paint(control, graphics, clip);
        return bitmap;
    }
    private static void Paint(Control control, Graphics graphics, Rectangle clip)
    {
        using var args = new PaintEventArgs(graphics, clip);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        control.GetType().GetMethod("OnPaintBackground", flags)!.Invoke(control, [args]);
        control.GetType().GetMethod("OnPaint", flags)!.Invoke(control, [args]);
    }
}
