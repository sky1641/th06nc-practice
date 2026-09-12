using System.Text;

namespace TH06NCTrainer;

internal static class WindowFrameSelfTest
{
    private sealed class Probe : MoonForm
    {
        internal void SetActive(bool active) { if (active) OnActivated(EventArgs.Empty); else OnDeactivate(EventArgs.Empty); }
        internal void Recreate() => RecreateHandle();
        internal void ThemeChanged()
        {
            var message = Message.Create(Handle, 0x031A, 0, 0);
            WndProc(ref message);
        }
    }
    internal static void Run(StringBuilder report)
    {
        if (!WindowFrame.Supported) { report.AppendLine("SKIP: custom DWM colors require Windows 11 build 22000+"); return; }
        if (SystemInformation.HighContrast) { report.AppendLine("SKIP: system high-contrast frame is intentionally retained"); return; }
        using var form = new Probe();
        _ = form.Handle;
        WindowFrame.AppliedAttribute[]? overrideResults = null;
        void Check(int attribute, int expected, string label)
        {
            // Color attributes are documented as setters, not readable properties.
            // Verify values and HRESULTs of the actual native calls, not fake readback.
            var applied = (overrideResults ?? form.FrameAttributes).Single(a => a.Attribute == attribute);
            if (applied.HResult < 0 || applied.Value != expected)
                throw new InvalidOperationException($"Window frame {label}: {applied}, expected={expected:X8}");
            if (attribute == WindowFrame.DarkMode)
            {
                int result = WindowFrame.DwmGetWindowAttribute(form.Handle, attribute, out int actual, sizeof(int));
                if (result < 0 || actual != expected) throw new InvalidOperationException("Dark frame readback failed");
            }
            report.AppendLine("PASS: DWM accepted " + label);
        }
        void Colors(bool active)
        {
            Check(WindowFrame.DarkMode, 1, "dark caption buttons");
            Check(WindowFrame.CaptionColor, WindowFrame.ColorRef(Theme.Canvas), "dark moon caption");
            Check(WindowFrame.BorderColor, WindowFrame.ColorRef(active ? WindowFrame.ActiveBorder : WindowFrame.InactiveBorder), active ? "active red border" : "inactive muted border");
            Check(WindowFrame.TextColor, WindowFrame.ColorRef(active ? Theme.Ink : Theme.Muted), "readable caption text");
        }
        Colors(false);
        form.SetActive(true); Colors(true);
        form.SetActive(false); Colors(false);
        form.Recreate(); Colors(false);
        form.SetActive(true); form.ThemeChanged(); Colors(true);
        overrideResults = WindowFrame.Apply(form.Handle, true, true);
        Check(WindowFrame.DarkMode, 0, "high contrast restores system caption mode");
        Check(WindowFrame.BorderColor, -1, "high contrast restores system border");
        Check(WindowFrame.CaptionColor, -1, "high contrast restores system caption color");
        Check(WindowFrame.TextColor, -1, "high contrast restores system caption text");
        overrideResults = null;
        form.ThemeChanged(); Colors(true);
        if (form.FormBorderStyle != FormBorderStyle.Sizable || !form.ControlBox || !form.MinimizeBox || !form.MaximizeBox)
            throw new InvalidOperationException("Native window controls or resize frame removed");
        report.AppendLine("PASS: native sizable frame and standard caption controls retained");
    }
}
