namespace HarmonicaPlayer;

public static class PlaybackValidation
{
    public static int ValidateGap(string text, IReadOnlyCollection<ScoreNote> notes, double noteMs)
    {
        if (!int.TryParse(text, out int silence) || silence < 10 || silence > 5000 ||
            notes.Any(n => n.Degree != 0 && n.Beats * noteMs - silence < 40))
            throw new FormatException("留白范围10～5000毫秒；最短音减去留白后须至少40毫秒。请降低速度或减少留白。");

        return silence;
    }
}
