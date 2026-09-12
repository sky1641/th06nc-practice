using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace TH06NCTrainer;

internal enum Resource { Lives, Bombs, Power }

internal sealed partial class MemorySession : IDisposable
{
    public const string SupportedHash = "07850C8C6E469C0E82C13423E6D0D096A88D693455BDACACBB44C0AA3BCCE473";
    public const int LivesRva = 0x4FF0F0, BombsRva = 0x4FF0F1, PowerRva = 0x4F1E88;
    public const int CollisionRva = 0x6A980, PatchRva = 0x6AA9D, SignatureRva = 0x6AA97;
    public static readonly byte[] OriginalSignature = Convert.FromHexString("0F97C084C0750733C0E9CC00000080BB9878000000");
    public const int LaserCollisionRva = 0x6ABA0, LaserPatchRva = 0x6ACF2, LaserSignatureRva = 0x6ACEA;
    public static readonly byte[] LaserSignature = Convert.FromHexString("803D47BF4900000F852E02000041B901000000");
    private sealed record CodePatch(int Rva, int SignatureRva, byte[] Original, byte Replacement);
    private static readonly CodePatch[] Patches =
    [
        new(PatchRva, SignatureRva, OriginalSignature, 0),
        new(LaserPatchRva, LaserSignatureRva, LaserSignature, 0x83)
    ];
    private readonly HashSet<int> owned = [];
    private readonly SafeProcessHandle handle;
    private readonly long moduleBase;
    private readonly int moduleSize;
    private bool disposed;
    public bool OwnsPatch => owned.Count > 0;
    public int Pid { get; }

    public MemorySession(Process process)
    {
        var module = process.MainModule ?? throw new InvalidOperationException(L.T("无法读取游戏模块", "Could not read the game module."));
        using (var file = File.OpenRead(module.FileName))
        {
            if (Convert.ToHexString(SHA256.HashData(file)) != SupportedHash)
                throw new InvalidOperationException(L.T("游戏版本与地址配置不匹配，请更新修改器。", "Unsupported game build. Please update the helper."));
        }
        moduleBase = module.BaseAddress.ToInt64();
        moduleSize = module.ModuleMemorySize;
        Pid = process.Id;
        handle = OpenProcess(0x0400 | 0x0008 | 0x0010 | 0x0020 | 0x00100000, false, Pid);
        if (handle.IsInvalid)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            handle.Dispose();
            throw new InvalidOperationException(L.T("无法打开游戏进程：", "Could not open the game process: ") + L.Error(error));
        }
        try { VerifySignature(false); }
        catch { handle.Dispose(); throw; }
    }

    // Tests use a private mapped copy of the exact supported game image.
    internal MemorySession(IntPtr mappedImage, int size)
    {
        moduleBase = mappedImage.ToInt64();
        moduleSize = size;
        Pid = Environment.ProcessId;
        handle = OpenProcess(0x0400 | 0x0008 | 0x0010 | 0x0020 | 0x00100000, false, Pid);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        VerifySignature(false);
    }

    public bool IsAlive => !disposed && WaitForSingleObject(handle, 0) == 0x102;
    public static (int Rva, int Width, int Maximum) Spec(Resource kind) => kind switch
    {
        Resource.Lives => (LivesRva, 1, 8),
        Resource.Bombs => (BombsRva, 1, 8),
        Resource.Power => (PowerRva, 2, 128),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public int ReadResource(Resource kind)
    {
        var spec = Spec(kind);
        var data = Read(spec.Rva, spec.Width);
        return spec.Width == 1 ? data[0] : BitConverter.ToUInt16(data);
    }

    public void WriteResource(Resource kind, int value)
    {
        var spec = Spec(kind);
        if (value < 0 || value > spec.Maximum) throw new ArgumentOutOfRangeException(nameof(value));
        if (ReadResource(kind) == value) return;
        Write(spec.Rva, spec.Width == 1 ? [(byte)value] : BitConverter.GetBytes((ushort)value));
    }

    internal byte[] Read(int rva, int count)
    {
        CheckRange(rva, count);
        byte[] data = new byte[count];
        if (!ReadProcessMemory(handle, (IntPtr)(moduleBase + rva), data, (nuint)count, out nuint read) || read != (nuint)count)
            throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("读取游戏内存失败", "Could not read game memory."));
        return data;
    }

    internal void Write(int rva, byte[] data)
    {
        CheckRange(rva, data.Length);
        if (!WriteProcessMemory(handle, (IntPtr)(moduleBase + rva), data, (nuint)data.Length, out nuint written) || written != (nuint)data.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("写入游戏内存失败", "Could not write game memory."));
    }

    private void CheckRange(int rva, int count)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (rva < 0 || count < 0 || (long)rva + count > moduleSize)
            throw new ArgumentOutOfRangeException(nameof(rva));
    }

    public void VerifySignature(bool patched)
    {
        foreach (var patch in Patches) VerifyPatch(patch, patched);
    }

    private void VerifyPatch(CodePatch patch, bool patched)
    {
        var expected = (byte[])patch.Original.Clone();
        if (patched) expected[patch.Rva - patch.SignatureRva] = patch.Replacement;
        if (!Read(patch.SignatureRva, expected.Length).SequenceEqual(expected))
            throw new InvalidOperationException(L.T("碰撞指令与预期不符，可能存在其他修改器或游戏版本变化。", "Unexpected collision instructions. Another trainer or a different game build may be present."));
    }

    public void SetInvincible(bool enabled)
    {
        if (enabled == OwnsPatch)
        {
            VerifySignature(enabled);
            return;
        }
        if (enabled)
        {
            VerifySignature(false);
            // Bullet: JNE +7 -> JNE +0 takes the existing no-hit return path.
            // Laser: JNE -> JAE after CMP byte,0 always takes the no-hit branch.
            // Each patch changes one byte and retains the original instruction size.
            try
            {
                foreach (var patch in Patches)
                {
                    owned.Add(patch.Rva);
                    WriteCodeByte(patch.Rva, patch.Replacement);
                }
                VerifySignature(true);
            }
            catch
            {
                RestoreInvincible();
                throw;
            }
        }
        else RestoreInvincible();
    }

    private void WriteCodeByte(int rva, byte value)
    {
        var address = (IntPtr)(moduleBase + rva);
        if (!VirtualProtectEx(handle, address, 1, 0x40, out uint oldProtection))
            throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("无法修改碰撞指令的内存权限", "Could not change collision-code memory protection."));
        try
        {
            Write(rva, [value]);
            if (!FlushInstructionCache(handle, address, 1))
                throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("刷新指令缓存失败", "Could not flush the instruction cache."));
        }
        finally
        {
            if (!VirtualProtectEx(handle, address, 1, oldProtection, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("恢复碰撞指令内存权限失败", "Could not restore collision-code memory protection."));
        }
    }

    public void RestoreInvincible()
    {
        if (!OwnsPatch) return;
        if (!IsAlive) { owned.Clear(); return; }
        var errors = new List<Exception>();
        foreach (var patch in Patches.Reverse())
        {
            if (!owned.Contains(patch.Rva)) continue;
            try
            {
                if (!Read(patch.SignatureRva, patch.Original.Length).SequenceEqual(patch.Original))
                {
                    VerifyPatch(patch, true);
                    WriteCodeByte(patch.Rva, patch.Original[patch.Rva - patch.SignatureRva]);
                    VerifyPatch(patch, false);
                }
                owned.Remove(patch.Rva);
            }
            catch (Exception ex) { errors.Add(ex); }
        }
        if (errors.Count > 0) throw new AggregateException(L.T("恢复中弹判定失败", "Could not restore collision detection."), errors);
    }

    public void Dispose()
    {
        if (disposed) return;
        var errors = new List<Exception>();
        try { RemoveModes(); } catch (Exception ex) { errors.Add(ex); }
        try { RestoreInvincible(); } catch (Exception ex) { errors.Add(ex); }
        // A process may exit between any two native calls. There is nothing to restore
        // after its handle is signaled; never turn that race into an unclosable UI.
        if (errors.Count > 0 && IsAlive) throw new AggregateException(L.T("恢复游戏状态失败", "Could not restore the game state."), errors);
        modeBlock = 0; modePatches.Clear(); owned.Clear();
        handle.Dispose();
        disposed = true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(SafeProcessHandle h, IntPtr address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(SafeProcessHandle h, IntPtr address, byte[] buffer, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtectEx(SafeProcessHandle h, IntPtr address, nuint size, uint protection, out uint old);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushInstructionCache(SafeProcessHandle h, IntPtr address, nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle h, uint milliseconds);
}
