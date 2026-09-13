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
int[] Pauses(string text) => ScoreParser.Parse(text).Select(n => n.PauseBefore(100, 300)).ToArray();
Expect(Pauses("12").SequenceEqual(new[] {0, 0}), "连写不加停顿");
Expect(Pauses("1 2").SequenceEqual(new[] {0, 100}), "单空格停顿");
Expect(Pauses("1   \t　2").SequenceEqual(new[] {0, 100}), "连续空白合并");
Expect(Pauses("1 \r\n 2").SequenceEqual(new[] {0, 300}), "CRLF与空格不叠加");
Expect(Pauses("1\n\n \n2").SequenceEqual(new[] {0, 300}), "连续空行合并");
Expect(Pauses(" \n1 \n").SequenceEqual(new[] {0}), "忽略首尾空白");
Expect(Pauses("【1 21】").SequenceEqual(new[] {0, 100, 0}), "括号内部空格");
Expect(Pauses("1 【2】 3").SequenceEqual(new[] {0, 100, 100}), "跨括号分组");
Expect(Pauses("1\n【 2】").SequenceEqual(new[] {0, 300}), "跨括号换行优先");
Expect(Pauses("1 # 2").SequenceEqual(new[] {0, 100}), "跨半音标记空格合并");
Expect(ScoreParser.Parse("1 2\n3").All(n => n.PauseBefore(0, 0) == 0), "关闭两种停顿");
Expect(Pauses("1 0\n2").SequenceEqual(new[] {0, 100, 300}), "显式休止保留分隔停顿");
var timeline = new List<int>(); int time = 0;
foreach (var n in ScoreParser.Parse("12 3\n4"))
{ time += n.PauseBefore(100, 300); timeline.Add(time); time += 300; }
Expect(timeline.SequenceEqual(new[] {0, 300, 700, 1300}) && time == 1600, "额外停顿计入绝对时间线");
var rhythm = ScoreParser.Parse("1 2_ 3__ 4. 5_. 6 — — 0_ 0 -", true);
Expect(rhythm.Select(n => n.Beats).SequenceEqual(new[] {1.0, .5, .25, 1.5, .75, 3.0, .5, 2.0}), "完整时值解析");
Expect(rhythm.Count == 8 && rhythm[5].Degree == 6, "延长不生成重复音");
Expect(rhythm[^1].Degree == 0 && rhythm[^1].Beats == 2, "休止延长");
Expect(ScoreParser.Parse("【#1_.】 （2）-", true).Select(n => n.Beats).SequenceEqual(new[] {.75, 2.0}), "音区半音与时值组合");
Expect(ScoreParser.Parse("1 2_ 3_ 5 — | 0 6. 5_ 1 |", true).Sum(n => n.Beats) * 500 == 4000, "120速度八拍为四秒");
foreach (string invalid in new[] {"-1", "_1", "1___", "1..", "1-_", "1._", "1 | -", "#-1", "1-.", "0" + new string('-', 64)})
{
    bool rejected = false;
    try { ScoreParser.Parse(invalid, true); } catch (FormatException) { rejected = true; }
    Expect(rejected, "拒绝非法时值: " + invalid);
}
bool legacyRejects = false;
try { ScoreParser.Parse("1-", false); } catch (FormatException) { legacyRejects = true; }
Expect(legacyRejects, "旧模式不静默忽略节奏标记");
Expect(PlaybackValidation.ValidateGap("20", ScoreParser.Parse("1_", true), 500) == 20,
    "预览与播放共用有效留白校验");
foreach (string invalidGap in new[] { "", "9", "5001" })
{
    bool rejected = false;
    try { PlaybackValidation.ValidateGap(invalidGap, ScoreParser.Parse("1"), 300); }
    catch (FormatException) { rejected = true; }
    Expect(rejected, "预览与播放共用留白范围校验: " + invalidGap);
}
bool shortNoteRejected = false;
try { PlaybackValidation.ValidateGap("20", ScoreParser.Parse("1__", true), 200); }
catch (FormatException) { shortNoteRejected = true; }
Expect(shortNoteRejected, "预览与播放共用最短音校验");
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
    var custom = new AppSettings { Start = new(0x75, 2), Stop = new(0x77, 4), Rhythm = true, Bpm = 90, Gap = 10, Duration = 450, SpaceGap = 80, LineGap = 150 };
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
