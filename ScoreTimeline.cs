namespace HarmonicaPlayer;

public sealed record TimedNote(ScoreNote Note, double StartMs, double EndMs, double SoundEndMs, int MidiPitch);

// Local audio and MIDI use the same absolute timeline. Gap is inside the beat,
// never appended. The game's 12ms key preparation is not an audio event.
public sealed class ScoreTimeline
{
    public IReadOnlyList<TimedNote> Notes { get; }
    public int Bpm { get; }
    public double DurationMs => Notes[^1].EndMs;
    private ScoreTimeline(List<TimedNote> notes, int bpm) { Notes = notes.AsReadOnly(); Bpm = bpm; }
    public static ScoreTimeline Create(string body, string bpmText, string gapText)
    {
        int bpm = PlaybackValidation.ParseBpm(bpmText);
        var notes = ScoreParser.Parse(body);
        double beatMs = 60000.0 / bpm;
        int gap = PlaybackValidation.ValidateGap(gapText, notes, beatMs);
        double beats = 0;
        var result = new List<TimedNote>(notes.Count);
        foreach (var note in notes)
        {
            double start = beats * beatMs;
            beats += note.Beats;
            double end = beats * beatMs;
            result.Add(new(note, start, end, note.Degree == 0 ? start : end - gap, Pitch(note)));
        }
        return new(result, bpm);
    }
    public static int Pitch(ScoreNote note) => note.Degree == 0 ? -1 :
        60 + note.Octave * 12 + new[] { 0, 2, 4, 5, 7, 9, 11 }[note.Degree - 1] + (note.Sharp ? 1 : 0);

    // Spaces/bars select the next note; duration symbols/closing brackets select
    // the preceding note. At EOF start at the last note. Group brackets may hold many notes.
    public int IndexAtCursor(string body, int cursor)
    {
        cursor = Math.Clamp(cursor, 0, body.Length);
        for (int i = 0; i < Notes.Count; i++)
        {
            int position = Notes[i].Note.Position;
            if (cursor <= position) return i;
            int next = i + 1 < Notes.Count ? Notes[i + 1].Note.Position : body.Length;
            if (cursor < next && cursor < body.Length && !char.IsWhiteSpace(body[cursor]) &&
                body[cursor] is not ('|' or '｜' or '【' or '（' or '(' or '#')) return i;
        }
        return Notes.Count - 1;
    }
    public int IndexAtTime(double ms)
    {
        int lo = 0, hi = Notes.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (Notes[mid].EndMs <= ms) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
