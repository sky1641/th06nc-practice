using System.Runtime.InteropServices;
using System.Text;

namespace TH06NCTrainer;

internal static class ModeSelfTest
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int Native(IntPtr arg);
    private static void Check(bool condition, string text, StringBuilder report)
    {
        if (!condition) throw new InvalidOperationException(text);
        report.AppendLine("PASS: " + text);
        Console.WriteLine("PASS: " + text);
    }
    internal static void Run(IntPtr image, int imageSize, StringBuilder report)
    {
        IntPtr player = Marshal.AllocHGlobal(0xA400), gui = Marshal.AllocHGlobal(0x4000), bullets = Marshal.AllocHGlobal(0x100000);
        Marshal.Copy(new byte[0xA400], 0, player, 0xA400);
        Marshal.Copy(new byte[0x4000], 0, gui, 0x4000);
        Marshal.Copy(new byte[0x100000], 0, bullets, 0x100000);
        var saved = new List<(IntPtr Address, byte[] Data)>();
        void Replace(int rva, string hex)
        {
            byte[] bytes = Convert.FromHexString(hex), original = new byte[bytes.Length];
            Marshal.Copy(image + rva, original, 0, original.Length);
            saved.Add((image + rva, original));
            WriteCode(image + rva, bytes);
        }
        try
        {
            Marshal.WriteInt32(image + ModeCode.Scene, 2);
            Marshal.WriteIntPtr(image + ModeCode.Gui, gui);
            Marshal.WriteInt32(gui + 0x36B0, -1);
            Marshal.WriteByte(image + ModeCode.NativeTime, 0);
            Marshal.WriteInt32(image + ModeCode.Input, 0);
            using (var m = new MemorySession(image, imageSize))
            {
                var conflictSite = ModeCode.Sites[11];
                byte[] foreign = (byte[])conflictSite.Original.Clone(); foreign[0] ^= 1;
                WriteCode(image + conflictSite.Rva, foreign);
                bool rejected = false;
                try { m.InstallModes(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && !m.ModesInstalled && Marshal.ReadByte(image + conflictSite.Rva) == foreign[0], "Conflicting mode instruction is rejected before allocation or writes", report);
                WriteCode(image + conflictSite.Rva, conflictSite.Original);
                m.SetPeace(true);
                m.SetSakuya(true);
                Check(m.ModesInstalled && m.ModeState == (true, true, false), "Install all hooks with independent peace / Sakuya flags", report);
                long block = m.ModeBlock;
                IntPtr control = (IntPtr)m.Control;
                Native Stub(int index) => Marshal.GetDelegateForFunctionPointer<Native>((IntPtr)(block + index * 0x400));
                // Only test continuations are stubbed; the exact installed hook machine code runs natively.
                // SETNE AL; MOVZX EAX,AL; RET exposes the comparison result to this harness.
                foreach (int index in new[] { 0, 1, 10, 11, 12 })
                {
                    var site = ModeCode.Sites[index];
                    Replace(site.Rva + site.Original.Length, "0F95C00FB6C0C3");
                }
                Replace(0x373B7, "B802000000C3");
                Replace(0x10856, "C3");
                Replace(0x689E5, "8BC1C3");
                Replace(0x69B09, "B801000000C3");
                Replace(0x69C9F, "B801000000C3");
                var tick = Stub(0);
                m.SetPeace(false);
                Check(tick(player) == 0 && !m.ModeState.Active, "Normal player update remains enabled", report);
                Marshal.WriteInt32(image + ModeCode.Input, 2);
                Check(tick(player) == 0 && m.ModeState.Active, "Bomb action toggles time stop ON on the game update thread", report);
                for (int i = 0; i < 10; i++) tick(player);
                Check(m.ModeState.Active, "Holding Bomb does not repeatedly toggle time", report);
                Marshal.WriteByte(image + ModeCode.NativeTime, 1);
                Check(tick(player) == 0, "Player can move during our stop even if native Sakuya stop is set", report);
                Check(Marshal.ReadByte(image + ModeCode.NativeTime) == 1, "Native Sakuya flag is never overwritten", report);
                Marshal.WriteInt32(image + ModeCode.Input, 0); tick(player);
                Marshal.WriteInt32(image + ModeCode.Input, 2); tick(player);
                Check(!m.ModeState.Active && tick(player) == 1, "Second press restores time and preserves native enemy time stop", report);
                Marshal.WriteByte(image + ModeCode.NativeTime, 0);
                for (int i = 0; i < 30; i++)
                {
                    m.HeartbeatModes();
                    Marshal.WriteInt32(image + ModeCode.Input, 0); tick(player);
                    Marshal.WriteInt32(image + ModeCode.Input, 2); tick(player);
                }
                Check(!m.ModeState.Active, "30 press/release cycles preserve toggle parity", report);
                foreach (int index in new[] { 1, 10, 11, 12 })
                {
                    Marshal.WriteByte(control + ModeCode.Active, 0);
                    Marshal.WriteByte(image + ModeCode.NativeTime, 0);
                    Check(Stub(index)(bullets) == 0, $"Clock hook {index}: normal updates run", report);
                    Marshal.WriteByte(control + ModeCode.Active, 1);
                    Check(Stub(index)(bullets) == 1, $"Clock hook {index}: our stop skips updates", report);
                    Marshal.WriteByte(control + ModeCode.Active, 0);
                    Marshal.WriteByte(image + ModeCode.NativeTime, 1);
                    Check(Stub(index)(bullets) == 1, $"Clock hook {index}: native stop remains intact", report);
                }
                Marshal.WriteByte(image + ModeCode.NativeTime, 0);
                Marshal.WriteByte(control + ModeCode.Active, 1);
                Check(Stub(2)(IntPtr.Zero) == 1, "Frozen enemy callback exits before movement, script, collision and timer updates", report);
                Check(Stub(8)(IntPtr.Zero) == 1 && Stub(9)(IntPtr.Zero) == 1, "Frozen player projectile and firing paths reach safe epilogues", report);
                Marshal.WriteByte(control + ModeCode.Active, 0);
                Check(Stub(2)(IntPtr.Zero) == 2, "Unfrozen enemy callback follows original entry", report);
                Marshal.WriteByte(image + MemorySession.BombsRva, 5);
                Check(Stub(7)(IntPtr.Zero) == 0 && Marshal.ReadByte(image + MemorySession.BombsRva) == 5, "Sakuya suppresses native bomb activation without consuming Bomb", report);
                m.SetSakuya(false);
                Check(Stub(7)(IntPtr.Zero) == 5, "Disabling Sakuya restores native bomb eligibility", report);
                Marshal.WriteByte(image + MemorySession.BombsRva, 0);
                Marshal.WriteInt32(image + ModeCode.Input, 0);
                m.SetSakuya(true); tick(player);
                Marshal.WriteInt32(image + ModeCode.Input, 2); tick(player);
                Check(m.ModeState.Active && Marshal.ReadByte(image + MemorySession.BombsRva) == 0, "Time stop works with zero Bomb inventory", report);
                m.SetSakuya(false);
                m.SetPeace(true);
                for (int i = 0; i < 640; i++)
                {
                    Marshal.WriteInt16(bullets + 0x4C + i * 0x620, 4);
                    Marshal.WriteByte(bullets + 0x4E + i * 0x620, 0xA5);
                }
                for (int i = 0; i < 64; i++)
                {
                    Marshal.WriteByte(bullets + 0xF528C + i * 0x298, 1);
                    Marshal.WriteByte(bullets + 0xF528D + i * 0x298, 0xB6);
                }
                Stub(1)(bullets);
                Check(Enumerable.Range(0, 640).All(i => Marshal.ReadInt16(bullets + 0x4C + i * 0x620) == 0 && Marshal.ReadByte(bullets + 0x4E + i * 0x620) == 0xA5), "Peace clears all 640 enemy bullet states without touching neighboring bytes", report);
                Check(Enumerable.Range(0, 64).All(i => Marshal.ReadByte(bullets + 0xF528C + i * 0x298) == 0 && Marshal.ReadByte(bullets + 0xF528D + i * 0x298) == 0xB6), "Peace clears all 64 lasers without touching neighboring bytes", report);
                Check(Stub(3)(IntPtr.Zero) == 0, "Peace rejects enemy bullet spawning before allocation", report);
                Check(Stub(5)(IntPtr.Zero) == 0 && Stub(6)(IntPtr.Zero) == 0, "Peace disables contact and laser hit entry paths", report);
                // Wrapper supplies RBX expected by the actual laser completion hook.
                byte[] wrapper = Convert.FromHexString("534883EC20488BD9B90100000048B8")
                    .Concat(BitConverter.GetBytes(block + 4 * 0x400))
                    .Concat(Convert.FromHexString("FFD04883C4205BC3")).ToArray();
                IntPtr wrapperPtr = VirtualAlloc(IntPtr.Zero, 4096, 0x3000, 0x40);
                try
                {
                    Marshal.Copy(wrapper, 0, wrapperPtr, wrapper.Length);
                    var laser = Marshal.GetDelegateForFunctionPointer<Native>(wrapperPtr);
                    Marshal.WriteByte(bullets + 0x274, 1); laser(bullets);
                    Check(Marshal.ReadByte(bullets + 0x274) == 0 && Marshal.ReadByte(bullets + 0x290) == 1, "New lasers keep a valid object but become inactive before return", report);
                    m.SetPeace(false);
                    Marshal.WriteByte(bullets + 0x274, 1); laser(bullets);
                    Check(Marshal.ReadByte(bullets + 0x274) == 1, "Laser activation is preserved with peace disabled", report);
                }
                finally { VirtualFree(wrapperPtr, 0, 0x8000); }
                m.SetSakuya(true);
                Marshal.WriteByte(control + ModeCode.Active, 1);
                Marshal.WriteByte(player + 0x7898, 2); tick(player);
                Check(!m.ModeState.Active, "Entering hit/death state safely cancels time stop", report);
                Marshal.WriteByte(player + 0x7898, 0);
                Marshal.WriteByte(control + ModeCode.Active, 1);
                Marshal.WriteInt32(gui + 0x36B0, 0); tick(player);
                Check(!m.ModeState.Active, "Dialogue cancels time stop", report);
                Marshal.WriteInt32(gui + 0x36B0, -1);
                Marshal.WriteByte(control + ModeCode.Active, 1);
                Marshal.WriteInt32(image + ModeCode.Scene, 1); tick(player);
                Check(!m.ModeState.Active, "Leaving gameplay cancels time stop", report);
                Marshal.WriteInt32(image + ModeCode.Scene, 2);
                m.SetPeace(true);
                Marshal.WriteByte(control + ModeCode.Active, 1);
                Marshal.WriteInt32(control + ModeCode.Lease, 0); tick(player);
                Check(m.ModeState == (false, false, false), "Expired trainer heartbeat cancels both modes without touching native time", report);
                m.SetPeace(true); m.SetSakuya(true); m.SetInvincible(true);
                m.VerifyModes(); m.VerifySignature(true);
                m.SetInvincible(false); m.VerifyModes();
                Check(m.ModeState.Peace && m.ModeState.Enabled, "Original invincibility toggles independently of both new modes", report);
                m.DisableModes();
                Check(m.ModeState == (false, false, false), "Disable-all clears every mode flag", report);
                foreach (var (address, bytes) in saved.AsEnumerable().Reverse()) WriteCode(address, bytes);
                saved.Clear();
            }
            foreach (var site in ModeCode.Sites)
            {
                byte[] bytes = new byte[site.Original.Length]; Marshal.Copy(image + site.Rva, bytes, 0, bytes.Length);
                Check(bytes.SequenceEqual(site.Original), $"Dispose restores exact original bytes at {site.Rva:X}", report);
            }
            using (var again = new MemorySession(image, imageSize)) { again.SetPeace(true); again.SetSakuya(true); }
            Check(true, "A fresh session can reinstall and remove every hook", report);
        }
        finally
        {
            foreach (var (address, bytes) in saved.AsEnumerable().Reverse()) WriteCode(address, bytes);
            Marshal.FreeHGlobal(player); Marshal.FreeHGlobal(gui); Marshal.FreeHGlobal(bullets);
        }
    }
    private static void WriteCode(IntPtr address, byte[] bytes)
    {
        if (!VirtualProtect(address, (nuint)bytes.Length, 0x40, out uint old)) throw new InvalidOperationException("Test protection failed");
        Marshal.Copy(bytes, 0, address, bytes.Length);
        VirtualProtect(address, (nuint)bytes.Length, old, out _);
        FlushInstructionCache(GetCurrentProcess(), address, (nuint)bytes.Length);
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr h, IntPtr address, nuint size);
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, nuint size, uint type);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtect(IntPtr address, nuint size, uint protection, out uint old);
}
