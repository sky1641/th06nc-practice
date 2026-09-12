using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TH06NCTrainer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
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
        if (args.Length > 0 && args[0] == "--preview")
        {
            using var form = new TrainerForm(false);
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
            MessageBox.Show("新版修改器已经运行，请切换到已有窗口。", "新典修改器");
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

internal sealed class TrainerForm : Form
{
    private readonly Label connection = new() { AutoSize = false };
    private readonly Label feedback = new() { AutoSize = false };
    private readonly Label godStatus = new() { AutoSize = true, Text = "已关闭", ForeColor = Color.DimGray };
    private readonly CheckBox god = new() { AutoSize = true, Text = "无敌  [F8]" };
    private readonly CheckBox peace = new() { AutoSize = true, Text = "和平观光  [F9]", Enabled = false };
    private readonly CheckBox sakuya = new() { AutoSize = true, Text = "不可攻击  [F11]", Enabled = false };
    private readonly CheckBox sakuyaAttack = new() { AutoSize = true, Text = "可攻击  [F10]", Enabled = false };
    private readonly Label peaceStatus = new() { AutoSize = true, Text = "已关闭", ForeColor = Color.DimGray };
    private readonly Label timeStatus = new() { AutoSize = true, Text = "已关闭", ForeColor = Color.DimGray };
    private readonly Button resume = new() { Text = "立即恢复时间", Enabled = false };
    private readonly NumericUpDown speedTarget = new() { Minimum = 25, Maximum = 200, Increment = 5, Value = 100, Width = 70 };
    private readonly Label speedStatus = new() { Text = "正常速度 1.00×", AutoSize = true, ForeColor = Color.DimGray };
    private readonly List<Button> speedButtons = [];
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
        Text = "东方红魔乡：新典 · 练习辅助 v3.2";
        Font = new Font("Microsoft YaHei UI", 10F);
        BackColor = Color.FromArgb(247, 249, 252);
        ClientSize = new Size(700, 812);
        MinimumSize = new Size(716, 650);
        AutoScroll = true;
        AutoScrollMinSize = new Size(700, 812);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var title = new Label { Text = "TH06 New Classic", AutoSize = true, Font = new Font(Font.FontFamily, 21F, FontStyle.Bold), Location = new Point(24, 17) };
        var subtitle = new Label { Text = "新典练习辅助  /  资源设置 · 变速 · 观光与时停", AutoSize = true, ForeColor = Color.DimGray, Location = new Point(26, 58) };
        var reconnect = new Button { Text = "重新连接", Bounds = new Rectangle(554, 25, 120, 35), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        reconnect.Click += (_, _) => Reconnect();
        connection.Bounds = new Rectangle(26, 92, 648, 40);
        connection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        connection.Text = "等待游戏启动…";
        var table = new TableLayoutPanel { Bounds = new Rectangle(24, 143, 652, 145), ColumnCount = 4, RowCount = 4, BackColor = Color.White, Padding = new Padding(12), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        foreach (string heading in new[] { "功能 / 快捷键", "当前值", "锁定目标", "操作" })
            table.Controls.Add(new Label { Text = heading, AutoSize = true, ForeColor = Color.DimGray });
        var prefs = LoadPreferences();
        AddRow(Resource.Lives, "锁残机  [F5]", prefs.Lives, table);
        AddRow(Resource.Bombs, "锁 Bomb  [F6]", prefs.Bombs, table);
        AddRow(Resource.Power, "锁 POWER  [F7]", prefs.Power, table);
        var speedBox = new Panel { Bounds = new Rectangle(24, 300, 652, 88), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        speedTarget.Location = new Point(92, 9);
        speedStatus.Location = new Point(258, 13);
        var applySpeed = new Button { Text = "应用", Bounds = new Rectangle(183, 8, 63, 29), Enabled = false };
        applySpeed.Click += (_, _) => RunAction(() => ApplySpeed((int)speedTarget.Value));
        speedButtons.Add(applySpeed);
        speedBox.Controls.AddRange([new Label { Text = "游戏速度", AutoSize = true, Location = new Point(12, 13) }, speedTarget, new Label { Text = "%", AutoSize = true, Location = new Point(162, 13) }, applySpeed, speedStatus]);
        int speedX = 12;
        foreach (int percent in new[] { 50, 75, 100, 150, 200 })
        {
            var preset = new Button { Text = percent == 100 ? "恢复 1×" : $"{percent / 100.0:0.##}×", Bounds = new Rectangle(speedX, 47, 82, 28), Enabled = false };
            preset.Click += (_, _) => RunAction(() => { speedTarget.Value = percent; ApplySpeed(percent); });
            speedButtons.Add(preset); speedBox.Controls.Add(preset); speedX += 88;
        }
        speedBox.Controls.Add(new Label { Text = "音乐原速；跨面保留\n加速受性能/垂直同步限制", Bounds = new Rectangle(454, 45, 190, 39), ForeColor = Color.DimGray, Font = new Font(Font.FontFamily, 8.5F) });
        var godBox = new Panel { Bounds = new Rectangle(24, 400, 652, 67), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        god.Location = new Point(12, 12);
        god.Enabled = false;
        godStatus.Location = new Point(250, 12);
        godBox.Controls.AddRange([god, godStatus, new Label { Text = "覆盖普通弹、敌机接触及激光；关闭或正常退出时恢复。", AutoSize = true, Location = new Point(12, 39), ForeColor = Color.DimGray, Font = new Font(Font.FontFamily, 9F) }]);
        god.CheckedChanged += (_, _) => ToggleGod();
        var peaceBox = new Panel { Bounds = new Rectangle(24, 478, 652, 70), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        peace.Location = new Point(12, 10);
        peaceStatus.Location = new Point(250, 10);
        peaceBox.Controls.AddRange([peace, peaceStatus, new Label { Text = "清除并禁止敌弹与激光，过滤发弹音效；保留音乐，免接触伤害。", AutoSize = true, Location = new Point(12, 40), ForeColor = Color.DimGray, Font = new Font(Font.FontFamily, 9F) }]);
        var timeBox = new Panel { Bounds = new Rectangle(24, 560, 652, 148), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        sakuyaAttack.Location = new Point(12, 44);
        sakuya.Location = new Point(216, 44);
        timeStatus.Location = new Point(132, 12);
        resume.Bounds = new Rectangle(504, 9, 133, 30);
        resume.Click += (_, _) => RunAction(() => { session?.ResumeTime(); RefreshModes(); });
        timeBox.Controls.AddRange([new Label { Text = "咲夜模式", AutoSize = true, Location = new Point(12, 12) }, sakuyaAttack, sakuya, timeStatus, resume, new Label { Text = "先选模式，再用游戏放雷键切换时停；不消耗 Bomb，可自由移动。\n可攻击：射击并结算伤害；不可攻击：自机射击也暂停。\n跨面保留模式但恢复时间；Boss 换阶段时自动恢复，避免卡关。", Bounds = new Rectangle(12, 78, 628, 64), ForeColor = Color.DimGray, Font = new Font(Font.FontFamily, 9F) }]);
        peace.CheckedChanged += (_, _) => ToggleMode(0);
        sakuya.CheckedChanged += (_, _) => ToggleMode(1);
        sakuyaAttack.CheckedChanged += (_, _) => ToggleMode(2);
        feedback.Bounds = new Rectangle(26, 721, 648, 55);
        feedback.Text = "先进入一局，再开启需要的功能。POWER 满值为 128。";
        feedback.ForeColor = Color.DimGray;
        var note = new Label { Text = "不限制得分；辅助成绩不用于正常通关比较。录像可能失同步。F12 留给 Steam 截图。", Bounds = new Rectangle(26, 782, 648, 25), ForeColor = Color.DimGray, Font = new Font(Font.FontFamily, 9F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        Controls.AddRange([title, subtitle, reconnect, connection, table, speedBox, godBox, peaceBox, timeBox, feedback, note]);
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
            if (registeredKeys.Count < 7) feedback.Text = "部分快捷键被其他程序占用，可直接点击开关。";
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
        catch { feedback.Text = "目标值无法保存，下次启动将使用默认值。"; }
    }

    private void WriteRow(ResourceRow row)
    {
        if (session is null) throw new InvalidOperationException("请先启动并连接游戏");
        session.WriteResource(row.Kind, (int)row.Target.Value);
        feedback.Text = $"已设置{row.Name}为 {row.Target.Value}" + (row.Lock.Checked ? "，正在持续锁定。" : "。");
        RefreshValues();
    }

    private void ToggleGod()
    {
        if (updating) return;
        RunAction(() =>
        {
            if (session is null) throw new InvalidOperationException("请先启动并连接游戏");
            session.SetInvincible(god.Checked);
            godStatus.Text = god.Checked ? "已开启 · 中弹不进入死亡状态" : "已关闭 · 原始判定已恢复";
            godStatus.ForeColor = god.Checked ? Color.DarkGreen : Color.DimGray;
        });
    }

    private void ApplySpeed(int percent)
    {
        if (session is null) throw new InvalidOperationException("请先连接游戏");
        session.SetSpeed(percent); RefreshSpeed();
        feedback.Text = percent == 100 ? "已恢复正常速度；其他辅助开关保持不变。" : $"目标速度 {percent / 100.0:0.00}×。变速只改变游戏节奏，不调整音乐、不限制得分。";
        feedback.ForeColor = Color.DimGray;
    }

    private void RefreshSpeed()
    {
        int desired = session?.DesiredSpeed ?? 100, effective = session?.EffectiveSpeed ?? 100;
        speedStatus.Text = desired == 100 ? "正常速度 1.00×" : effective == desired ? $"已应用 {desired / 100.0:0.00}×" : $"目标 {desired / 100.0:0.00}× · 等待游戏帧";
        speedStatus.ForeColor = desired == 100 ? Color.DimGray : Color.DarkGreen;
    }

    private void ToggleMode(int mode)
    {
        if (updating) return;
        RunAction(() =>
        {
            if (session is null) throw new InvalidOperationException("请先连接游戏");
            if (mode == 0) session.SetPeace(peace.Checked);
            else session.SetSakuya(mode == 2 ? sakuyaAttack.Checked : sakuya.Checked, mode == 2);
            RefreshModes();
            feedback.Text = mode != 0 && session.ModeState.Enabled ? "咲夜模式已就绪：使用游戏放雷键切换时停。" : "玩法开关已更新。";
            feedback.ForeColor = Color.DimGray;
        });
    }

    private void RefreshModes()
    {
        var state = session?.ModeState ?? (Peace: false, Enabled: false, Active: false);
        updating = true;
        bool attack = session?.AttackAllowed ?? false;
        try { peace.Checked = state.Peace; sakuya.Checked = state.Enabled && !attack; sakuyaAttack.Checked = state.Enabled && attack; }
        finally { updating = false; }
        peaceStatus.Text = state.Peace ? "已开启 · 禁弹与接触保护" : "已关闭";
        peaceStatus.ForeColor = state.Peace ? Color.DarkGreen : Color.DimGray;
        timeStatus.Text = state.Active ? (attack ? "时停中 · 可攻击" : "时停中 · 不可攻击") : state.Enabled ? "已就绪 · 放雷键切换" : "已关闭";
        timeStatus.ForeColor = state.Active ? Color.MediumSlateBlue : state.Enabled ? Color.DarkGreen : Color.DimGray;
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
                connection.Text = "游戏已退出，等待重新启动…";
                connection.ForeColor = Color.DimGray;
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
            row.Current.Text = value is null ? "—" : value == 255 && row.Kind != Resource.Power ? "结束" : value.ToString();
        }
    }

    private void TryAttach()
    {
        nextAttach = Environment.TickCount64 + 2000;
        var games = Process.GetProcessesByName("th06nc");
        try
        {
            if (games.Length == 0) return;
            if (games.Length != 1) throw new InvalidOperationException("发现多个游戏进程，请只保留一个。");
            if (Process.GetProcessesByName("TH06NCTrainer").Any(p => { using (p) return p.Id != Environment.ProcessId; }))
                throw new InvalidOperationException("请先关闭旧版或其他修改器窗口。");
            session = new MemorySession(games[0]);
            SetConnected(true);
            connection.Text = $"已连接 · PID {session.Pid} · 游戏版本校验通过";
            connection.ForeColor = Color.DarkGreen;
            RefreshValues();
        }
        catch (Exception ex)
        {
            connection.Text = ex.Message;
            connection.ForeColor = Color.Firebrick;
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
            foreach (var button in speedButtons) button.Enabled = connected;
            speedStatus.Text = "正常速度 1.00×";
            speedStatus.ForeColor = Color.DimGray;
            god.Enabled = connected;
            godStatus.Text = "已关闭";
            godStatus.ForeColor = Color.DimGray;
            peace.Checked = sakuya.Checked = sakuyaAttack.Checked = false;
            peace.Enabled = sakuya.Enabled = sakuyaAttack.Enabled = connected;
            peaceStatus.Text = timeStatus.Text = "已关闭";
            peaceStatus.ForeColor = timeStatus.ForeColor = Color.DimGray;
            resume.Enabled = false;
        }
        finally { updating = false; }
    }

    private void DisableAll()
    {
        foreach (var row in rows) row.Lock.Checked = false;
        session?.DisableModes();
        session?.RestoreSpeed();
        session?.RestoreInvincible();
        updating = true;
        god.Checked = false;
        peace.Checked = sakuya.Checked = sakuyaAttack.Checked = false;
        updating = false;
        godStatus.Text = "已关闭";
        godStatus.ForeColor = Color.DimGray;
        RefreshModes();
        RefreshSpeed();
        feedback.Text = "全部功能已关闭，时间与原始中弹判定已恢复。";
    }

    private void Reconnect() => RunAction(() =>
    {
        DisableAll();
        session?.Dispose();
        session = null;
        SetConnected(false);
        connection.Text = "等待游戏启动…";
        connection.ForeColor = Color.DimGray;
        TryAttach();
        feedback.ForeColor = Color.DimGray;
        if (live) timer.Start();
    });

    private void RunAction(Action action)
    {
        try { action(); }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex)
    {
        timer.Stop();
        foreach (var row in rows) row.Lock.Checked = false;
        string message = ex.Message;
        try { DisableAll(); }
        catch (Exception restore) { message += "；恢复失败：" + restore.Message + "。请关闭游戏以清除内存修改。"; }
        feedback.Text = message;
        feedback.ForeColor = Color.Firebrick;
        connection.Text = "操作已停止；处理后可点击重新连接。";
        connection.ForeColor = Color.Firebrick;
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        timer.Stop();
        try
        {
            DisableAll();
            session?.Dispose();
            session = null;
            SavePreferences();
        }
        catch (Exception ex)
        {
            e.Cancel = true;
            MessageBox.Show("尚未成功恢复游戏指令：" + ex.Message + "\n请先关闭游戏，再退出修改器。", "恢复失败");
        }
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
    public string Name => Kind switch { Resource.Lives => "残机", Resource.Bombs => "Bomb", _ => "POWER" };
    public CheckBox Lock { get; }
    public Label Current { get; } = new() { Text = "—", AutoSize = true, Margin = new Padding(3, 5, 3, 3) };
    public NumericUpDown Target { get; }
    public Button Apply { get; } = new() { Text = "设置一次", Enabled = false, Size = new Size(108, 29), Margin = new Padding(3, 0, 3, 0) };
    public ResourceRow(Resource kind, string title, int value)
    {
        Kind = kind;
        Lock = new CheckBox { Text = title, AutoSize = true, Enabled = false, Margin = new Padding(3, 5, 3, 3) };
        Target = new NumericUpDown { Minimum = 0, Maximum = MemorySession.Spec(kind).Maximum, Value = value, Width = 85 };
    }
}
