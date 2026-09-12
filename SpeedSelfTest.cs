using System.Runtime.InteropServices;
using System.Text;

namespace TH06NCTrainer;

internal static class SpeedSelfTest
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Native();
    internal static void Run(IntPtr image, int imageSize, StringBuilder report)
    {
        void Check(bool ok, string title)
        {
            if (!ok) throw new InvalidOperationException(title);
            report.AppendLine("PASS: " + title); Console.WriteLine("PASS: " + title);
        }
        var saved = new List<(IntPtr Address, byte[] Bytes)>();
        void Replace(int rva, string hex)
        {
            byte[] bytes = Convert.FromHexString(hex), original = new byte[bytes.Length];
            Marshal.Copy(image + rva, original, 0, original.Length);
            saved.Add((image + rva, original)); WriteCode(image + rva, bytes);
        }
        IntPtr thunk = VirtualAlloc(IntPtr.Zero, 4096, 0x3000, 0x40);
        try
        {
            Marshal.WriteInt32(image + ModeCode.Scene, 2);
            Marshal.WriteInt64(image + ModeCode.FramePeriod, 60000);
            using (var m = new MemorySession(image, imageSize))
            {
                m.SetSpeed(50);
                IntPtr control = (IntPtr)m.Control;
                Replace(0x3C46C, "0F95C00FB6C0C3");
                // R15b is zero at the actual caller; preserve the harness's nonvolatile R15.
                byte[] wrapper = Convert.FromHexString("41574883EC204533FF48B8").Concat(BitConverter.GetBytes(m.ModeBlock + 19 * 0x400)).Concat(Convert.FromHexString("FFD04883C420415FC3")).ToArray();
                Marshal.Copy(wrapper, 0, thunk, wrapper.Length);
                var tick = Marshal.GetDelegateForFunctionPointer<Native>(thunk);
                long Period() => Marshal.ReadInt64(image + ModeCode.FramePeriod);
                foreach (int percent in new[] { 25, 50, 75, 100, 125, 150, 200 })
                {
                    m.RestoreSpeed(); Marshal.WriteInt64(image + ModeCode.FramePeriod, 60000);
                    Marshal.WriteByte(image + ModeCode.FrameInitialized, 1);
                    m.SetSpeed(percent); m.HeartbeatModes(); int comparison = tick();
                    Check(Period() == 60000L * 100 / percent && m.EffectiveSpeed == percent, $"Speed {percent}% scales whole-frame interval accurately");
                    Check(comparison == (percent == 100 ? 1 : 0), $"Speed {percent}% keeps original branch flags and rebases only when changed");
                    Marshal.WriteByte(image + ModeCode.FrameInitialized, 1); tick();
                    Check(Marshal.ReadByte(image + ModeCode.FrameInitialized) == 1, $"Stable speed {percent}% does not reset frame scheduling every tick");
                }
                m.SetSpeed(50); tick();
                Marshal.WriteInt32(image + ModeCode.Scene, 3); tick();
                Check(Period() == 60000 && m.DesiredSpeed == 50 && m.EffectiveSpeed == 100, "Loading uses 1x but retains chosen speed");
                Marshal.WriteInt64(image + ModeCode.FramePeriod, 66000); tick();
                Marshal.WriteInt32(image + ModeCode.Scene, 2); tick();
                Check(Period() == 132000 && m.DesiredSpeed == 50, "New renderer base interval is captured across scene reinitialization");
                Marshal.WriteInt32(image + ModeCode.Scene, 1); tick();
                Check(Period() == 66000 && m.EffectiveSpeed == 100 && m.DesiredSpeed == 50, "Title menu stays at normal speed");
                Marshal.WriteInt32(image + ModeCode.Scene, 2); tick();
                Marshal.WriteInt32(control + ModeCode.SpeedLease, 0); tick();
                Check(Period() == 66000 && m.DesiredSpeed == 100, "Expired speed heartbeat restores original pacing in native frame hook");
                foreach (int percent in new[] { 0, 24, 201, int.MaxValue })
                {
                    bool rejected = false; try { m.SetSpeed(percent); } catch (ArgumentOutOfRangeException) { rejected = true; }
                    Check(rejected, $"Unsafe requested speed {percent} is rejected");
                }
                Marshal.WriteInt32(control + ModeCode.SpeedPercent, 0); m.HeartbeatModes(); tick();
                Check(Period() == 66000, "Zeroed control data cannot divide by zero or freeze the game");
                m.SetPeace(true); m.SetSakuya(true, true); Marshal.WriteByte(control + ModeCode.Active, 1);
                m.SetSpeed(75); tick(); m.SetSpeed(100);
                Check(Period() == 66000 && m.ModeState == (true, true, true) && m.AttackAllowed, "Restoring 1x preserves peace and active attack time stop");
                m.SetSpeed(50); tick(); m.DisableModes(); tick();
                Check(Period() == 132000 && m.DesiredSpeed == 50, "Gameplay mode switches do not disable independent speed selection");
                m.RestoreSpeed();
                Check(Period() == 66000 && Marshal.ReadByte(image + ModeCode.FrameInitialized) == 0, "Explicit speed restore resets deadline anchor without waiting for another game frame");
                m.SetSpeed(200); tick();
            }
            Check(Marshal.ReadInt64(image + ModeCode.FramePeriod) == 66000, "Dispose restores original frame period before removing hooks");
            foreach (var (address, bytes) in saved.AsEnumerable().Reverse()) WriteCode(address, bytes); saved.Clear();

            // Execute the game's actual frame-deadline/spin-wait code with a deterministic fake clock.
            // Only graphics calls and the unrelated FPS/UI tail are bypassed in this private image.
            Replace(0x3C36F, "E9E8000000"); // First graphics call -> 3C45C clock read.
            Replace(0x3C5A1, "E977010000"); // Skip FPS HUD -> native cookie check/epilogue 3C71D.
            IntPtr clockValue = thunk + 512;
            byte[] clockCode = Convert.FromHexString("48B8").Concat(BitConverter.GetBytes(clockValue.ToInt64())).Concat(Convert.FromHexString("48FF00488B00C3")).ToArray();
            Marshal.Copy(clockCode, 0, thunk, clockCode.Length);
            Marshal.WriteIntPtr(image + 0xC220E0, thunk); Marshal.WriteIntPtr(image + 0xC220F0, IntPtr.Zero);
            var pace = Marshal.GetDelegateForFunctionPointer<Native>(image + 0x3C330);
            using (var m = new MemorySession(image, imageSize))
            {
                m.InstallModes();
                foreach (int percent in new[] { 50, 75, 100, 150, 200 })
                {
                    m.RestoreSpeed(); Marshal.WriteInt64(image + ModeCode.FramePeriod, 6000);
                    Marshal.WriteByte(image + ModeCode.FrameInitialized, 0); Marshal.WriteInt64(clockValue, 0);
                    m.SetSpeed(percent); m.HeartbeatModes(); pace();
                    long start = Marshal.ReadInt64(clockValue);
                    for (int frame = 0; frame < 6; frame++) pace();
                    long elapsed = Marshal.ReadInt64(clockValue) - start;
                    Check(elapsed == 6 * (6000L * 100 / percent), $"Native deadline loop: six {percent}% frames consume {elapsed} clock ticks");
                }
            }
        }
        finally
        {
            foreach (var (address, bytes) in saved.AsEnumerable().Reverse()) WriteCode(address, bytes);
            VirtualFree(thunk, 0, 0x8000);
        }
    }
    private static void WriteCode(IntPtr address, byte[] bytes)
    {
        VirtualProtect(address, (nuint)bytes.Length, 0x40, out uint old);
        Marshal.Copy(bytes, 0, address, bytes.Length); VirtualProtect(address, (nuint)bytes.Length, old, out _);
        FlushInstructionCache(GetCurrentProcess(), address, (nuint)bytes.Length);
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr h, IntPtr address, nuint size);
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, nuint size, uint type);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtect(IntPtr address, nuint size, uint protection, out uint old);
}
