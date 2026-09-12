using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace HarmonicaPlayer;

public sealed class HotkeyDialog : Window
{
    private readonly BindingEditor startEditor;
    private readonly BindingEditor stopEditor;
    public HotkeyBinding Start => startEditor.Value;
    public HotkeyBinding Stop => stopEditor.Value;
    public HotkeyDialog(HotkeyBinding start, HotkeyBinding stop)
    {
        Title = "自定义快捷键"; Width = 540; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(18) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "点击录入框后按下快捷键，或用下拉框和勾选项设置。\n修改期间暂停全局快捷键，关闭此窗口后重新注册。", TextWrapping = TextWrapping.Wrap });
        startEditor = new BindingEditor("开始快捷键", start);
        stopEditor = new BindingEditor("停止快捷键", stop);
        panel.Children.Add(startEditor); panel.Children.Add(stopEditor);
        panel.Children.Add(new TextBlock { Text = "停止建议使用容易按到的单键。F12、Win组合及演奏键Z/X/C/V/B/N/M/逗号不支持。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) });
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(error);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var defaults = new Button { Content = "恢复 F6 / F8", Margin = new Thickness(4), Padding = new Thickness(10, 6, 10, 6) };
        var save = new Button { Content = "应用并检查占用", Margin = new Thickness(4), Padding = new Thickness(10, 6, 10, 6) };
        var cancel = new Button { Content = "取消", IsCancel = true, Margin = new Thickness(4), Padding = new Thickness(10, 6, 10, 6) };
        defaults.Click += (_, _) => { startEditor.Set(HotkeyBinding.DefaultStart); stopEditor.Set(HotkeyBinding.DefaultStop); error.Text = ""; };
        save.Click += (_, _) =>
        {
            try { HotkeyBinding.ValidatePair(Start, Stop); DialogResult = true; }
            catch (FormatException e) { error.Text = e.Message; }
        };
        row.Children.Add(defaults); row.Children.Add(save); row.Children.Add(cancel); panel.Children.Add(row);
    }

    private sealed class BindingEditor : StackPanel
    {
        private sealed record Choice(uint Key, string Name);
        private readonly ComboBox key = new() { Width = 100, DisplayMemberPath = "Name" };
        private readonly CheckBox ctrl = new() { Content = "Ctrl", Margin = new Thickness(8, 0, 0, 0) };
        private readonly CheckBox alt = new() { Content = "Alt", Margin = new Thickness(8, 0, 0, 0) };
        private readonly CheckBox shift = new() { Content = "Shift", Margin = new Thickness(8, 0, 0, 0) };
        private readonly TextBox capture = new() { IsReadOnly = true, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8) };
        private readonly TextBlock hint = new() { TextWrapping = TextWrapping.Wrap };
        public HotkeyBinding Value => new((key.SelectedItem as Choice)?.Key ?? 0,
            (ctrl.IsChecked == true ? 2u : 0u) | (alt.IsChecked == true ? 1u : 0u) | (shift.IsChecked == true ? 4u : 0u));
        public BindingEditor(string label, HotkeyBinding binding)
        {
            Margin = new Thickness(0, 12, 0, 0);
            Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.Bold });
            Children.Add(capture);
            var choices = Enumerable.Range(0x70, 11).Concat(Enumerable.Range(0x30, 10))
                .Concat(Enumerable.Range(0x41, 26)).Concat(new[] { 0x1B, 0x20, 0x24, 0x23, 0x21, 0x22, 0x2D, 0x2E });
            key.ItemsSource = choices.Where(k => k is not (0x5A or 0x58 or 0x43 or 0x56 or 0x42 or 0x4E or 0x4D))
                .Select(k => new Choice((uint)k, HotkeyBinding.KeyName((uint)k))).ToArray();
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(key); row.Children.Add(ctrl); row.Children.Add(alt); row.Children.Add(shift);
            Children.Add(row); Children.Add(hint);
            key.SelectionChanged += (_, _) => Refresh();
            foreach (var box in new[] { ctrl, alt, shift })
            { box.Checked += (_, _) => Refresh(); box.Unchecked += (_, _) => Refresh(); }
            capture.GotKeyboardFocus += (_, _) => hint.Text = "现在按下快捷键；如被其他软件拦截，可使用下方选项。";
            capture.PreviewKeyDown += (_, e) =>
            {
                e.Handled = true;
                var pressed = e.Key == Key.System ? e.SystemKey : e.Key;
                if (pressed is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
                var candidate = new HotkeyBinding((uint)KeyInterop.VirtualKeyFromKey(pressed), (uint)Keyboard.Modifiers);
                if (candidate.Error() is string message) { hint.Text = message; return; }
                Set(candidate); hint.Text = "已录入，点击“应用并检查占用”生效。";
            };
            Set(binding);
        }
        public void Set(HotkeyBinding value)
        {
            key.SelectedItem = key.Items.Cast<Choice>().FirstOrDefault(c => c.Key == value.Key);
            ctrl.IsChecked = (value.Modifiers & 2) != 0;
            alt.IsChecked = (value.Modifiers & 1) != 0;
            shift.IsChecked = (value.Modifiers & 4) != 0;
            Refresh();
        }
        private void Refresh() { capture.Text = Value.Label; hint.Text = ""; }
    }
}
