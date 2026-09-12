using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TH06NCTrainer;

internal static class RemoteModeTest
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Tick(IntPtr player);
    internal static void Host(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        if (Convert.ToHexString(SHA256.HashData(file)) != MemorySession.SupportedHash) throw new InvalidOperationException("Wrong test image");
        int pe = BitConverter.ToInt32(file, 0x3c), opt = pe + 24;
        int size = BitConverter.ToInt32(file, opt + 56);
        IntPtr image = VirtualAlloc(IntPtr.Zero, (nuint)size, 0x3000, 0x04);
        if (image == IntPtr.Zero) throw new InvalidOperationException("Test map failed");
        IntPtr player = Marshal.AllocHGlobal(0xA400), gui = Marshal.AllocHGlobal(0x4000);
        IntPtr frameWrapper = VirtualAlloc(IntPtr.Zero, 4096, 0x3000, 0x40);
        using var stop = new CancellationTokenSource();
        Task? worker = null;
        try
        {
            int count = BitConverter.ToUInt16(file, pe + 6), table = opt + BitConverter.ToUInt16(file, pe + 20);
            for (int i = 0; i < count; i++)
            {
                int header = table + i * 40, rva = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16), rawOffset = BitConverter.ToInt32(file, header + 20);
                if (rawSize > 0) Marshal.Copy(file, rawOffset, image + rva, rawSize);
                if ((BitConverter.ToUInt32(file, header + 36) & 0x20000000) != 0)
                    VirtualProtect(image + rva, (nuint)Math.Max(rawSize, BitConverter.ToInt32(file, header + 8)), 0x20, out _);
            }
            Marshal.Copy(new byte[0xA400], 0, player, 0xA400);
            Marshal.Copy(new byte[0x4000], 0, gui, 0x4000);
            Marshal.WriteInt32(gui + 0x36B0, -1);
            Marshal.WriteIntPtr(image + ModeCode.Gui, gui);
            Marshal.WriteInt32(image + ModeCode.Scene, 2);
            Marshal.WriteInt32(image + ModeCode.Input, 0);
            Marshal.WriteByte(image + ModeCode.NativeTime, 0);
            byte[] continuation = Convert.FromHexString("0F95C00FB6C0C3");
            VirtualProtect(image + 0x68834, (nuint)continuation.Length, 0x40, out uint old);
            Marshal.Copy(continuation, 0, image + 0x68834, continuation.Length);
            VirtualProtect(image + 0x68834, (nuint)continuation.Length, old, out _);
            VirtualProtect(image + 0x3C46C, (nuint)continuation.Length, 0x40, out old);
            Marshal.Copy(continuation, 0, image + 0x3C46C, continuation.Length);
            VirtualProtect(image + 0x3C46C, (nuint)continuation.Length, old, out _);
            Marshal.WriteInt64(image + ModeCode.FramePeriod, 60000);
            byte[] wrapper = Convert.FromHexString("41574883EC204533FF48B8").Concat(BitConverter.GetBytes((image + 0x3C465).ToInt64())).Concat(Convert.FromHexString("FFD04883C420415FC3")).ToArray();
            Marshal.Copy(wrapper, 0, frameWrapper, wrapper.Length);
            var frame = Marshal.GetDelegateForFunctionPointer<Tick>(frameWrapper);
            var tick = Marshal.GetDelegateForFunctionPointer<Tick>(image + 0x6882D);
            worker = Task.Run(() => { while (!stop.IsCancellationRequested) { tick(player); frame(IntPtr.Zero); Thread.Sleep(5); } });
            Console.WriteLine($"READY {image.ToInt64()} {size}"); Console.Out.Flush();
            Task.WhenAny(Task.Run(() => Console.ReadLine()), Task.Delay(30000)).GetAwaiter().GetResult();
        }
        finally
        {
            stop.Cancel(); worker?.GetAwaiter().GetResult();
            Marshal.FreeHGlobal(player); Marshal.FreeHGlobal(gui); VirtualFree(image, 0, 0x8000);
            VirtualFree(frameWrapper, 0, 0x8000);
        }
    }
    internal static void Run(string gamePath, StringBuilder report)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardInput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--native-host"); info.ArgumentList.Add(gamePath);
        using var child = Process.Start(info) ?? throw new InvalidOperationException("Cannot start isolated test host");
        void Check(bool ok, string text)
        {
            if (!ok) throw new InvalidOperationException(text);
            report.AppendLine("PASS: " + text); Console.WriteLine("PASS: " + text);
        }
        static void Until(Func<bool> predicate)
        {
            long deadline = Environment.TickCount64 + 3000;
            while (!predicate())
            {
                if (Environment.TickCount64 >= deadline) throw new TimeoutException("Remote hook state did not settle");
                Thread.Sleep(10);
            }
        }
        try
        {
            string line = child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult() ?? "";
            string[] fields = line.Split(' ');
            if (fields.Length != 3 || fields[0] != "READY") throw new InvalidOperationException("Bad test host handshake: " + line);
            long image = long.Parse(fields[1]); int size = int.Parse(fields[2]);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                using var m = new MemorySession(child.Id, image, size);
                m.SetSakuya(true); m.SetPeace(true);
                m.Write(ModeCode.Input, BitConverter.GetBytes(0)); Thread.Sleep(20);
                m.Write(ModeCode.Input, BitConverter.GetBytes(2)); Until(() => m.ModeState.Active);
                Check(m.ModeState == (true, true, true), $"Live test host cycle {cycle}: safely suspend threads, install hooks and activate time stop");
                m.Write(ModeCode.Input, BitConverter.GetBytes(0)); Thread.Sleep(20);
                m.Write(ModeCode.Input, BitConverter.GetBytes(2)); Until(() => !m.ModeState.Active);
                Check(m.ModeState.Enabled, $"Live test host cycle {cycle}: second Bomb action resumes time");
                m.SetSpeed(50);
                Until(() => BitConverter.ToInt64(m.Read(ModeCode.FramePeriod, 8)) == 120000);
                Check(m.DesiredSpeed == 50, $"Live test host cycle {cycle}: independent half-speed applied on native frame thread");
                m.Write(ModeCode.Scene, BitConverter.GetBytes(3)); m.MaintainModes();
                Until(() => BitConverter.ToInt64(m.Read(ModeCode.FramePeriod, 8)) == 60000);
                Check(m.DesiredSpeed == 50 && m.ModeState.Peace && m.ModeState.Enabled, $"Live test host cycle {cycle}: loading keeps speed choice and gameplay modes");
                m.Write(ModeCode.Scene, BitConverter.GetBytes(2)); m.MaintainModes();
                Until(() => BitConverter.ToInt64(m.Read(ModeCode.FramePeriod, 8)) == 120000);
                if (cycle == 0)
                {
                    Until(() => !m.ModeState.Enabled);
                    Check(m.ModeState == (false, false, false), "Live test host heartbeat expiry clears both modes while its update thread keeps running");
                    Until(() => m.DesiredSpeed == 100 && BitConverter.ToInt64(m.Read(ModeCode.FramePeriod, 8)) == 60000);
                    Check(true, "Live test host restores normal speed after independent heartbeat expiry");
                }
                m.Write(ModeCode.Input, BitConverter.GetBytes(0));
            }
            using (var m = new MemorySession(child.Id, image, size))
            {
                Check(ModeCode.Sites.All(site => m.Read(site.Rva, site.Original.Length).SequenceEqual(site.Original)), "Cross-process Dispose restored every original instruction after three install/remove cycles");
                Check(BitConverter.ToInt64(m.Read(ModeCode.FramePeriod, 8)) == 60000, "Cross-process Dispose restores native frame period while host keeps running");
            }
            Check(!child.HasExited, "Isolated native host is still running after cross-process patch tests");
            using (var ended = new MemorySession(child.Id, image, size))
            {
                ended.SetPeace(true); ended.SetSakuya(true); ended.SetOverdrive(true);
                ended.SetOpacity(30, 60); ended.SetInvincible(true);
                child.StandardInput.WriteLine("quit"); child.StandardInput.Flush();
                Check(child.WaitForExit(5000), "Test game exits first while all feature families are installed");
                Check(!ended.IsAlive && ended.ModeState == (false, false, false) && !ended.AttackAllowed && !ended.IsOverdrive && ended.DesiredSpeed == 100 && ended.EffectiveSpeed == 100, "After game exit, status getters never read released process memory");
                using var form = new TrainerForm(false);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(TrainerForm).GetField("session", flags)!.SetValue(form, ended);
                var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                typeof(TrainerForm).GetMethod("OnClosing", flags | System.Reflection.BindingFlags.DeclaredOnly)!.Invoke(form, [form, closing]);
                Check(!closing.Cancel && !ended.ModesInstalled && !ended.OwnsPatch, "Actual form shutdown succeeds after game exit with patches still recorded");
                ended.Dispose();
                Check(true, "Repeated cleanup after process exit is idempotent");
            }
        }
        finally
        {
            if (!child.HasExited)
            {
                child.StandardInput.WriteLine("quit"); child.StandardInput.Flush();
                if (!child.WaitForExit(5000)) child.Kill(); // Only the private test process created above.
            }
        }
    }
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, nuint size, uint type);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtect(IntPtr address, nuint size, uint protection, out uint old);
}
