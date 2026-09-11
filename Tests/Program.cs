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
Console.WriteLine($"PASS: {passed} tests");
