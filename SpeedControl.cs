namespace TH06NCTrainer;

internal sealed partial class MemorySession
{
    public bool IsOverdrive => ModesInstalled && IsAlive && ReadRemote(Control + ModeCode.Overdrive, 1)[0] != 0;
    public int DesiredSpeed => IsOverdrive ? ModeCode.OverdrivePercent : !ModesInstalled || !IsAlive ? 100 : NormalizeSpeed(BitConverter.ToInt32(ReadRemote(Control + ModeCode.SpeedPercent, 4)));
    public int EffectiveSpeed => !ModesInstalled || !IsAlive ? 100 : NormalizeSpeed(BitConverter.ToInt32(ReadRemote(Control + ModeCode.SpeedEffective, 4)));
    private static int NormalizeSpeed(int percent) => percent is >= 25 and <= 200 or ModeCode.OverdrivePercent ? percent : 100;

    public void SetOverdrive(bool enabled)
    {
        if (enabled) { RequireGame(); InstallModes(); }
        if (!ModesInstalled) return;
        WriteRemote(Control + ModeCode.Overdrive, [enabled ? (byte)1 : (byte)0]);
        HeartbeatModes();
    }

    public void SetOpacity(int player, int enemy)
    {
        if (player is < 0 or > 100 || enemy is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(player));
        if (player != 100 || enemy != 100) { RequireGame(); InstallModes(); }
        if (!ModesInstalled || !IsAlive) return;
        HeartbeatModes();
        WriteRemote(Control + ModeCode.PlayerOpacity, BitConverter.GetBytes(player));
        WriteRemote(Control + ModeCode.EnemyOpacity, BitConverter.GetBytes(enemy));
    }

    public void SetSpeed(int percent)
    {
        if (percent is < 25 or > 200) throw new ArgumentOutOfRangeException(nameof(percent));
        if (percent == 100) { RestoreSpeed(); return; }
        RequireGame(); InstallModes(); HeartbeatModes();
        WriteRemote(Control + ModeCode.Overdrive, [0]);
        WriteRemote(Control + ModeCode.SpeedPercent, BitConverter.GetBytes(percent));
    }

    public void RestoreSpeed()
    {
        if (!ModesInstalled || !IsAlive) return;
        using var guard = QuiesceModes();
        WriteRemote(Control + ModeCode.Overdrive, [0]);
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
