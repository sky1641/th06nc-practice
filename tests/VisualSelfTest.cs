using System.Runtime.InteropServices;
using System.Text;

namespace TH06NCTrainer;

internal static class VisualSelfTest
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Native(IntPtr argument);
    internal static void Run(IntPtr image, int imageSize, StringBuilder report)
    {
        void Check(bool ok, string text)
        {
            if (!ok) throw new InvalidOperationException(text);
            report.AppendLine("PASS: " + text); Console.WriteLine("PASS: " + text);
        }
        var saved = new List<(IntPtr Address, byte[] Bytes)>();
        void Replace(int rva, byte[] bytes)
        {
            byte[] old = new byte[bytes.Length]; Marshal.Copy(image + rva, old, 0, old.Length);
            saved.Add((image + rva, old)); WriteCode(image + rva, bytes);
        }
        IntPtr wrapper = VirtualAlloc(IntPtr.Zero, 4096, 0x3000, 0x40);
        IntPtr gui = Marshal.AllocHGlobal(0x4000), inner = Marshal.AllocHGlobal(0x400), bullets = Marshal.AllocHGlobal(0x100000);
        try
        {
            Marshal.Copy(new byte[0x4000], 0, gui, 0x4000);
            Marshal.Copy(new byte[0x400], 0, inner, 0x400);
            Marshal.WriteIntPtr(gui + 0x38, inner);
            Marshal.WriteInt32(image + ModeCode.Scene, 2);
            using var m = new MemorySession(image, imageSize);
            m.InstallModes();
            IntPtr control = (IntPtr)m.Control;
            var guiSite = ModeCode.Sites[10];
            Replace(guiSite.Rva + guiSite.Original.Length, Convert.FromHexString("0F95C00FB6C0C3"));
            var guiTick = Marshal.GetDelegateForFunctionPointer<Native>((IntPtr)(m.ModeBlock + 10 * 0x400));
            Marshal.WriteByte(image + ModeCode.NativeTime, 0);
            Marshal.WriteByte(control + ModeCode.Active, 1);
            for (int i = 0; i < 8; i++)
            {
                long xy = ((long)BitConverter.SingleToInt32Bits(240 - i * 3) << 32) | (uint)BitConverter.SingleToInt32Bits(192 + i * 2);
                Marshal.WriteInt64(image + ModeCode.Player + 0x7730, xy);
                Marshal.WriteInt32(image + ModeCode.Player + 0x7738, BitConverter.SingleToInt32Bits(0.4F));
                Check(guiTick(gui) == 1 && Marshal.ReadInt64(inner + 0x1E8) == xy && Marshal.ReadInt64(inner + 0x308) == xy && Marshal.ReadInt32(inner + 0x1F0) == BitConverter.SingleToInt32Bits(0.4F) && Marshal.ReadInt32(inner + 0x310) == BitConverter.SingleToInt32Bits(0.4F), $"Stopped GUI keeps both player indicators aligned, movement step {i}");
            }
            Check(Marshal.ReadByte(image + ModeCode.NativeTime) == 0 && Marshal.ReadInt32(inner + 0x1F4) == 0, "Indicator sync leaves native time and adjacent GUI state unchanged");
            Marshal.WriteByte(control + ModeCode.Active, 0); Marshal.WriteByte(image + ModeCode.NativeTime, 1);
            Marshal.WriteInt64(inner + 0x1E8, 123);
            Check(guiTick(gui) == 1 && Marshal.ReadInt64(inner + 0x1E8) == 123, "Enemy Sakuya stop retains original GUI behavior");
            Marshal.WriteByte(control + ModeCode.Active, 1);
            Check(guiTick(IntPtr.Zero) == 1, "Missing GUI is safe during our stop");
            Marshal.WriteIntPtr(gui + 0x38, IntPtr.Zero);
            Check(guiTick(gui) == 1, "Missing indicator storage is safe during scene teardown");
            Marshal.WriteByte(image + ModeCode.NativeTime, 0);
            Marshal.WriteByte(control + ModeCode.Active, 0);
            Marshal.WriteInt64(control + ModeCode.EnemyManager, bullets.ToInt64());
            foreach (int index in new[] { 20, 21, 22 })
            {
                var site = ModeCode.Sites[index];
                Replace(site.Rva + site.Original.Length, [0xC3]);
                byte[] code = Convert.FromHexString("534883EC20488BD9B88000000049BB")
                    .Concat(BitConverter.GetBytes(m.ModeBlock + index * 0x400))
                    .Concat(Convert.FromHexString("41FFD34883C4205BC3")).ToArray();
                WriteCode(wrapper, code);
                var draw = Marshal.GetDelegateForFunctionPointer<Native>(wrapper);
                foreach (var (vm, player, enemy, expected) in new (IntPtr, int, int, int)[]
                {
                    (image + ModeCode.Player + 0x428, 50, 25, 50),
                    (image + ModeCode.Player + 0x428 + 79 * 0x170, 25, 75, 25),
                    (bullets + 0x58, 25, 75, 75),
                    (bullets + 0xF5038, 25, 50, 50),
                    (bullets + 0xF5038 + 63 * 0x298 + 0x120, 25, 50, 50),
                    (image + ModeCode.Player + 0x78C8, 0, 0, 100),
                    (inner, 0, 0, 100),
                    (bullets + 0xFF638, 0, 0, 100),
                    (image + ModeCode.Player + 0x428, 0, 100, 0),
                    (image + ModeCode.Player + 0x428, 100, 0, 100)
                })
                {
                    m.SetOpacity(player, enemy);
                    Marshal.WriteInt32(vm + 0xEC, unchecked((int)0xB3C86480));
                    foreach (int color in ModeCode.VertexColors) Marshal.WriteInt32(image + color, unchecked((int)0xA5C86480));
                    int result = draw(vm);
                    int wanted = unchecked((int)((uint)(165 * expected / 100) << 24)) | 0xC86480;
                    Check(ModeCode.VertexColors.All(color => Marshal.ReadInt32(image + color) == wanted), $"Opacity hook {index}: independent category at {expected}%, RGB preserved");
                    Check(Marshal.ReadInt32(vm + 0xEC) == unchecked((int)0xB3C86480), $"Color hook {index} never mutates the projectile VM color");
                }
                m.SetOpacity(0, 0); Marshal.WriteInt32(image + ModeCode.Scene, 1);
                foreach (int color in ModeCode.VertexColors) Marshal.WriteInt32(image + color, unchecked((int)0xA5C86480));
                int menuResult = draw(image + ModeCode.Player + 0x428);
                Check(ModeCode.VertexColors.All(color => Marshal.ReadInt32(image + color) == unchecked((int)0xA5C86480)), $"Opacity hook {index} leaves menu rendering unchanged");
                Marshal.WriteInt32(image + ModeCode.Scene, 2);
            }
            foreach (int index in new[] { 23, 24 })
            {
                var site = ModeCode.Sites[index];
                Replace(site.Rva + site.Original.Length, Convert.FromHexString("B800000000C3"));
                Replace(index == 23 ? 0x364E : 0x42A2, Convert.FromHexString("B801000000C3"));
                foreach (bool nativeFallback in new[] { false, true })
                {
                    byte[] code = Convert.FromHexString("534883EC20488BD94533D233C0" + (nativeFallback ? "41B20183C8FF" : "" ) + "49BB")
                        .Concat(BitConverter.GetBytes(m.ModeBlock + index * 0x400))
                        .Concat(Convert.FromHexString("41FFD34883C4205BC3")).ToArray();
                    WriteCode(wrapper, code);
                    var route = Marshal.GetDelegateForFunctionPointer<Native>(wrapper);
                    m.SetOpacity(50, 25);
                    Check(route(image + ModeCode.Player + 0x428) == 1 && route(bullets + 0x58) == 1, $"Opacity route {index} sends faded projectiles through vertex alpha, native fallback={nativeFallback}");
                    m.SetOpacity(100, 100);
                    Check(route(image + ModeCode.Player + 0x428) == (nativeFallback ? 1 : 0), $"Opacity route {index} preserves original branch conditions at 100%, native fallback={nativeFallback}");
                    m.SetOpacity(0, 0);
                    Check(route(inner) == (nativeFallback ? 1 : 0), $"Opacity route {index} does not reroute HUD/background sprites");
                }
            }
            m.SetOpacity(40, 60); Marshal.WriteInt32(image + ModeCode.Scene, 3); m.MaintainModes();
            Check(Marshal.ReadInt32(control + ModeCode.PlayerOpacity) == 40 && Marshal.ReadInt32(control + ModeCode.EnemyOpacity) == 60, "Stage loading retains separate opacity choices");
            foreach (int invalid in new[] { -1, 101 })
            {
                bool rejected = false; try { m.SetOpacity(invalid, 100); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, $"Invalid opacity {invalid} is rejected");
            }
            Marshal.WriteInt32(image + ModeCode.Scene, 2);
            var playerSite = ModeCode.Sites[0];
            Replace(playerSite.Rva + playerSite.Original.Length, [0xC3]);
            Marshal.WriteInt32(control + ModeCode.Lease, 0);
            var tick = Marshal.GetDelegateForFunctionPointer<Native>((IntPtr)m.ModeBlock);
            tick(IntPtr.Zero);
            Check(Marshal.ReadInt32(control + ModeCode.PlayerOpacity) == 100 && Marshal.ReadInt32(control + ModeCode.EnemyOpacity) == 100, "Expired player watchdog restores both opacity choices");
        }
        finally
        {
            foreach (var (address, bytes) in saved.AsEnumerable().Reverse()) WriteCode(address, bytes);
            Marshal.FreeHGlobal(gui); Marshal.FreeHGlobal(inner); Marshal.FreeHGlobal(bullets);
            VirtualFree(wrapper, 0, 0x8000);
        }
    }
    private static void WriteCode(IntPtr address, byte[] bytes)
    {
        VirtualProtect(address, (nuint)bytes.Length, 0x40, out uint old);
        Marshal.Copy(bytes, 0, address, bytes.Length); VirtualProtect(address, (nuint)bytes.Length, old, out _);
        FlushInstructionCache(GetCurrentProcess(), address, (nuint)bytes.Length);
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, nuint size);
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, nuint size, uint type);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtect(IntPtr address, nuint size, uint protection, out uint old);
}
