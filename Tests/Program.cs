using HarmonicaPlayer;

int passed = 0;
void Expect(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL: " + name);
    passed++;
}
var notes = ScoreParser.Parse("6 5 4 【1 21】64 565 456 434 6 #6654 565 434");
Expect(notes.Count == 28, "示例音符总数");
Expect(notes[3].Octave == 1 && notes[5].Octave == 1 && notes[6].Octave == 0, "高音范围");
var sharp = ScoreParser.Parse("#6654");
Expect(sharp[0].Sharp && !sharp[1].Sharp, "#只影响一个音");
Expect(ScoreParser.Parse("（1）(2)【#6】0").Select(n => n.Octave).SequenceEqual(new[] {-1,-1,1,0}), "音区");
Expect(ScoreParser.Parse("565").Select(n=>n.Degree).SequenceEqual(ScoreParser.Parse("5 6 5").Select(n=>n.Degree)), "空格不改变音符");
foreach (string invalid in new[] {"", "【1", "1】", "（1)", "#", "##1", "#0", "8", "标题1", "【（1）】", "#【1】"})
{
    bool rejected = false;
    try { ScoreParser.Parse(invalid); } catch (FormatException) { rejected = true; }
    Expect(rejected, "拒绝非法谱: " + invalid);
}
Expect(ScoreParser.Parse("1 2\n3").Sum(n => n.Beats) == 3, "空格换行不增加拍数");
var rhythm = ScoreParser.Parse("1 2_ 3__ 4. 5_. 6 — — 0_ 0 -");
Expect(rhythm.Select(n => n.Beats).SequenceEqual(new[] {1.0, .5, .25, 1.5, .75, 3.0, .5, 2.0}), "完整时值解析");
Expect(rhythm.Count == 8 && rhythm[5].Degree == 6, "延长不生成重复音");
Expect(rhythm[^1].Degree == 0 && rhythm[^1].Beats == 2, "休止延长");
Expect(ScoreParser.Parse("【#1_.】 （2）-").Select(n => n.Beats).SequenceEqual(new[] {.75, 2.0}), "音区半音与时值组合");
Expect(ScoreParser.Parse("1 2_ 3_ 5 — | 0 6. 5_ 1 |").Sum(n => n.Beats) * 500 == 4000, "120速度八拍为四秒");
foreach (string invalid in new[] {"-1", "_1", "1___", "1..", "1-_", "1._", "1 | -", "#-1", "1-.", "0" + new string('-', 64)})
{
    bool rejected = false;
    try { ScoreParser.Parse(invalid); } catch (FormatException) { rejected = true; }
    Expect(rejected, "拒绝非法时值: " + invalid);
}
Console.WriteLine($"PASS: {passed} tests");

void RejectHotkey(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (FormatException) { rejected = true; }
    Expect(rejected, name);
}
HotkeyBinding.ValidatePair(new(0x75, 2), new(0x77));
Expect(new HotkeyBinding(0x75, 7).Label == "Ctrl + Alt + Shift + F6", "组合键显示及MOD位对应");
RejectHotkey(() => HotkeyBinding.ValidatePair(new(0x75), new(0x75)), "拒绝重复开始停止键");
foreach (var binding in new[] {new HotkeyBinding(0x7B), new HotkeyBinding(0x75, 8),
    new HotkeyBinding(0x5A, 2), new HotkeyBinding(0x41), new HotkeyBinding(0x73, 1),
    new HotkeyBinding(0x2E, 3), new HotkeyBinding(0), new HotkeyBinding(0x11),
    new HotkeyBinding(0x1B, 2), new HotkeyBinding(0x20, 1)})
    RejectHotkey(() => HotkeyBinding.ValidatePair(binding, new(0x77)), "拒绝无效/保留键 " + binding.Label);
Expect(new HotkeyBinding(0x41, 2).Error() == null && new HotkeyBinding(0x24).Error() == null, "接受Ctrl+A和Home");

var backend = new FakeHotkeys();
var manager = new HotkeyController(backend);
backend.Occupied.Add(HotkeyBinding.DefaultStart);
manager.Apply(HotkeyBinding.DefaultStart, HotkeyBinding.DefaultStop);
Expect(!manager.StartReady && manager.StopReady && manager.StartError == 1409, "开始冲突仍保留停止键");
Expect(manager.Describe(HotkeyBinding.DefaultStart, HotkeyBinding.DefaultStop).Contains("点击“开始”"), "开始冲突可用按钮提示");
backend.Occupied.Clear(); backend.Occupied.Add(HotkeyBinding.DefaultStop);
manager.Apply(HotkeyBinding.DefaultStart, HotkeyBinding.DefaultStop);
Expect(manager.StartReady && !manager.StopReady, "停止冲突独立标记");
Expect(manager.Describe(HotkeyBinding.DefaultStart, HotkeyBinding.DefaultStop).Contains("禁止真实演奏"), "停止冲突阻断提示");
backend.Occupied.Clear(); manager.Apply(HotkeyBinding.DefaultStart, HotkeyBinding.DefaultStop);
Expect(manager.StartReady && manager.StopReady, "解除占用后重试成功");
manager.Apply(HotkeyBinding.DefaultStop, HotkeyBinding.DefaultStart);
Expect(backend.Registered[1] == HotkeyBinding.DefaultStop && backend.Registered[2] == HotkeyBinding.DefaultStart, "互换快捷键先释放旧注册");
RejectHotkey(() => manager.Apply(new(0x75), new(0x75)), "无效配置拒绝应用");
Expect(manager.StartReady && manager.StopReady && backend.Registered.Count == 2, "无效配置保留已有注册");
manager.Suspend(); manager.Suspend();
Expect(backend.Registered.Count == 0 && !manager.StartReady && !manager.StopReady, "多次暂停幂等");
manager.Apply(new(0x75, 2), new(0x77, 5));
Expect(backend.Registered[1].Modifiers == 2 && backend.Registered[2].Modifiers == 5, "组合键正确传递至注册层");

string settingsDir = Path.Combine(Path.GetTempPath(), "HarmonicaPlayerTests-" + Guid.NewGuid().ToString("N"));
string settingsPath = Path.Combine(settingsDir, "settings.json");
try
{
    var defaults = SettingsStore.Load(settingsPath, out var warning);
    Expect(warning == null && defaults.Start == HotkeyBinding.DefaultStart, "缺少设置使用默认值");
    var custom = new AppSettings { Start = new(0x75, 2), Stop = new(0x77, 4), Bpm = 90, Gap = 10 };
    SettingsStore.Save(settingsPath, custom);
    Expect(SettingsStore.Load(settingsPath, out warning) == custom && warning == null, "快捷键和时间设置往返保存");
    var changed = custom with { Bpm = 100 };
    SettingsStore.Save(settingsPath, changed);
    Expect(SettingsStore.Load(settingsPath, out warning).Bpm == 100 && Directory.GetFiles(settingsDir).Length == 1, "原子覆盖无临时文件残留");
    RejectHotkey(() => SettingsStore.Save(settingsPath, custom with { Bpm = 0 }), "不保存无效数值");
    Expect(SettingsStore.Load(settingsPath, out warning).Bpm == 100, "无效写入保留旧设置");
    File.WriteAllText(settingsPath, "{bad json");
    Expect(SettingsStore.Load(settingsPath, out warning) == new AppSettings() && warning != null, "损坏设置回退并告知");
    File.WriteAllText(settingsPath, "{\"Start\":null}");
    Expect(SettingsStore.Load(settingsPath, out warning).Start == HotkeyBinding.DefaultStart && warning != null, "空快捷键安全回退");
}
finally { if (Directory.Exists(settingsDir)) Directory.Delete(settingsDir, true); }
Console.WriteLine($"PASS TOTAL: {passed} tests (parser, hotkeys, settings)");

// The settings writer must not perform a blocking disk action on the caller.
var initial = new AppSettings();
int unchangedWrites = 0;
var unchangedWriter = new SettingsWriter(_ => Interlocked.Increment(ref unchangedWrites), initial);
Expect(await unchangedWriter.SaveAsync(initial) == null && unchangedWrites == 0, "已保存且未更改时不重复写盘");
using var entered = new ManualResetEventSlim();
using var releaseWrite = new ManualResetEventSlim();
var writeOrder = new List<int>();
var queuedWriter = new SettingsWriter(value =>
{
    if (value.Bpm == 90)
    {
        entered.Set();
        if (!releaseWrite.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test gate timed out.");
    }
    lock (writeOrder) writeOrder.Add(value.Bpm);
}, initial);
try
{
    var firstSave = queuedWriter.SaveAsync(initial with { Bpm = 90 });
    Expect(entered.Wait(TimeSpan.FromSeconds(2)) && !firstSave.IsCompleted, "慢写盘在后台等待，调用方已返回");
    Expect(ReferenceEquals(firstSave, queuedWriter.SaveAsync(initial with { Bpm = 90 })), "重复待保存值共用同一任务");
    var finalSave = queuedWriter.SaveAsync(initial);
    Expect(!finalSave.IsCompleted && ReferenceEquals(finalSave, queuedWriter.FlushAsync()), "关闭等待最后一次保存而非首个保存");
    releaseWrite.Set();
    Expect(await finalSave.WaitAsync(TimeSpan.FromSeconds(3)) == null, "后台队列可完整结束");
    Expect(writeOrder.SequenceEqual(new[] {90, 120}), "快速修改再还原仍按顺序写入最终值");
    await queuedWriter.SaveAsync(initial);
    Expect(writeOrder.Count == 2, "关闭时已保存快照不重复写入");
}
finally { releaseWrite.Set(); }
int attempts = 0;
var retryWriter = new SettingsWriter(_ =>
{
    if (Interlocked.Increment(ref attempts) == 1) throw new IOException("Simulated disk failure");
});
Expect((await retryWriter.SaveAsync(initial))?.Contains("Simulated") == true, "后台保存错误返回给界面");
Expect(await retryWriter.SaveAsync(initial) == null && attempts == 2, "相同值写入失败后可重试");
Console.WriteLine($"PASS TOTAL: {passed} tests including async settings writer");

// v0.2.0: explicit durations and highest do; no native input is sent.
var customNotes = ScoreParser.Parse("5:1.25 【1】:2.5 0:0.25 【【1】】:1.25 2");
Expect(customNotes.Count == 5, "指定时值不产生额外音符");
Expect(customNotes.Select(n => n.Beats).SequenceEqual(new[] {1.25, 2.5, .25, 1.25, 1.0}), "指定时值及重置");
Expect(customNotes[3].Octave == 2 && customNotes[4].Octave == 0, "最高do音区正确恢复");
Expect(ScoreParser.Parse("【【1】】_.").Single().Beats == .75, "最高do兼容旧时值符号");
Expect(ScoreParser.Parse("5:1.25 | 0:0.25 1:2.5").Sum(n => n.Beats) == 4, "自定义四拍时间线");
Expect(ScoreParser.Parse("0:64").Single().Beats == 64, "支持64拍上限");
foreach (string bad in new[] {"【【2】】", "【【#1】】", "【【1】", "【【11】】", "【【1_】】", "5:0", "5:-1", "5:65", "5:", "5:1.2.3", "5:1.", "5_:1.25", "5.:2", "5:1.25_", "5:1.25 —", "5:1:2", "5 | :1.25"})
{
    bool rejected = false;
    try { ScoreParser.Parse(bad); } catch (FormatException) { rejected = true; }
    Expect(rejected, "拒绝含糊或非法新格式: " + bad);
}
var oldCulture = System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
    Expect(ScoreParser.Parse("5:1.25").Single().Beats == 1.25, "小数点不受系统区域设置影响");
}
finally { System.Globalization.CultureInfo.CurrentCulture = oldCulture; }
try { ScoreParser.Parse("1\n8"); Expect(false, "非法符号必须报错"); }
catch (FormatException e) { Expect(e.Message.Contains("第2行、第1列"), "错误定位到行列"); }
string legacyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
try
{
    File.WriteAllText(legacyPath, "{\"Rhythm\":false,\"Duration\":0,\"SpaceGap\":-1,\"LineGap\":-1,\"Bpm\":95,\"Gap\":15}");
    var migrated = SettingsStore.Load(legacyPath, out var migrationWarning);
    Expect(migrationWarning == null && migrated.Bpm == 95 && migrated.Gap == 15, "旧模式废弃字段不影响有效设置迁移");
    SettingsStore.Save(legacyPath, migrated);
    Expect(!File.ReadAllText(legacyPath).Contains("Rhythm"), "保存时移除废弃模式字段");
}
finally { File.Delete(legacyPath); }
try { ScoreParser.Parse("1\n8"); }
catch (ScoreFormatException e) { Expect(e.Position == 2, "错误定位保留精确字符索引"); }
Console.WriteLine($"PASS FINAL: {passed} tests including v0.2.0");

sealed class FakeHotkeys : IHotkeyBackend
{
    public HashSet<HotkeyBinding> Occupied { get; } = new();
    public Dictionary<int, HotkeyBinding> Registered { get; } = new();
    public int Register(int id, HotkeyBinding binding)
    {
        if (Occupied.Contains(binding) || Registered.Values.Contains(binding) || Registered.ContainsKey(id)) return 1409;
        Registered[id] = binding; return 0;
    }
    public void Unregister(int id) => Registered.Remove(id);
}
