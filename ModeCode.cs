namespace TH06NCTrainer;

// Exact-build x64 hooks. Only the game's own update thread changes the time-stop state.
// No remote threads, no game-file patching, and no changes to the native Sakuya flag.
internal static class ModeCode
{
    internal const int NativeTime = 0x4F27B9, Input = 0xA6EC60, Scene = 0xC21D9C, Gui = 0xA6EC08;
    internal const int ControlOffset = 0x9000, AllocationSize = 0xA000;
    internal const int Peace = 0, Enabled = 1, Active = 2, Latch = 3, Lease = 4, Attack = 8;
    internal const int SpeedPercent = 12, SpeedLease = 16, SpeedBase = 24, SpeedApplied = 32, SpeedCaptured = 40, SpeedEffective = 44;
    internal const int FramePeriod = 0xC22100, FrameInitialized = 0xC22120;
    internal static readonly int[] EnemyShotSounds = [7, 8, 9, 15, 16, 17, 22, 23, 24];
    internal sealed record Site(int Rva, string Hex, string Kind)
    {
        internal byte[] Original => Convert.FromHexString(Hex);
    }
    internal static readonly Site[] Sites =
    [
        new(0x6882D, "803D859F480000", "player"),
        new(0x1087F, "803D331F4E0000", "bullets"),
        new(0x373B0, "488BC448894808", "enemy"),
        new(0xF870, "4056574881ECF8000000", "spawn"),
        new(0x10850, "888B90020000", "laser"),
        new(0x6A980, "40534883EC70", "contact"),
        new(0x6ABA0, "488BC448895808", "contact"),
        new(0x689DE, "0FB60D0C674900", "bomb"),
        new(0x69774, "F3440F100DDB352A00", "shots"),
        new(0x69B98, "8BAF0C04000085ED", "fire"),
        new(0x3CC56, "803D5C5B4B0000", "time"),
        new(0x77BC7, "803DEBAB470000", "time"),
        new(0x76310, "803DA2C4470000", "time"),
        new(0x3740C, "E84FEEFFFF", "timeline"),
        new(0x374B1, "488DB3B0000000", "enemyMotion"),
        new(0x38095, "83BB9010000000", "enemyEffects"),
        new(0x381F1, "41898C24B8C01000FFC141898C24BCC01000", "timelineTick"),
        new(0x37B86, "83BB3402000000", "bossPhase"),
        new(0x76A34, "0FB60D7CB3BA00", "sound"),
        new(0x3C465, "44383DB45CBE00", "speed")
    ];

    internal static byte[] Build(Site site, long origin, long image, long control)
    {
        var a = new Assembler(origin);
        long Resume = image + site.Rva + site.Original.Length;
        void Cmp(int field, byte value = 0) => a.Rip("803D", control + field, [value]);
        void Set(int field, byte value) => a.Rip("C605", control + field, [value]);
        void Original() { a.Emit(site.Original); a.Jmp(Resume); }
        void EffectiveTime()
        {
            Cmp(Active);
            a.Branch(0x85, "done");
            a.Rip("803D", image + NativeTime, [0]);
            a.Label("done"); a.Jmp(Resume);
        }
        switch (site.Kind)
        {
            case "speed":
                // On the render/update thread, before the native frame deadline is computed.
                // Scale the complete frame interval, never per-object velocities or global QPC.
                a.Emit("505152"); // Preserve RAX=current time, RDX=current time, and RCX.
                a.Rip("833D", control + SpeedLease, [0]); a.Branch(0x8E, "speedExpired");
                a.Rip("FF0D", control + SpeedLease);
                a.Rip("8B0D", control + SpeedPercent); a.Jmp("speedScene");
                a.Label("speedExpired");
                a.Rip("C705", control + SpeedPercent, BitConverter.GetBytes(100));
                a.Emit("B964000000");
                a.Label("speedScene");
                a.Rip("833D", image + Scene, [2]); a.Branch(0x84, "speedRange");
                a.Emit("B964000000"); // Menus/loading run at original speed; choice stays armed.
                a.Label("speedRange");
                a.Emit("83F919"); a.Branch(0x8C, "speedDefault");
                a.Emit("81F9C8000000"); a.Branch(0x8E, "speedRead");
                a.Label("speedDefault"); a.Emit("B964000000");
                a.Label("speedRead"); a.Rip("488B05", image + FramePeriod);
                a.Emit("4885C0"); a.Branch(0x8E, "speedDone");
                Cmp(SpeedCaptured); a.Branch(0x84, "speedCapture");
                a.Rip("483B05", control + SpeedApplied); a.Branch(0x84, "speedCompute");
                // Native renderer reinitialization may supply a new unmodified interval.
                a.Label("speedCapture"); a.Rip("488905", control + SpeedBase); Set(SpeedCaptured, 1);
                a.Label("speedCompute"); a.Rip("488B05", control + SpeedBase);
                a.Emit("486BC06433D248F7F1"); // RAX = original interval * 100 / percent (RCX).
                a.Emit("4885C0"); a.Branch(0x8F, "speedStore"); a.Emit("B801000000");
                a.Label("speedStore"); a.Rip("890D", control + SpeedEffective);
                a.Rip("488905", control + SpeedApplied);
                a.Rip("483B05", image + FramePeriod); a.Branch(0x84, "speedDone");
                a.Rip("488905", image + FramePeriod);
                a.Rip("C605", image + FrameInitialized, [0]); // Rebase: no long freeze/catch-up on a rate change.
                a.Label("speedDone"); a.Emit("5A5958");
                a.Rip("44383D", image + FrameInitialized); // Original CMP byte,[RIP],R15b.
                a.Jmp(Resume);
                break;
            case "player":
                a.Emit("5052"); // Preserve incoming RAX (original stack pointer) and RDX.
                a.Rip("833D", control + Lease, [0]);
                a.Branch(0x8E, "expired");
                a.Rip("FF0D", control + Lease);
                a.Rip("833D", image + Scene, [2]);
                a.Branch(0x85, "reset");
                Cmp(Enabled); a.Branch(0x84, "reset");
                a.Emit("80B99878000001"); a.Branch(0x84, "reset"); // Spawning.
                a.Emit("80B99878000002"); a.Branch(0x84, "reset"); // Hit/deathbomb.
                a.Emit("80B9C89E000000"); a.Branch(0x85, "reset"); // Existing bomb.
                a.Rip("488B15", image + Gui);
                a.Emit("4885D2"); a.Branch(0x84, "reset");
                a.Emit("83BAB036000000"); a.Branch(0x8D, "reset"); // Dialogue active.
                a.Rip("8B05", image + Input); a.Emit("A802");
                a.Branch(0x84, "release");
                Cmp(Latch); a.Branch(0x85, "finish");
                a.Rip("8035", control + Active, [1]);
                Set(Latch, 1); a.Jmp("finish");
                a.Label("release"); Set(Latch, 0); a.Jmp("finish");
                a.Label("expired"); Set(Peace, 0); Set(Enabled, 0);
                a.Label("reset"); Set(Active, 0);
                a.Rip("8B05", image + Input); a.Emit("D1E883E001");
                a.Rip("8805", control + Latch);
                a.Label("finish"); a.Emit("5A58");
                Cmp(Active, 1); a.Branch(0x84, "done"); // ZF=1 lets player run during OUR stop.
                a.Rip("803D", image + NativeTime, [0]);
                a.Label("done"); a.Jmp(Resume);
                break;
            case "bullets":
                // RCX is BulletManager. Clearing state retains all object storage/pointers.
                Cmp(Peace); a.Branch(0x84, "clock");
                a.Emit("5052");
                a.Emit("488D514CB880020000");
                a.Label("clearBullets"); a.Emit("66C70200004881C220060000FFC8");
                a.Branch(0x85, "clearBullets");
                a.Emit("488D918C520F00B840000000");
                a.Label("clearLasers"); a.Emit("C602004881C298020000FFC8");
                a.Branch(0x85, "clearLasers");
                a.Emit("5A58");
                a.Label("clock"); EffectiveTime();
                break;
            case "time": EffectiveTime(); break;
            case "enemy":
                Cmp(Active); a.Branch(0x84, "original");
                Cmp(Attack); a.Branch(0x85, "original");
                a.Emit("B801000000C3");
                a.Label("original"); Original();
                break;
            case "spawn":
                Cmp(Peace); a.Branch(0x84, "original");
                a.Emit("33C0C3");
                a.Label("original"); Original();
                break;
            case "laser":
                a.Emit("9C"); Cmp(Peace); a.Branch(0x84, "original");
                a.Emit("C6837402000000");
                a.Label("original"); a.Emit("9D"); Original();
                break;
            case "contact":
                Cmp(Peace); a.Branch(0x85, "nohit");
                Cmp(Active); a.Branch(0x84, "original");
                a.Label("nohit"); a.Emit("33C0C3");
                a.Label("original"); Original();
                break;
            case "bomb":
                Cmp(Enabled); a.Branch(0x84, "original");
                a.Emit("33C9"); a.Jmp(Resume);
                a.Label("original"); a.Rip("0FB60D", image + MemorySession.BombsRva); a.Jmp(Resume);
                break;
            case "shots":
                Cmp(Active); a.Branch(0x84, "original");
                Cmp(Attack); a.Branch(0x85, "original");
                a.Jmp(image + 0x69B09); // All saved XMM registers are restored here.
                a.Label("original"); a.Rip("F3440F100D", image + 0x30CD58); a.Jmp(Resume);
                break;
            case "fire":
                Cmp(Active); a.Branch(0x84, "original");
                Cmp(Attack); a.Branch(0x85, "original");
                a.Jmp(image + 0x69C9F); // Shared GPR epilogue, after XMM restoration.
                a.Label("original"); Original();
                break;
            case "timeline":
                Cmp(Active); a.Branch(0x85, "skip");
                // Emulate the original CALL without leaving a return address in the code cave.
                // This keeps uninstall safe even when a thread is inside the native callee.
                a.Emit("5048B8"); a.Emit(BitConverter.GetBytes(Resume));
                a.Emit("48870424"); a.Jmp(image + 0x36260);
                a.Label("skip"); a.Jmp(Resume);
                break;
            case "enemyMotion":
                a.Emit(site.Original); // RSI = enemy position, also required by damage calculations.
                Cmp(Active); a.Branch(0x84, "normal");
                a.Jmp(image + 0x379E0); // Skip movement, ECL, animation; preserve the native damage path.
                a.Label("normal"); a.Jmp(Resume);
                break;
            case "enemyEffects":
                Cmp(Active); a.Branch(0x84, "original");
                a.Emit("BFFFFFFFFF"); // Normally restored at 38163 before the next enemy.
                a.Jmp(image + 0x38174);
                a.Label("original"); Original();
                break;
            case "timelineTick":
                Cmp(Active); a.Branch(0x84, "original"); a.Jmp(Resume);
                a.Label("original"); Original();
                break;
            case "bossPhase":
                Cmp(Active); a.Branch(0x84, "original");
                a.Emit("F683BD00000008"); a.Branch(0x84, "original");
                a.Emit("50");
                a.Emit("8B833402000085C0"); a.Branch(0x8E, "resumeBoss");
                a.Emit("83BBA000000000"); a.Branch(0x88, "keepBoss");
                a.Emit("3B83A0000000"); a.Branch(0x8D, "keepBoss");
                a.Label("resumeBoss"); Set(Active, 0);
                a.Label("keepBoss"); a.Emit("58");
                a.Label("original"); Original();
                break;
            case "sound":
                Cmp(Peace); a.Branch(0x84, "original");
                a.Emit("50415041514152"); // RAX,R8,R9,R10; ECX is overwritten by the original instruction.
                a.Rip("4C8D15", image + 0x509670);
                a.Emit("4533C04533C9");
                a.Label("readSound"); a.Emit("438B048285C0"); a.Branch(0x88, "nextSound");
                foreach (int sound in EnemyShotSounds)
                {
                    a.Emit([0x83, 0xF8, (byte)sound]); a.Branch(0x84, "nextSound");
                }
                a.Emit("4389048A41FFC1");
                a.Label("nextSound"); a.Emit("41FFC04183F803"); a.Branch(0x8C, "readSound");
                a.Label("fillSound"); a.Emit("4183F903"); a.Branch(0x8D, "soundDone");
                a.Emit("43C7048AFFFFFFFF41FFC1"); a.Jmp("fillSound");
                a.Label("soundDone"); a.Emit("415A4159415858");
                // Keep the remaining sounds contiguous: the original player stops at the first empty slot.
                a.Label("original"); a.Rip("0FB60D", image + 0xC21DB7); a.Jmp(Resume);
                break;
            default: throw new InvalidOperationException(site.Kind);
        }
        return a.Finish();
    }

    internal sealed class Assembler(long origin)
    {
        private readonly List<byte> data = [];
        private readonly Dictionary<string, int> labels = [];
        private readonly List<(int Position, string Target)> fixups = [];
        internal void Emit(string hex) => Emit(Convert.FromHexString(hex));
        internal void Emit(byte[] bytes) => data.AddRange(bytes);
        internal void Label(string label) => labels.Add(label, data.Count);
        internal void Rip(string hex, long target, byte[]? suffix = null)
        {
            Emit(hex);
            Emit(BitConverter.GetBytes(checked((int)(target - (origin + data.Count + 4 + (suffix?.Length ?? 0))))));
            if (suffix is not null) Emit(suffix);
        }
        internal void Jmp(long target) => Rip("E9", target);
        internal void Jmp(string label) { Emit("E9"); Fix(label); }
        internal void Branch(byte condition, string label) { Emit([0x0F, condition]); Fix(label); }
        private void Fix(string target) { fixups.Add((data.Count, target)); Emit(new byte[4]); }
        internal byte[] Finish()
        {
            var bytes = data.ToArray();
            foreach (var (pos, target) in fixups) BitConverter.GetBytes(labels[target] - pos - 4).CopyTo(bytes, pos);
            return bytes;
        }
    }
}
