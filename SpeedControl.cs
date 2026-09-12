namespace TH06NCTrainer;

internal sealed partial class MemorySession
{
    public int DesiredSpeed => !ModesInstalled ? 100 : NormalizeSpeed(BitConverter.ToInt32(ReadRemote(Control + ModeCode.SpeedPercent, 4)));
    public int EffectiveSpeed => !ModesInstalled ? 100 : NormalizeSpeed(BitConverter.ToInt32(ReadRemote(Control + ModeCode.SpeedEffective, 4)));
    private static int NormalizeSpeed(int percent) => percent is >= 25 and <= 200 ? percent : 100;

    public void SetSpeed(int percent)
    {
        if (percent is < 25 or > 200) throw new ArgumentOutOfRangeException(nameof(percent));
        if (percent == 100) { RestoreSpeed(); return; }
        RequireGame(); InstallModes(); HeartbeatModes();
        WriteRemote(Control + ModeCode.SpeedPercent, BitConverter.GetBytes(percent));
    }

    public void RestoreSpeed()
    {
        if (!ModesInstalled || !IsAlive) return;
        using var guard = QuiesceModes();
        WriteRemote(Control + ModeCode.SpeedPercent, BitConverter.GetBytes(100));
        WriteRemote(Control + ModeCode.SpeedEffective, BitConverter.GetBytes(100));
        WriteRemote(Control + ModeCode.SpeedLease, BitConverter.GetBytes(0));
        if (ReadRemote(Control + ModeCode.SpeedCaptured, 1)[0] == 0) return;
        long original = BitConverter.ToInt64(ReadRemote(Control + ModeCode.SpeedBase, 8));
        long applied = BitConverter.ToInt64(ReadRemote(Control + ModeCode.SpeedApplied, 8));
        long current = BitConverter.ToInt64(Read(ModeCode.FramePeriod, 8));
        // Do not overwrite another writer's value. Native reinit also resets its own schedule.
        if (original > 0 && current == applied && current != original)
        {
            Write(ModeCode.FramePeriod, BitConverter.GetBytes(original));
            Write(ModeCode.FrameInitialized, [0]);
        }
        WriteRemote(Control + ModeCode.SpeedCaptured, [0]);
    }
}
