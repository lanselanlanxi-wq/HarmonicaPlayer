using System.Windows;
using Microsoft.Win32;

namespace HarmonicaPlayer;

// Injectable only for UI tests; production uses standard Windows dialogs.
public interface IScoreDialogs
{
    string? Open(Window owner);
    string? Save(Window owner, string suggestedName);
    MessageBoxResult Unsaved(Window owner);
}

public sealed class ScoreDialogs : IScoreDialogs
{
    public string? Open(Window owner)
    {
        var dialog = new OpenFileDialog { Filter = "TXT 简谱|*.txt" };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
    public string? Save(Window owner, string suggestedName)
    {
        var dialog = new SaveFileDialog { Filter = "TXT 简谱|*.txt", DefaultExt = ".txt", AddExtension = true,
            OverwritePrompt = true, FileName = suggestedName };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
    public MessageBoxResult Unsaved(Window owner) => MessageBox.Show(owner,
        "曲谱、曲名、BPM或音符间隔已修改，是否保存？\n“否”放弃本次修改，“取消”返回编辑。",
        "曲谱尚未保存", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
}
