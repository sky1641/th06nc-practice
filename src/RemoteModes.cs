using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TH06NCTrainer;

internal sealed partial class MemorySession
{
    private long modeBlock;
    private readonly List<(ModeCode.Site Site, byte[] Patch)> modePatches = [];
    internal long ModeBlock => modeBlock;
    internal long Control => modeBlock + ModeCode.ControlOffset;
    // Only used by the isolated child-process test, never by the normal attachment path.
    internal MemorySession(int testPid, long testImage, int testSize)
    {
        moduleBase = testImage; moduleSize = testSize; Pid = testPid;
        handle = OpenProcess(0x0400 | 0x0008 | 0x0010 | 0x0020 | 0x00100000, false, Pid);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        VerifySignature(false);
    }
    public bool ModesInstalled => modeBlock != 0;
    public (bool Peace, bool Enabled, bool Active) ModeState
    {
        get
        {
            if (!ModesInstalled || !IsAlive) return (false, false, false);
            var state = ReadRemote(Control, 3);
            return (state[0] != 0, state[1] != 0, state[2] != 0);
        }
    }
    public bool InGame => BitConverter.ToInt32(Read(ModeCode.Scene, 4)) == 2;
    public bool AttackAllowed => ModesInstalled && IsAlive && ReadRemote(Control + ModeCode.Attack, 1)[0] != 0;

    public void MaintainModes()
    {
        if (!ModesInstalled) return;
        int scene = BitConverter.ToInt32(Read(ModeCode.Scene, 4));
        if (scene == 3) ResumeTime(); // Stage/retry reinitialization: retain the user's armed modes.
        else if (scene != 2) DisableModes();
        HeartbeatModes();
        VerifyModes();
    }

    public void SetPeace(bool enabled)
    {
        if (enabled) { RequireGame(); InstallModes(); HeartbeatModes(); }
        if (ModesInstalled) WriteRemote(Control + ModeCode.Peace, [enabled ? (byte)1 : (byte)0]);
    }
    public void SetSakuya(bool enabled, bool canAttack = false)
    {
        if (enabled)
        {
            RequireGame(); InstallModes(); HeartbeatModes();
            WriteRemote(Control + ModeCode.Latch, [(byte)((Read(ModeCode.Input, 1)[0] >> 1) & 1)]);
        }
        if (ModesInstalled)
        {
            WriteRemote(Control + ModeCode.Attack, [canAttack ? (byte)1 : (byte)0]);
            WriteRemote(Control + ModeCode.Enabled, [enabled ? (byte)1 : (byte)0]);
            if (!enabled) WriteRemote(Control + ModeCode.Active, [0]);
        }
    }
    public void ResumeTime()
    {
        if (ModesInstalled) WriteRemote(Control + ModeCode.Active, [0]);
    }
    public void DisableModes()
    {
        if (!ModesInstalled || !IsAlive) return;
        WriteRemote(Control + ModeCode.Enabled, [0]);
        WriteRemote(Control + ModeCode.Active, [0]);
        WriteRemote(Control + ModeCode.Peace, [0]);
    }
    private void RequireGame()
    {
        if (!InGame) throw new InvalidOperationException(L.T("请先进入一局，再启用此功能。", "Start a run before enabling this feature."));
    }
    public void HeartbeatModes()
    {
        if (ModesInstalled)
        {
            int frames = IsOverdrive ? 1920 : 120;
            WriteRemote(Control + ModeCode.Lease, BitConverter.GetBytes(frames));
            WriteRemote(Control + ModeCode.SpeedLease, BitConverter.GetBytes(frames));
        }
    }
    public void VerifyModes()
    {
        foreach (var (site, patch) in modePatches)
            if (!Read(site.Rva, patch.Length).SequenceEqual(patch))
                throw new InvalidOperationException(L.T("玩法指令被其他程序改变，请关闭游戏后重新启动。", "Another program changed the mode instructions. Close and restart the game."));
    }

    internal void InstallModes()
    {
        if (ModesInstalled) { VerifyModes(); return; }
        foreach (var site in ModeCode.Sites)
            if (!Read(site.Rva, site.Original.Length).SequenceEqual(site.Original))
                throw new InvalidOperationException(L.T($"指令校验失败（{site.Rva:X}），未启用功能。", $"Instruction check failed ({site.Rva:X}); feature not enabled."));
        // Allocate close enough for rel32 jumps and RIP-relative accesses.
        long first = (moduleBase + moduleSize + 0xFFFF) & ~0xFFFFL;
        for (long address = first; address < moduleBase + 0x60000000; address += 0x10000)
        {
            var allocated = VirtualAllocEx(handle, (IntPtr)address, ModeCode.AllocationSize, 0x3000, 0x04);
            if (allocated != IntPtr.Zero) { modeBlock = allocated.ToInt64(); break; }
        }
        if (!ModesInstalled) throw new InvalidOperationException(L.T("无法分配玩法模块内存，没有修改游戏指令。", "Could not allocate mode memory. Game instructions were not changed."));
        try
        {
            WriteRemote(Control + ModeCode.PlayerOpacity, BitConverter.GetBytes(100));
            WriteRemote(Control + ModeCode.EnemyOpacity, BitConverter.GetBytes(100));
            for (int index = 0; index < ModeCode.Sites.Length; index++)
            {
                var site = ModeCode.Sites[index];
                long stub = modeBlock + index * 0x400;
                byte[] code = ModeCode.Build(site, stub, moduleBase, Control);
                if (code.Length > 0x400) throw new InvalidOperationException(L.T("原生补丁超出预留空间。", "Native hook exceeds reserved slot."));
                WriteRemote(stub, code);
            }
            if (!VirtualProtectEx(handle, (IntPtr)modeBlock, 0x8000, 0x20, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!FlushInstructionCache(handle, (IntPtr)modeBlock, 0x8000))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            using var guard = QuiesceModes();
            try
            {
                // Recheck after stopping threads, before any code writes.
                foreach (var site in ModeCode.Sites)
                    if (!Read(site.Rva, site.Original.Length).SequenceEqual(site.Original))
                        throw new InvalidOperationException(L.T("安装前指令发生变化，已取消。", "Instructions changed before installation. Cancelled."));
                for (int index = 0; index < ModeCode.Sites.Length; index++)
                {
                    var site = ModeCode.Sites[index];
                    byte[] patch = Enumerable.Repeat((byte)0x90, site.Original.Length).ToArray();
                    patch[0] = 0xE9;
                    BitConverter.GetBytes(checked((int)(modeBlock + index * 0x400 - moduleBase - site.Rva - 5))).CopyTo(patch, 1);
                    modePatches.Add((site, patch));
                    WriteCode(site.Rva, patch);
                }
                VerifyModes();
            }
            catch
            {
                foreach (var (site, _) in modePatches.AsEnumerable().Reverse()) WriteCode(site.Rva, site.Original);
                modePatches.Clear();
                throw;
            }
        }
        catch
        {
            if (modePatches.Count == 0) FreeModeBlock();
            throw;
        }
    }

    private void RemoveModes()
    {
        if (!ModesInstalled) return;
        if (!IsAlive) { modeBlock = 0; modePatches.Clear(); return; }
        DisableModes();
        RestoreSpeed();
        using var guard = QuiesceModes();
        VerifyModes();
        for (int i = modePatches.Count - 1; i >= 0; i--)
        {
            var (site, _) = modePatches[i];
            WriteCode(site.Rva, site.Original);
            modePatches.RemoveAt(i);
        }
        FreeModeBlock();
    }
    private void FreeModeBlock()
    {
        if (modeBlock != 0 && !VirtualFreeEx(handle, (IntPtr)modeBlock, 0, 0x8000))
            throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("无法释放玩法模块内存", "Could not free mode memory."));
        modeBlock = 0;
    }
    private byte[] ReadRemote(long address, int count)
    {
        byte[] data = new byte[count];
        if (!ReadProcessMemory(handle, (IntPtr)address, data, (nuint)count, out nuint read) || read != (nuint)count)
            throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("读取玩法状态失败", "Could not read mode state."));
        return data;
    }
    private void WriteRemote(long address, byte[] bytes)
    {
        if (!WriteProcessMemory(handle, (IntPtr)address, bytes, (nuint)bytes.Length, out nuint written) || written != (nuint)bytes.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("写入玩法状态失败", "Could not write mode state."));
    }
    private void WriteCode(int rva, byte[] bytes)
    {
        IntPtr address = (IntPtr)(moduleBase + rva);
        if (!VirtualProtectEx(handle, address, (nuint)bytes.Length, 0x40, out uint old))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            Write(rva, bytes);
            if (!FlushInstructionCache(handle, address, (nuint)bytes.Length)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (!VirtualProtectEx(handle, address, (nuint)bytes.Length, old, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private IDisposable QuiesceModes()
    {
        // Private-image tests are single-threaded and do not execute the image concurrently.
        if (Pid == Environment.ProcessId) return new ThreadGuard([]);
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var suspended = new List<SafeWaitHandle>();
            bool conflict = false;
            IntPtr raw = Marshal.AllocHGlobal(1248);
            IntPtr context = (IntPtr)((raw.ToInt64() + 15) & ~15L);
            try
            {
                using var process = Process.GetProcessById(Pid);
                foreach (ProcessThread thread in process.Threads)
                {
                    using (thread)
                    {
                        var h = OpenThread(0x0002 | 0x0008 | 0x0040, false, thread.Id);
                        if (h.IsInvalid) { h.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
                        if (SuspendThread(h) == uint.MaxValue) { h.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
                        suspended.Add(h);
                        Marshal.WriteInt32(context, 48, 0x00100001); // CONTEXT_AMD64 | CONTROL
                        if (!GetThreadContext(h, context)) throw new Win32Exception(Marshal.GetLastWin32Error());
                        long rip = Marshal.ReadInt64(context, 248);
                        if ((modeBlock != 0 && rip >= modeBlock && rip < modeBlock + 0x8000) ||
                            ModeCode.Sites.Any(s => rip >= moduleBase + s.Rva && rip < moduleBase + s.Rva + s.Original.Length))
                            conflict = true;
                    }
                }
                if (!conflict) return new ThreadGuard(suspended);
            }
            catch { new ThreadGuard(suspended).Dispose(); throw; }
            finally { Marshal.FreeHGlobal(raw); }
            new ThreadGuard(suspended).Dispose();
            Thread.Sleep(2);
        }
        throw new InvalidOperationException(L.T("游戏正在执行玩法代码，请稍后重试或先退出游戏。", "The game is executing mode code. Try again later or close the game first."));
    }
    private sealed class ThreadGuard(List<SafeWaitHandle> threads) : IDisposable
    {
        public void Dispose()
        {
            foreach (var thread in threads.AsEnumerable().Reverse()) { ResumeThread(thread); thread.Dispose(); }
            threads.Clear();
        }
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAllocEx(SafeProcessHandle h, IntPtr address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFreeEx(SafeProcessHandle h, IntPtr address, nuint size, uint freeType);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeWaitHandle OpenThread(uint access, bool inherit, int tid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint SuspendThread(SafeWaitHandle thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(SafeWaitHandle thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetThreadContext(SafeWaitHandle thread, IntPtr context);
}
