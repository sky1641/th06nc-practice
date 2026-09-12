using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TH06NCTrainer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        L.Initialize();
        ApplicationConfiguration.Initialize();
        if (args.Length > 0 && args[0] == "--write-icon") { PracticeIcon.Write(args[1]); return; }
        if (args.Length > 0 && args[0] == "--ui-self-test")
        {
            UiPaintSelfTest.Run(args[1]); return;
        }
        if (args.Length > 0 && args[0] == "--native-host")
        {
            RemoteModeTest.Host(args[1]);
            return;
        }
        if (args.Length > 0 && args[0] == "--self-test")
        {
            SelfTest.Run(args[1], args[2]);
            return;
        }
        if (args.Length > 0 && args[0] is "--preview" or "--preview-active" or "--preview-wide")
        {
            using var form = new TrainerForm(false);
            if (args[0] == "--preview-active") form.PreparePreview();
            if (args[0] == "--preview-wide") form.Width = 1000;
            form.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(args[1]);
            return;
        }
        using var mutex = new Mutex(true, @"Local\TH06NCTrainerV2", out bool created);
        if (!created)
        {
            MessageBox.Show(L.T("练习辅助已在运行，请切换到已有窗口。", "The helper is already running. Switch to its existing window."), "TH06 New Classic");
            return;
        }
        Application.Run(new TrainerForm());
    }
}

internal sealed class Preferences
{
    public int Lives { get; set; } = 8;
    public int Bombs { get; set; } = 8;
    public int Power { get; set; } = 128;
}

internal sealed class TrainerForm : MoonForm
{
    private readonly Label connection = new() { AutoSize = false };
    private readonly Label feedback = new() { AutoSize = false };
    private readonly Label godStatus = new() { AutoSize = true, Text = L.T("已关闭", "Off"), ForeColor = Theme.Muted };
    private readonly CheckBox god = new ThemedCheckBox() { AutoSize = true, Text = L.T("无敌  [F8]", "Invincibility  [F8]") };
    private readonly CheckBox peace = new ThemedCheckBox() { AutoSize = true, Text = L.T("和平观光  [F9]", "Peaceful sightseeing  [F9]"), Enabled = false };
    private readonly CheckBox sakuya = new ThemedCheckBox() { AutoSize = true, Text = L.T("不可攻击  [F11]", "No attacks  [F11]"), Enabled = false };
    private readonly CheckBox sakuyaAttack = new ThemedCheckBox() { AutoSize = true, Text = L.T("可攻击  [F10]", "Allow attacks  [F10]"), Enabled = false };
    private readonly Label peaceStatus = new() { AutoSize = true, Text = L.T("已关闭", "Off"), ForeColor = Theme.Muted };
    private readonly Label timeStatus = new() { AutoSize = true, Text = L.T("已关闭", "Off"), ForeColor = Theme.Muted };
    private readonly Button resume = new ThemedButton() { Text = L.T("立即恢复时间", "Resume time"), Enabled = false };
    private readonly NumericUpDown speedTarget = new() { Minimum = 25, Maximum = 200, Increment = 5, Value = 100, Width = 70 };
    private readonly Label speedStatus = new() { Text = L.T("正常速度 1.00×", "Normal speed 1.00×"), AutoSize = true, ForeColor = Theme.Muted };
    private readonly List<Button> speedButtons = [];
    private readonly CheckBox overdrive = new ThemedCheckBox() { Text = L.T("Overdrive · 娱乐", "Overdrive · Fun"), AutoSize = true, Enabled = false };
    private readonly NumericUpDown playerOpacity = new() { Minimum = 0, Maximum = 100, Value = 100, Width = 65 };
    private readonly NumericUpDown enemyOpacity = new() { Minimum = 0, Maximum = 100, Value = 100, Width = 65 };
    private readonly Button applyOpacity = new ThemedButton() { Text = L.T("应用", "Apply"), Enabled = false };
    private readonly Button resetOpacity = new ThemedButton() { Text = L.T("恢复", "Reset"), Enabled = false };
    private readonly List<ResourceRow> rows = [];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 50 };
    private MemorySession? session;
    private long nextAttach;
    private bool updating;
    private readonly bool live;
    private readonly string prefsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private readonly List<int> registeredKeys = [];

    public TrainerForm(bool live = true)
    {
        this.live = live;
        Text = L.T("东方红魔乡：新典 · 练习辅助 v1.0.1", "TH06 New Classic · Practice Helper v1.0.1");
        Font = new Font(L.English ? "Segoe UI" : "Microsoft YaHei UI", 10F);
        BackColor = Color.FromArgb(247, 249, 252);
        ClientSize = new Size(700, Math.Min(880, Math.Max(540, Screen.PrimaryScreen!.WorkingArea.Height - 100)));
        MinimumSize = new Size(716, 560);
        AutoScroll = true;
        AutoScrollMinSize = new Size(700, 880);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var title = new Label { Text = L.T("新典练习辅助", "NEW CLASSIC"), AutoSize = true, Font = new Font("Segoe UI", 23F, FontStyle.Bold), Location = new Point(74, 15) };
        var subtitle = new Label { Text = L.T("练习与观光辅助", "PRACTICE && SIGHTSEEING"), AutoSize = true, ForeColor = Theme.Muted, Location = new Point(26, 62) };
        var reconnect = new ThemedButton { Text = L.T("重新连接", "Reconnect"), Bounds = new Rectangle(554, 25, 120, 35), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        reconnect.Click += (_, _) => Reconnect();
        connection.Bounds = new Rectangle(26, 92, 648, 26);
        connection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        connection.Text = L.T("等待游戏启动…", "Waiting for the game…");
        var table = new TableLayoutPanel { Bounds = new Rectangle(24, 124, 652, 145), ColumnCount = 4, RowCount = 4, BackColor = Color.Transparent, Padding = new Padding(12), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        foreach (string heading in new[] { L.T("功能 / 快捷键", "Feature / Hotkey"), L.T("当前值", "Current"), L.T("锁定目标", "Target"), L.T("操作", "Action") })
            table.Controls.Add(new Label { Text = heading, AutoSize = true, ForeColor = Theme.Muted });
        var prefs = LoadPreferences();
        AddRow(Resource.Lives, L.T("锁残机  [F5]", "Lock lives  [F5]"), prefs.Lives, table);
        AddRow(Resource.Bombs, L.T("锁 Bomb  [F6]", "Lock Bomb  [F6]"), prefs.Bombs, table);
        AddRow(Resource.Power, L.T("锁 POWER  [F7]", "Lock POWER  [F7]"), prefs.Power, table);
        var speedBox = new GlassPanel { Bounds = new Rectangle(24, 281, 652, 121), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        speedTarget.Location = new Point(92, 9);
        speedStatus.Location = new Point(258, 13);
        var applySpeed = new ThemedButton { Text = L.T("应用", "Apply"), Bounds = new Rectangle(183, 8, 63, 29), Enabled = false };
        applySpeed.Click += (_, _) => RunAction(() => ApplySpeed((int)speedTarget.Value));
        speedButtons.Add(applySpeed);
        speedBox.Controls.AddRange([new Label { Text = L.T("游戏速度", "Speed"), AutoSize = true, Location = new Point(12, 13) }, speedTarget, new Label { Text = "%", AutoSize = true, Location = new Point(162, 13) }, applySpeed, speedStatus]);
        int speedX = 12;
        foreach (int percent in new[] { 50, 75, 100, 150, 200 })
        {
            var preset = new ThemedButton { Text = percent == 100 ? L.T("恢复 1×", "Reset 1×") : $"{percent / 100.0:0.##}×", Bounds = new Rectangle(speedX, 47, 82, 28), Enabled = false };
            preset.Click += (_, _) => RunAction(() => { speedTarget.Value = percent; ApplySpeed(percent); });
            speedButtons.Add(preset); speedBox.Controls.Add(preset); speedX += 88;
        }
        overdrive.Location = new Point(456, 51);
        overdrive.CheckedChanged += (_, _) =>
        {
            if (updating) return;
            RunAction(() =>
            {
                session?.SetOverdrive(overdrive.Checked); RefreshSpeed();
                feedback.Text = overdrive.Checked ? L.T("Overdrive：目标 16×（约 960 FPS），实际速度取决于性能与显示设置。", "Overdrive targets 16× (~960 FPS); actual speed depends on performance and display settings.") : L.T("已恢复所选常规倍率。", "Selected regular speed restored.");
            });
        };
        speedBox.Controls.Add(overdrive);
        speedBox.Controls.Add(new Label { Text = L.T("加速前关闭 V-Sync（垂直同步）· 音乐原速", "Disable V-Sync before speeding up. Music stays at its original rate."), Bounds = new Rectangle(12, 87, 430, 27), ForeColor = Theme.Muted, Font = new Font(Font.FontFamily, 9F) });
        speedBox.Controls.Add(new Label { Text = L.T("还原千帧乡（笑）", "Runaway-FPS nostalgia :)"), Bounds = new Rectangle(456, 87, 190, 27), ForeColor = Theme.Muted, Font = new Font(Font.FontFamily, 9F) });
        var opacityBox = new GlassPanel { Bounds = new Rectangle(24, 414, 652, 84), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        playerOpacity.Location = new Point(159, 11); enemyOpacity.Location = new Point(340, 11);
        applyOpacity.Bounds = new Rectangle(436, 9, 78, 30); resetOpacity.Bounds = new Rectangle(526, 9, 78, 30);
        applyOpacity.Click += (_, _) => RunAction(() => ApplyOpacity());
        resetOpacity.Click += (_, _) => RunAction(() => { playerOpacity.Value = enemyOpacity.Value = 100; ApplyOpacity(); });
        opacityBox.Controls.AddRange([new Label { Text = L.T("弹幕透明度", "Opacity"), AutoSize = true, Location = new Point(12, 15) }, new Label { Text = L.T("自机", "Player"), AutoSize = true, Location = new Point(105, 15) }, playerOpacity, new Label { Text = "%", AutoSize = true, Location = new Point(226, 15) }, new Label { Text = L.T("敌机", "Enemy"), AutoSize = true, Location = new Point(280, 15) }, enemyOpacity, new Label { Text = "%", AutoSize = true, Location = new Point(407, 15) }, applyOpacity, resetOpacity, new Label { Text = L.T("0% 隐藏 · 100% 原始显示；子弹与激光的碰撞判定不变。", "0% hidden · 100% original. Bullet and laser collisions are unchanged."), Bounds = new Rectangle(12, 49, 628, 27), ForeColor = Theme.Muted, Font = new Font(Font.FontFamily, 9F) }]);
        var godBox = new GlassPanel { Bounds = new Rectangle(24, 510, 652, 60), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        god.Location = new Point(12, 12);
        god.Enabled = false;
        godStatus.Location = new Point(250, 12);
        godBox.Controls.AddRange([god, godStatus, new Label { Text = L.T("免疫普通弹、敌机接触与激光伤害。", "Protects against ordinary bullets, enemy contact and lasers."), AutoSize = true, Location = new Point(12, 35), ForeColor = Theme.Muted, Font = new Font(Font.FontFamily, 9F) }]);
        god.CheckedChanged += (_, _) => ToggleGod();
        var peaceBox = new GlassPanel { Bounds = new Rectangle(24, 582, 652, 60), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        peace.Location = new Point(12, 10);
        peaceStatus.Location = new Point(250, 10);
        peaceBox.Controls.AddRange([peace, peaceStatus, new Label { Text = L.T("清除并禁止敌弹与激光，静音发弹音效，免接触伤害。", "Suppresses enemy shots, lasers and firing sounds; prevents contact damage."), AutoSize = true, Location = new Point(12, 35), ForeColor = Theme.Muted, Font = new Font(Font.FontFamily, 9F) }]);
        var timeBox = new GlassPanel { Bounds = new Rectangle(24, 654, 652, 140), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        sakuyaAttack.Location = new Point(12, 44);
        sakuya.Location = new Point(216, 44);
        timeStatus.Location = new Point(132, 12);
        resume.Bounds = new Rectangle(504, 9, 133, 30);
        resume.Click += (_, _) => RunAction(() => { session?.ResumeTime(); RefreshModes(); });
        timeBox.Controls.AddRange([new Label { Text = L.T("咲夜模式", "Sakuya mode"), AutoSize = true, Location = new Point(12, 12) }, sakuyaAttack, sakuya, timeStatus, resume, new Label { Text = L.T("选择模式后，用游戏放雷键切换时停；不消耗 Bomb，可自由移动。\n不可攻击模式下，自机射击也暂停。\n跨面保留模式；过面、对话及 Boss 换阶段时恢复时间。", "Select a mode, then use the Bomb action to toggle time stop. No Bomb cost.\nYou can move; No attacks also pauses player shots. The mode persists.\nStage changes, dialogue and boss phase changes resume time."), Bounds = new Rectangle(12, 75, 628, 62), ForeColor = Theme.Muted, Font = new Font(Font.FontFamily, 9F) }]);
        peace.CheckedChanged += (_, _) => ToggleMode(0);
        sakuya.CheckedChanged += (_, _) => ToggleMode(1);
        sakuyaAttack.CheckedChanged += (_, _) => ToggleMode(2);
        feedback.Bounds = new Rectangle(26, 806, 648, 43);
        feedback.Text = L.T("进入一局后，按需启用功能。", "Start a run, then enable the features you need.");
        feedback.ForeColor = Theme.Muted;
        var note = new Label { Text = L.T("辅助游玩的录像可能失同步，成绩请与正常挑战区分。", "Assisted replays may desync. Keep assisted results separate from normal clears."), Bounds = new Rectangle(26, 852, 648, 25), ForeColor = Theme.Muted, Font = new Font(Font.FontFamily, 9F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        Controls.AddRange([title, subtitle, reconnect, connection, table, speedBox, opacityBox, godBox, peaceBox, timeBox, feedback, note]);
        if (Icon is not null)
            Controls.Add(new PictureBox { Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(27, 27, 36, 36), BackColor = Color.Transparent });
        Theme.Apply(this);
        FormClosing += OnClosing;
        FormClosed += (_, _) =>
        {
            timer.Dispose();
            foreach (int key in registeredKeys) UnregisterHotKey(Handle, key);
        };
        Shown += (_, _) =>
        {
            if (!live) return;
            for (int index = 0; index < 4; index++)
                if (RegisterHotKey(Handle, index + 1, 0x4000, (uint)(Keys.F5 + index)))
                    registeredKeys.Add(index + 1);
            if (RegisterHotKey(Handle, 6, 0x4000, (uint)Keys.F9)) registeredKeys.Add(6);
            if (RegisterHotKey(Handle, 7, 0x4000, (uint)Keys.F11)) registeredKeys.Add(7);
            if (RegisterHotKey(Handle, 8, 0x4000, (uint)Keys.F10)) registeredKeys.Add(8);
            if (registeredKeys.Count < 7) feedback.Text = L.T("部分快捷键被其他程序占用，可直接点击开关。", "Some hotkeys are in use by another app. Use the switches instead.");
            timer.Start();
            TryAttach();
        };
        timer.Tick += (_, _) => Tick();
    }

    private void AddRow(Resource kind, string title, int initial, TableLayoutPanel table)
    {
        var row = new ResourceRow(kind, title, Math.Clamp(initial, 0, MemorySession.Spec(kind).Maximum));
        rows.Add(row);
        table.Controls.Add(row.Lock);
        table.Controls.Add(row.Current);
        table.Controls.Add(row.Target);
        table.Controls.Add(row.Apply);
        row.Lock.CheckedChanged += (_, _) =>
        {
            if (!updating && row.Lock.Checked) RunAction(() => WriteRow(row));
        };
        row.Apply.Click += (_, _) => RunAction(() => WriteRow(row));
        row.Target.ValueChanged += (_, _) =>
        {
            SavePreferences();
            if (row.Lock.Checked) RunAction(() => WriteRow(row));
        };
    }

    internal void PreparePreview()
    {
        if (live) throw new InvalidOperationException("Preview only");
        SetConnected(true);
        updating = true;
        try
        {
            connection.Text = L.T("已连接游戏 · 界面预览", "Connected · UI preview"); connection.ForeColor = Theme.Success;
            rows[0].Current.Text = "3"; rows[1].Current.Text = "2"; rows[2].Current.Text = "128";
            rows[2].Lock.Checked = true;
            sakuyaAttack.Checked = true; timeStatus.Text = L.T("已就绪 · 放雷键切换", "Ready · Bomb to toggle"); timeStatus.ForeColor = Theme.Success;
        }
        finally { updating = false; }
    }

    private Preferences LoadPreferences()
    {
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(prefsPath)) ?? new(); }
        catch { return new(); }
    }

    private void SavePreferences()
    {
        if (!live || rows.Count != 3) return;
        try
        {
            var values = new Preferences { Lives = (int)rows[0].Target.Value, Bombs = (int)rows[1].Target.Value, Power = (int)rows[2].Target.Value };
            File.WriteAllText(prefsPath, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { feedback.Text = L.T("目标值无法保存，下次启动将使用默认值。", "Could not save targets. Defaults will be used next time."); }
    }

    private void WriteRow(ResourceRow row)
    {
        if (session is null) throw new InvalidOperationException(L.T("请先启动并连接游戏", "Start and connect to the game first."));
        session.WriteResource(row.Kind, (int)row.Target.Value);
        feedback.Text = L.T($"已设置{row.Name}为 {row.Target.Value}", $"Set {row.Name} to {row.Target.Value}") + (row.Lock.Checked ? L.T("，正在持续锁定。", "; lock is active.") : "。");
        RefreshValues();
    }

    private void ToggleGod()
    {
        if (updating) return;
        RunAction(() =>
        {
            if (session is null) throw new InvalidOperationException(L.T("请先启动并连接游戏", "Start and connect to the game first."));
            session.SetInvincible(god.Checked);
            godStatus.Text = god.Checked ? L.T("已开启", "On") : L.T("已关闭", "Off");
            godStatus.ForeColor = god.Checked ? Theme.Success : Theme.Muted;
        });
    }

    private void ApplySpeed(int percent)
    {
        if (session is null) throw new InvalidOperationException(L.T("请先连接游戏", "Connect to the game first."));
        session.SetSpeed(percent); RefreshSpeed();
        feedback.Text = percent == 100 ? L.T("已恢复正常速度。", "Normal speed restored.") : L.T($"目标速度 {percent / 100.0:0.00}×。", $"Target speed: {percent / 100.0:0.00}×.");
        feedback.ForeColor = Theme.Muted;
    }

    private void RefreshSpeed()
    {
        updating = true;
        try { overdrive.Checked = session?.IsOverdrive ?? false; }
        finally { updating = false; }
        int desired = session?.DesiredSpeed ?? 100;
        speedStatus.Text = desired == 100 ? L.T("正常速度 1.00×", "Normal speed 1.00×") : desired == ModeCode.OverdrivePercent ? L.T("Overdrive · 目标 16×", "Overdrive · Target 16×") : L.T($"目标 {desired / 100.0:0.00}×", $"Target {desired / 100.0:0.00}×");
        speedStatus.ForeColor = desired == 100 ? Theme.Muted : Theme.Success;
    }

    private void ApplyOpacity()
    {
        if (session is null) throw new InvalidOperationException(L.T("请先连接游戏", "Connect to the game first."));
        session.SetOpacity((int)playerOpacity.Value, (int)enemyOpacity.Value);
        feedback.Text = L.T($"弹幕透明度：自机 {playerOpacity.Value}% · 敌机 {enemyOpacity.Value}%。", $"Opacity: player {playerOpacity.Value}% · enemy {enemyOpacity.Value}%.");
        feedback.ForeColor = Theme.Muted;
    }

    private void ToggleMode(int mode)
    {
        if (updating) return;
        RunAction(() =>
        {
            if (session is null) throw new InvalidOperationException(L.T("请先连接游戏", "Connect to the game first."));
            if (mode == 0) session.SetPeace(peace.Checked);
            else session.SetSakuya(mode == 2 ? sakuyaAttack.Checked : sakuya.Checked, mode == 2);
            RefreshModes();
            feedback.Text = mode != 0 && session.ModeState.Enabled ? L.T("咲夜模式已就绪：使用游戏放雷键切换时停。", "Sakuya mode ready: use the Bomb action to toggle time stop.") : L.T("玩法开关已更新。", "Mode switches updated.");
            feedback.ForeColor = Theme.Muted;
        });
    }

    private void RefreshModes()
    {
        var state = session?.ModeState ?? (Peace: false, Enabled: false, Active: false);
        updating = true;
        bool attack = session?.AttackAllowed ?? false;
        try { peace.Checked = state.Peace; sakuya.Checked = state.Enabled && !attack; sakuyaAttack.Checked = state.Enabled && attack; }
        finally { updating = false; }
        peaceStatus.Text = state.Peace ? L.T("已开启", "On") : L.T("已关闭", "Off");
        peaceStatus.ForeColor = state.Peace ? Theme.Success : Theme.Muted;
        timeStatus.Text = state.Active ? (attack ? L.T("时停中 · 可攻击", "Stopped · Attacks allowed") : L.T("时停中 · 不可攻击", "Stopped · No attacks")) : state.Enabled ? L.T("已就绪 · 放雷键切换", "Ready · Bomb to toggle") : L.T("已关闭", "Off");
        timeStatus.ForeColor = state.Active ? Theme.Accent : state.Enabled ? Theme.Success : Theme.Muted;
        resume.Enabled = state.Active;
    }

    private void Tick()
    {
        try
        {
            if (session is not null && !session.IsAlive)
            {
                session.Dispose();
                session = null;
                SetConnected(false);
                connection.Text = L.T("游戏已退出，等待重新启动…", "Game closed. Waiting for it to restart…");
                connection.ForeColor = Theme.Muted;
            }
            if (session is null)
            {
                if (Environment.TickCount64 >= nextAttach) TryAttach();
                return;
            }
            foreach (var row in rows)
                if (row.Lock.Checked) session.WriteResource(row.Kind, (int)row.Target.Value);
            if (god.Checked) session.VerifySignature(true);
            if (session.ModesInstalled)
            {
                session.MaintainModes();
                RefreshModes();
                RefreshSpeed();
            }
            RefreshValues();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void RefreshValues()
    {
        foreach (var row in rows)
        {
            int? value = session?.ReadResource(row.Kind);
            row.Current.Text = value is null ? "—" : value == 255 && row.Kind != Resource.Power ? L.T("结束", "Ended") : value.ToString();
        }
    }

    private void TryAttach()
    {
        nextAttach = Environment.TickCount64 + 2000;
        var games = Process.GetProcessesByName("th06nc");
        try
        {
            if (games.Length == 0) return;
            if (games.Length != 1) throw new InvalidOperationException(L.T("发现多个游戏进程，请只保留一个。", "Multiple game processes found. Keep only one running."));
            if (Process.GetProcessesByName("TH06NCTrainer").Any(p => { using (p) return p.Id != Environment.ProcessId; }))
                throw new InvalidOperationException(L.T("请先关闭旧版或其他修改器窗口。", "Close older helpers and other trainers first."));
            session = new MemorySession(games[0]);
            SetConnected(true);
            connection.Text = L.T("已连接游戏", "Connected");
            connection.ForeColor = Theme.Success;
            RefreshValues();
        }
        catch (Exception ex)
        {
            connection.Text = L.Error(ex);
            connection.ForeColor = Theme.Error;
        }
        finally { foreach (var game in games) game.Dispose(); }
    }

    private void SetConnected(bool connected)
    {
        updating = true;
        try
        {
            foreach (var row in rows)
            {
                row.Lock.Checked = false;
                row.Lock.Enabled = connected;
                row.Apply.Enabled = connected;
                row.Current.Text = "—";
            }
            god.Checked = false;
            overdrive.Checked = false;
            overdrive.Enabled = applyOpacity.Enabled = resetOpacity.Enabled = connected;
            playerOpacity.Value = enemyOpacity.Value = 100;
            foreach (var button in speedButtons) button.Enabled = connected;
            speedStatus.Text = L.T("正常速度 1.00×", "Normal speed 1.00×");
            speedStatus.ForeColor = Theme.Muted;
            god.Enabled = connected;
            godStatus.Text = L.T("已关闭", "Off");
            godStatus.ForeColor = Theme.Muted;
            peace.Checked = sakuya.Checked = sakuyaAttack.Checked = false;
            peace.Enabled = sakuya.Enabled = sakuyaAttack.Enabled = connected;
            peaceStatus.Text = timeStatus.Text = L.T("已关闭", "Off");
            peaceStatus.ForeColor = timeStatus.ForeColor = Theme.Muted;
            resume.Enabled = false;
        }
        finally { updating = false; }
    }

    private void DisableAll()
    {
        foreach (var row in rows) row.Lock.Checked = false;
        if (session is not null && !session.IsAlive)
        {
            session.Dispose(); session = null;
            SetConnected(false);
            return;
        }
        session?.DisableModes();
        session?.RestoreSpeed();
        session?.RestoreInvincible();
        session?.SetOpacity(100, 100);
        updating = true;
        god.Checked = false;
        peace.Checked = sakuya.Checked = sakuyaAttack.Checked = false;
        updating = false;
        godStatus.Text = L.T("已关闭", "Off");
        godStatus.ForeColor = Theme.Muted;
        RefreshModes();
        RefreshSpeed();
        feedback.Text = L.T("已恢复游戏设置。", "Game settings restored.");
    }

    private void Reconnect() => RunAction(() =>
    {
        DisableAll();
        session?.Dispose();
        session = null;
        SetConnected(false);
        connection.Text = L.T("等待游戏启动…", "Waiting for the game…");
        connection.ForeColor = Theme.Muted;
        TryAttach();
        feedback.ForeColor = Theme.Muted;
        if (live) timer.Start();
    });

    private void RunAction(Action action)
    {
        try { action(); }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex)
    {
        LogFailure(ex);
        timer.Stop();
        foreach (var row in rows) row.Lock.Checked = false;
        string message = L.Error(ex);
        try { DisableAll(); }
        catch (Exception restore) { message += L.T("；恢复失败：", "; restoration failed: ") + L.Error(restore) + L.T("。请关闭游戏以清除内存修改。", ". Close the game to clear memory changes."); }
        feedback.Text = message;
        feedback.ForeColor = Theme.Error;
        connection.Text = L.T("操作已停止；处理后可点击重新连接。", "Operations stopped. Resolve the issue, then reconnect.");
        connection.ForeColor = Theme.Error;
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        timer.Stop();
        try
        {
            // Dispose owns recovery; no UI reads of a terminating process here.
            session?.Dispose();
            session = null;
            SavePreferences();
        }
        catch (Exception ex)
        {
            e.Cancel = true;
            LogFailure(ex);
            if (!live) throw;
            MessageBox.Show(L.T("尚未成功恢复游戏状态：", "Could not restore the game state: ") + L.Error(ex.GetBaseException()) + L.T("\n可以稍后重试；游戏结束后本窗口可以正常关闭。", "\nTry again later. Once the game exits, this window can close normally."), L.T("恢复失败", "Restoration failed"));
            timer.Start();
        }
    }

    private void LogFailure(Exception error)
    {
        if (!live) return;
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "error.log"), $"{DateTimeOffset.Now:O}\n{error}\n\n"); }
        catch { /* Diagnostics must never block cleanup. */ }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312)
        {
            int id = message.WParam.ToInt32();
            if (id is >= 1 and <= 3 && rows[id-1].Lock.Enabled) rows[id-1].Lock.Checked = !rows[id-1].Lock.Checked;
            else if (id == 4 && god.Enabled) god.Checked = !god.Checked;
            else if (id == 6 && peace.Enabled) peace.Checked = !peace.Checked;
            else if (id == 7 && sakuya.Enabled) sakuya.Checked = !sakuya.Checked;
            else if (id == 8 && sakuyaAttack.Enabled) sakuyaAttack.Checked = !sakuyaAttack.Checked;
        }
        base.WndProc(ref message);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

internal sealed class ResourceRow
{
    public Resource Kind { get; }
    public string Name => Kind switch { Resource.Lives => L.T("残机", "Lives"), Resource.Bombs => "Bomb", _ => "POWER" };
    public CheckBox Lock { get; }
    public Label Current { get; } = new() { Text = "—", AutoSize = true, Margin = new Padding(3, 5, 3, 3) };
    public NumericUpDown Target { get; }
    public Button Apply { get; } = new ThemedButton() { Text = L.T("设置一次", "Set once"), Enabled = false, Size = new Size(108, 29), Margin = new Padding(3, 0, 3, 0) };
    public ResourceRow(Resource kind, string title, int value)
    {
        Kind = kind;
        Lock = new ThemedCheckBox { Text = title, AutoSize = true, Enabled = false, Margin = new Padding(3, 5, 3, 3) };
        Target = new NumericUpDown { Minimum = 0, Maximum = MemorySession.Spec(kind).Maximum, Value = value, Width = 85 };
    }
}
