using System.Runtime.InteropServices;

namespace TH06NCTrainer;

internal static class WindowFrame
{
    internal readonly record struct AppliedAttribute(int Attribute, int Value, int HResult);
    internal const int DarkMode = 20, BorderColor = 34, CaptionColor = 35, TextColor = 36;
    internal static readonly Color ActiveBorder = Color.FromArgb(139, 74, 91);
    internal static readonly Color InactiveBorder = Color.FromArgb(83, 53, 65);
    internal static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
    internal static int ColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    // Per-window DWM attributes only. Retain the native caption, resize frame,
    // system menu, Snap, minimize/maximize and normal FormClosing cleanup.
    internal static AppliedAttribute[] Apply(nint handle, bool active, bool highContrast)
    {
        if (!Supported || handle == 0) return [];
        return [
            Set(handle, DarkMode, highContrast ? 0 : 1),
            Set(handle, BorderColor, highContrast ? -1 : ColorRef(active ? ActiveBorder : InactiveBorder)),
            Set(handle, CaptionColor, highContrast ? -1 : ColorRef(Theme.Canvas)),
            Set(handle, TextColor, highContrast ? -1 : ColorRef(active ? Theme.Ink : Theme.Muted))
        ];
    }
    private static AppliedAttribute Set(nint handle, int attribute, int value)
    {
        // A styling failure must never prevent startup or patch restoration.
        int result = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        if (result < 0) System.Diagnostics.Debug.WriteLine($"DWM attribute {attribute}: 0x{result:X8}");
        return new(attribute, value, result);
    }
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    internal static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
}
