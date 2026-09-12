using System.Runtime.InteropServices;
using System.Text;

namespace TH06NCTrainer;

internal static class PracticeSelfTest
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Native(IntPtr arg);
    internal static void Run(IntPtr image, int imageSize, StringBuilder report)
    {
        void Check(bool ok, string title)
        {
            if (!ok) throw new InvalidOperationException(title);
            report.AppendLine("PASS: " + title); Console.WriteLine("PASS: " + title);
        }
        void Float(IntPtr address, float value) => Marshal.WriteInt32(address, BitConverter.SingleToInt32Bits(value));
        IntPtr manager = Marshal.AllocHGlobal(0x110000), gui = Marshal.AllocHGlobal(0x4000);
        IntPtr player = image + 0x4FF3A0;
        byte[] playerSaved = new byte[0xA400]; Marshal.Copy(player, playerSaved, 0, playerSaved.Length);
        IntPtr guiSaved = Marshal.ReadIntPtr(image + ModeCode.Gui);
        var saved = new List<(IntPtr Address, byte[] Bytes)>();
        void Replace(int rva, string hex)
        {
            byte[] bytes = Convert.FromHexString(hex), original = new byte[bytes.Length];
            Marshal.Copy(image + rva, original, 0, original.Length);
            saved.Add((image + rva, original)); WriteCode(image + rva, bytes);
        }
        try
        {
            Marshal.Copy(new byte[0x110000], 0, manager, 0x110000);
            Marshal.Copy(new byte[0x4000], 0, gui, 0x4000);
            Marshal.Copy(new byte[0xA400], 0, player, 0xA400);
            Marshal.WriteIntPtr(image + ModeCode.Gui, gui);
            Marshal.WriteInt32(gui + 0x36B0, -1);
            Marshal.WriteInt32(image + ModeCode.Scene, 2);
            using var m = new MemorySession(image, imageSize);
            m.SetPeace(true); m.SetSakuya(true, true);
            IntPtr control = (IntPtr)m.Control;
            Marshal.WriteByte(control + ModeCode.Active, 1);
            Marshal.WriteInt32(image + ModeCode.Scene, 3); m.MaintainModes();
            Check(m.ModeState == (true, true, false) && m.AttackAllowed, "Stage loading retains peace and chosen Sakuya variant, cancels active stop");
            Marshal.WriteInt32(image + ModeCode.Scene, 2); m.MaintainModes();
            Check(m.ModeState == (true, true, false) && m.AttackAllowed, "Entering next stage keeps both mode switches armed");
            Marshal.WriteInt32(image + ModeCode.Scene, 1); m.MaintainModes();
            Check(m.ModeState == (false, false, false), "Returning to title disables new modes");
            Marshal.WriteInt32(image + ModeCode.Scene, 2);
            m.SetPeace(true); m.SetSakuya(true, true);
            Marshal.WriteByte(control + ModeCode.Active, 1);
            m.SetSakuya(true, false);
            Check(m.ModeState.Active && !m.AttackAllowed, "Switching to nonattack keeps existing stop active");
            m.SetSakuya(true, true);
            Check(m.ModeState.Active && m.AttackAllowed, "Switching to attack keeps existing stop active");
            Replace(0x76A3B, "8BC1C3");
            var sound = Marshal.GetDelegateForFunctionPointer<Native>((IntPtr)(m.ModeBlock + 18 * 0x400));
            void Queue(params int[] ids) { for (int i = 0; i < 3; i++) Marshal.WriteInt32(image + 0x509670 + i * 4, ids[i]); }
            int[] ReadQueue() => Enumerable.Range(0, 3).Select(i => Marshal.ReadInt32(image + 0x509670 + i * 4)).ToArray();
            Marshal.WriteByte(image + 0xC21DB7, 75);
            foreach (int id in ModeCode.EnemyShotSounds)
            {
                Queue(id, 0, 21);
                Check(sound(IntPtr.Zero) == 75 && ReadQueue().SequenceEqual(new[] { 0, 21, -1 }), $"Peace filters sound {id}, compacts queue, preserves shot/item sounds and volume");
            }
            foreach (int id in Enumerable.Range(0, 32).Except(ModeCode.EnemyShotSounds))
            {
                Queue(id, -1, -1); sound(IntPtr.Zero);
                Check(ReadQueue().SequenceEqual(new[] { id, -1, -1 }), $"Peace preserves non-enemy sound {id}");
            }
            Queue(7, 16, 24); sound(IntPtr.Zero);
            Check(ReadQueue().All(i => i == -1), "All-enemy sound queue becomes empty");
            m.SetPeace(false); Queue(7, 16, 24); sound(IntPtr.Zero);
            Check(ReadQueue().SequenceEqual(new[] { 7, 16, 24 }), "Peace OFF preserves enemy sounds unchanged");
            // Execute the relocated CALL with an isolated callee and return continuation.
            Replace(0x36260, "B82A000000C3"); Replace(0x37411, "C3");
            var timeline = Marshal.GetDelegateForFunctionPointer<Native>((IntPtr)(m.ModeBlock + 13 * 0x400));
            Marshal.WriteByte(control + ModeCode.Active, 0);
            Check(timeline(IntPtr.Zero) == 42, "Inactive time stop preserves relocated native CALL and return stack");
            Marshal.WriteByte(control + ModeCode.Active, 1);
            // Restore these test continuations before executing the real enemy callback below.
            foreach (var entry in saved.TakeLast(2).Reverse()) WriteCode(entry.Address, entry.Bytes);
            saved.RemoveRange(saved.Count - 2, 2);
            IntPtr enemy = manager + 8;
            Marshal.WriteByte(enemy + 0xBC, 0x80); Marshal.WriteByte(enemy + 0xBD, 0x15);
            Marshal.WriteInt32(enemy + 0x234, 100); Marshal.WriteInt32(enemy + 0x238, 100);
            Marshal.WriteInt32(enemy + 0x23C, 100); Marshal.WriteInt32(enemy + 0xA0, -1);
            Float(enemy + 0xB0, 192); Float(enemy + 0xB4, 100);
            Float(enemy + 0x2C0, 20); Float(enemy + 0x2C4, 20);
            Marshal.WriteInt32(enemy, 123); Marshal.WriteInt32(enemy + 8, 123);
            Marshal.WriteInt32(manager + 0x10C0B8, 776); Marshal.WriteInt32(manager + 0x10C0BC, 777);
            Marshal.WriteInt16(player + 0x420, 2); Marshal.WriteInt16(player + 0x422, 2);
            Float(player + 0x54C, 192); Float(player + 0x550, 100);
            Marshal.WriteInt16(player + 0x56C, 24);
            Float(player + 0x570, 10); Float(player + 0x574, 10);
            var update = Marshal.GetDelegateForFunctionPointer<Native>(image + 0x373B0);
            m.SetSakuya(true, false); Marshal.WriteByte(control + ModeCode.Active, 1);
            Check(update(manager) == 1 && Marshal.ReadInt32(enemy + 0x234) == 100, "Nonattack stop does not damage enemy");
            m.SetSakuya(true, true);
            Console.WriteLine("CHECK: full native enemy update with attack stop");
            int result = update(manager);
            Check(result == 1 && Marshal.ReadInt32(enemy + 0x234) == 76, "Attack stop executes actual native enemy damage: HP 100 -> 76");
            Check(Marshal.ReadInt32(enemy + 0xB0) == BitConverter.SingleToInt32Bits(192) && Marshal.ReadInt32(enemy + 0xB4) == BitConverter.SingleToInt32Bits(100), "Attack stop preserves enemy position without executing ECL");
            Check(Marshal.ReadInt32(enemy) == 123 && Marshal.ReadInt32(enemy + 8) == 123 && Marshal.ReadInt32(manager + 0x10C0B8) == 776 && Marshal.ReadInt32(manager + 0x10C0BC) == 777, "Attack stop preserves enemy and stage timers");
            // Test the phase guard separately to avoid executing unrelated boss death/ECL callbacks.
            Replace(0x37B8D, "0F9EC00FB6C0C3");
            byte[] wrapper = Convert.FromHexString("534883EC20488BD948B8").Concat(BitConverter.GetBytes(m.ModeBlock + 17 * 0x400)).Concat(Convert.FromHexString("FFD04883C4205BC3")).ToArray();
            IntPtr thunk = VirtualAlloc(IntPtr.Zero, 4096, 0x3000, 0x40);
            try
            {
                Marshal.Copy(wrapper, 0, thunk, wrapper.Length);
                var guard = Marshal.GetDelegateForFunctionPointer<Native>(thunk);
                Marshal.WriteByte(enemy + 0xBD, 0x1D); Marshal.WriteInt32(enemy + 0xA0, 90);
                guard(enemy);
                Check(!m.ModeState.Active && m.ModeState.Enabled, "Boss HP threshold resumes time without disarming Sakuya");
                Marshal.WriteByte(control + ModeCode.Active, 1); Marshal.WriteInt32(enemy + 0x234, 0); guard(enemy);
                Check(!m.ModeState.Active, "Boss zero HP resumes time");
                Marshal.WriteByte(control + ModeCode.Active, 1); Marshal.WriteInt32(enemy + 0x234, 90); guard(enemy);
                Check(m.ModeState.Active, "Boss threshold equality preserves native strict-less-than semantics");
            }
            finally { VirtualFree(thunk, 0, 0x8000); }
        }
        finally
        {
            foreach (var (address, bytes) in saved.AsEnumerable().Reverse()) WriteCode(address, bytes);
            Marshal.Copy(playerSaved, 0, player, playerSaved.Length);
            Marshal.WriteIntPtr(image + ModeCode.Gui, guiSaved);
            Marshal.FreeHGlobal(manager); Marshal.FreeHGlobal(gui);
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
