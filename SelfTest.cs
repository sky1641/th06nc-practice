using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TH06NCTrainer;

internal static class SelfTest
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int Collision(IntPtr player, IntPtr position, IntPtr size, byte circular);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int LaserCollision(IntPtr unused, IntPtr position, IntPtr size, IntPtr origin, float angle, byte graze);

    public static void Run(string executable, string reportPath)
    {
        var report = new StringBuilder();
        IntPtr image = IntPtr.Zero, player = IntPtr.Zero, position = IntPtr.Zero, size = IntPtr.Zero;
        try
        {
            byte[] file = File.ReadAllBytes(executable);
            Check(Convert.ToHexString(SHA256.HashData(file)) == MemorySession.SupportedHash, "Supported executable SHA-256", report);
            int pe = BitConverter.ToInt32(file, 0x3c);
            int optional = pe + 24;
            int imageSize = BitConverter.ToInt32(file, optional + 56);
            int headersSize = BitConverter.ToInt32(file, optional + 60);
            image = VirtualAlloc(IntPtr.Zero, (nuint)imageSize, 0x3000, 0x04);
            if (image == IntPtr.Zero) throw new InvalidOperationException("VirtualAlloc failed");
            Marshal.Copy(file, 0, image, headersSize);
            int sections = BitConverter.ToUInt16(file, pe + 6);
            int sectionTable = optional + BitConverter.ToUInt16(file, pe + 20);
            for (int index = 0; index < sections; index++)
            {
                int header = sectionTable + index * 40;
                int rva = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int rawOffset = BitConverter.ToInt32(file, header + 20);
                if (rawSize > 0) Marshal.Copy(file, rawOffset, image + rva, rawSize);
                uint flags = BitConverter.ToUInt32(file, header + 36);
                if ((flags & 0x20000000) != 0)
                {
                    int virtualSize = BitConverter.ToInt32(file, header + 8);
                    if (!VirtualProtect(image + rva, (nuint)Math.Max(virtualSize, rawSize), 0x20, out _))
                        throw new InvalidOperationException("VirtualProtect failed");
                }
            }
            // Resolve the two math imports used by the laser geometry routine.
            IntPtr math = NativeLibrary.Load("ucrtbase.dll");
            Marshal.WriteIntPtr(image + 0x2C03C8, NativeLibrary.GetExport(math, "sinf"));
            Marshal.WriteIntPtr(image + 0x2C03D0, NativeLibrary.GetExport(math, "cosf"));
            // Isolate only the particle-effect call; all collision/state logic is original.
            VirtualProtect(image + 0x2B0D0, 1, 0x40, out uint effectProtection);
            Marshal.WriteByte(image + 0x2B0D0, 0xC3);
            VirtualProtect(image + 0x2B0D0, 1, effectProtection, out _);
            ModeSelfTest.Run(image, imageSize, report);
            PracticeSelfTest.Run(image, imageSize, report);
            SpeedSelfTest.Run(image, imageSize, report);
            using (var memory = new MemorySession(image, imageSize))
            {
                Marshal.WriteByte(image + MemorySession.LivesRva - 1, 0xA7);
                Marshal.WriteByte(image + MemorySession.BombsRva + 1, 0xB8);
                memory.WriteResource(Resource.Lives, 8);
                memory.WriteResource(Resource.Bombs, 7);
                Check(memory.ReadResource(Resource.Lives) == 8 && memory.ReadResource(Resource.Bombs) == 7, "Independent fixed life and Bomb bytes", report);
                Check(Marshal.ReadByte(image + MemorySession.LivesRva - 1) == 0xA7 && Marshal.ReadByte(image + MemorySession.BombsRva + 1) == 0xB8, "Resource writes preserve neighboring bytes", report);
                Marshal.WriteByte(image + MemorySession.PowerRva - 1, 0xC9);
                Marshal.WriteByte(image + MemorySession.PowerRva + 2, 0xDA);
                foreach (int power in new[] { 0, 8, 32, 64, 127, 128 })
                {
                    memory.WriteResource(Resource.Power, power);
                    Check(memory.ReadResource(Resource.Power) == power, $"POWER UInt16 round-trip {power}", report);
                }
                Check(Marshal.ReadByte(image + MemorySession.PowerRva - 1) == 0xC9 && Marshal.ReadByte(image + MemorySession.PowerRva + 2) == 0xDA, "POWER writes exactly two bytes", report);
                foreach (var item in new[] { (Resource.Lives, -1), (Resource.Lives, 9), (Resource.Bombs, 9), (Resource.Power, 129) })
                {
                    bool rejected = false;
                    try { memory.WriteResource(item.Item1, item.Item2); }
                    catch (ArgumentOutOfRangeException) { rejected = true; }
                    Check(rejected, $"Out-of-range {item.Item1} {item.Item2} rejected", report);
                }
                player = Marshal.AllocHGlobal(0xA400);
                Marshal.Copy(new byte[0xA400], 0, player, 0xA400);
                position = Marshal.AllocHGlobal(8);
                size = Marshal.AllocHGlobal(8);
                PutFloat(player + 0x774C, 3);
                PutFloat(position, 0); PutFloat(position + 4, 0);
                PutFloat(size, 20); PutFloat(size + 4, 20);
                var collision = Marshal.GetDelegateForFunctionPointer<Collision>(image + MemorySession.CollisionRva);
                var laser = Marshal.GetDelegateForFunctionPointer<LaserCollision>(image + MemorySession.LaserCollisionRva);
                PutFloat(image + 0x506AD0, 0);
                PutFloat(image + 0x506AD4, 0);
                PutFloat(image + 0x506AEC, 3);
                Marshal.WriteByte(image + 0x506C38, 0);
                Check(laser(IntPtr.Zero, position, size, position, 0, 0) == 1
                    && Marshal.ReadByte(image + 0x506C38) == 2
                    && Marshal.ReadInt32(image + 0x506ADC) == 8,
                    "Original laser triggers hit state and 8-frame deathbomb window (particle effects stubbed)", report);
                foreach (byte mode in new byte[] { 0, 1 })
                {
                    Marshal.WriteByte(player + 0x7898, 0);
                    Marshal.WriteInt32(player + 0x773C, 0);
                    Check(collision(player, position, size, mode) == 1
                        && Marshal.ReadByte(player + 0x7898) == 2
                        && Marshal.ReadInt32(player + 0x773C) == 8,
                        $"Original collision mode {mode} triggers hit and deathbomb state (particle effects stubbed)", report);
                }
                memory.SetInvincible(true);
                Check(memory.OwnsPatch, "Invincibility patch ownership recorded", report);
                foreach (float angle in new[] { 0f, MathF.PI / 4, MathF.PI / 2, MathF.PI })
                {
                    Marshal.WriteByte(image + 0x506C38, 0);
                    Marshal.WriteInt32(image + 0x506ADC, 0);
                    Check(laser(IntPtr.Zero, position, size, position, angle, 0) == 0
                        && Marshal.ReadByte(image + 0x506C38) == 0
                        && Marshal.ReadInt32(image + 0x506ADC) == 0,
                        $"Patched laser at {angle:F3} radians causes no hit or deathbomb state", report);
                }
                Marshal.WriteByte(player + 0x7898, 0);
                Marshal.WriteInt32(player + 0x773C, 0);
                for (byte circular = 0; circular <= 1; circular++)
                {
                    Check(collision(player, position, size, circular) == 0, $"Patched collision mode {circular} returns no hit", report);
                    Check(Marshal.ReadByte(player + 0x7898) == 0 && Marshal.ReadInt32(player + 0x773C) == 0,
                        $"Patched mode {circular} preserves player state and deathbomb timer", report);
                }
                PutFloat(position, 1000); PutFloat(position + 4, 1000);
                Check(collision(player, position, size, 0) == 0 && collision(player, position, size, 1) == 0, "Distant bullets remain no hit", report);
                for (int i = 0; i < 20; i++) { memory.SetInvincible(false); memory.SetInvincible(true); }
                memory.SetInvincible(false);
                Check(memory.Read(MemorySession.SignatureRva, MemorySession.OriginalSignature.Length).SequenceEqual(MemorySession.OriginalSignature),
                    "20 toggle cycles restore original code exactly", report);
                PutFloat(position, 0); PutFloat(position + 4, 0);
                Marshal.WriteByte(player + 0x7898, 3);
                Check(collision(player, position, size, 0) == 1 && collision(player, position, size, 1) == 1, "Restored code returns hits again", report);
                Marshal.WriteByte(image + 0x506C38, 0);
                Check(laser(IntPtr.Zero, position, size, position, 0, 0) == 1 && Marshal.ReadByte(image + 0x506C38) == 2,
                    "Restored laser code triggers hit again", report);
                memory.SetInvincible(true);
            }
            Check(Marshal.ReadByte(image + MemorySession.PatchRva) == 7, "Dispose restores invincibility patch", report);
            Check(Marshal.ReadByte(image + MemorySession.LaserPatchRva) == 0x85, "Dispose restores laser patch", report);
            using (var memory = new MemorySession(image, imageSize))
            {
                // A conflict in the second patch must stop enabling before either write.
                VirtualProtect(image + MemorySession.LaserPatchRva, 1, 0x40, out uint laserOld);
                Marshal.WriteByte(image + MemorySession.LaserPatchRva, 0x84);
                VirtualProtect(image + MemorySession.LaserPatchRva, 1, laserOld, out _);
                bool laserRejected = false;
                try { memory.SetInvincible(true); }
                catch (InvalidOperationException) { laserRejected = true; }
                Check(laserRejected && Marshal.ReadByte(image + MemorySession.PatchRva) == 7
                    && Marshal.ReadByte(image + MemorySession.LaserPatchRva) == 0x84 && !memory.OwnsPatch,
                    "Laser conflict prevents both patch writes", report);
                VirtualProtect(image + MemorySession.LaserPatchRva, 1, 0x40, out laserOld);
                Marshal.WriteByte(image + MemorySession.LaserPatchRva, 0x85);
                VirtualProtect(image + MemorySession.LaserPatchRva, 1, laserOld, out _);
                VirtualProtect(image + MemorySession.PatchRva, 1, 0x40, out uint old);
                Marshal.WriteByte(image + MemorySession.PatchRva, 6);
                VirtualProtect(image + MemorySession.PatchRva, 1, old, out _);
                bool rejected = false;
                try { memory.SetInvincible(true); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && Marshal.ReadByte(image + MemorySession.PatchRva) == 6, "Foreign patch is rejected and preserved", report);
            }
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable))) == MemorySession.SupportedHash, "Game file unchanged after tests", report);
            RemoteModeTest.Run(executable, report);
            report.AppendLine("PASS: all checks. Tests used private memory images, including a separate actively updating test process.");
            report.AppendLine("Not tested: live gameplay and replay behavior. Laser particle effects were stubbed in the private test image only.");
        }
        catch (Exception ex)
        {
            report.AppendLine("FAIL: " + ex);
            Environment.ExitCode = 1;
        }
        finally
        {
            if (player != IntPtr.Zero) Marshal.FreeHGlobal(player);
            if (position != IntPtr.Zero) Marshal.FreeHGlobal(position);
            if (size != IntPtr.Zero) Marshal.FreeHGlobal(size);
            if (image != IntPtr.Zero) VirtualFree(image, 0, 0x8000);
            File.WriteAllText(reportPath, report.ToString());
        }
    }

    private static void PutFloat(IntPtr address, float value) => Marshal.WriteInt32(address, BitConverter.SingleToInt32Bits(value));
    private static void Check(bool ok, string title, StringBuilder report)
    {
        if (!ok) throw new InvalidOperationException(title);
        report.AppendLine("PASS: " + title);
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint allocationType, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, nuint size, uint protection, out uint oldProtection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr address, nuint size, uint freeType);
}
