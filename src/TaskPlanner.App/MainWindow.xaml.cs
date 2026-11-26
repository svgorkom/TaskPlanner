using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using TaskPlanner.Application.Pomodoro;
using TaskPlanner.Application.Settings;
using TaskPlanner.App.Diagnostics;
using TaskPlanner.App.ViewModels;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using WpfPoint = System.Windows.Point;

namespace TaskPlanner.App;

public partial class MainWindow : Window
{
    private const int HotkeyId = 7091;
    private const int WhKeyboardLowLevel = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSystemKeyDown = 0x0104;
    private const int WmSystemKeyUp = 0x0105;
    private const int VkControl = 0x11;
    private const int VkLeftControl = 0xA2;
    private const int VkRightControl = 0xA3;
    private const int VkShift = 0x10;
    private const int VkLeftShift = 0xA0;
    private const int VkRightShift = 0xA1;
    private const int VkSpace = 0x20;
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint ModShift = 0x0004;

    private readonly Forms.NotifyIcon notifyIcon;
    private readonly PanelSettings panelSettings;
    private readonly LowLevelKeyboardProc keyboardProc;
    private HwndSource? hwndSource;
    private nint keyboardHook;
    private bool hotkeyRegistered;
    private bool controlKeyDown;
    private bool shiftKeyDown;
    private bool shortcutKeyDown;
    private DateTimeOffset lastShortcutActivation;
    private DateTimeOffset suppressAutoHideUntil;
    private WpfPoint taskDragStartPoint;
    private TaskListItemViewModel? draggedTask;
    private WpfPoint topicDragStartPoint;
    private TopicGroupViewModel? draggedTopic;

    public MainWindow(MainViewModel viewModel, UserSettings settings)
    {
        panelSettings = settings.Panel;
        keyboardProc = KeyboardHookCallback;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.HideRequested += (_, _) => HidePanel();
        viewModel.ExitRequested += (_, _) => ExitApplication();

        notifyIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "Task Planner",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        notifyIcon.DoubleClick += (_, _) => TogglePanel();
        viewModel.Pomodoro.PhaseCompleted += (_, completed) => ShowPomodoroNotification(completed);
        viewModel.Pomodoro.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PomodoroViewModel.TrayText))
            {
                string text = viewModel.Pomodoro.TrayText;
                notifyIcon.Text = text.Length > 63 ? text[..63] : text;
            }
        };
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
    }

    public void HidePanel()
    {
        Hide();
    }

    public void ShowPanel()
    {
        try
        {
            FileLogger.Log("ShowPanel invoked.");
            suppressAutoHideUntil = DateTimeOffset.UtcNow.AddMilliseconds(500);
            ApplyPanelBounds();
            Topmost = panelSettings.AlwaysOnTop;
            Show();
            AnimateIntoView();

            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                () =>
                {
                    try
                    {
                        nint handle = new WindowInteropHelper(this).Handle;
                        ShowWindow(handle, ShowWindowCommand.Restore);
                        SetForegroundWindow(handle);
                        Activate();
                        Focus();
                        Keyboard.Focus(QuickAddBox);
                        FileLogger.Log("ShowPanel foreground activation complete.");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException("ShowPanel foreground activation failed", ex);
                    }
                });
        }
        catch (Exception ex)
        {
            FileLogger.LogException("ShowPanel failed", ex);
        }
    }

    private void ShowPomodoroNotification(PomodoroPhaseCompleted completed)
    {
        (string title, string message) = completed.CompletedPhase == PomodoroPhase.Focus
            ? ("Focus session done", completed.NextPhase == PomodoroPhase.LongBreak
                ? $"{completed.CompletedFocusSessions} pomodoros done. Time for a long break."
                : "Time for a short break.")
            : ("Break is over", "Ready for the next focus session.");

        System.Media.SystemSounds.Asterisk.Play();
        notifyIcon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Info);
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        Forms.ContextMenuStrip menu = new();
        menu.Items.Add("Open", null, (_, _) => ShowPanel());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        return menu;
    }

    private static void ExitApplication()
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void TogglePanel()
    {
        if (IsVisible)
        {
            HidePanel();
            return;
        }

        ShowPanel();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;
        hwndSource = HwndSource.FromHwnd(handle);
        hwndSource?.AddHook(WndProc);
        hotkeyRegistered = RegisterHotKey(
            handle,
            HotkeyId,
            ModControl | ModShift | ModNoRepeat,
            (uint)KeyInterop.VirtualKeyFromKey(Key.Space));

        keyboardHook = SetWindowsHookEx(WhKeyboardLowLevel, keyboardProc, GetModuleHandle(null), 0);

        FileLogger.Log($"SourceInitialized: hotkeyRegistered={hotkeyRegistered}, keyboardHook={keyboardHook != nint.Zero}.");

        if (!hotkeyRegistered)
        {
            Win32Exception exception = new(Marshal.GetLastWin32Error());
            notifyIcon.ShowBalloonTip(
                5000,
                "Task Planner shortcut unavailable",
                $"Ctrl+Shift+Space could not be registered. Exit other Task Planner instances or apps using this shortcut. {exception.Message}",
                Forms.ToolTipIcon.Warning);
        }

        if (keyboardHook == nint.Zero)
        {
            Win32Exception exception = new(Marshal.GetLastWin32Error());
            notifyIcon.ShowBalloonTip(
                5000,
                "Task Planner shortcut fallback unavailable",
                $"The global keyboard listener could not start. {exception.Message}",
                Forms.ToolTipIcon.Error);
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;
        if (hotkeyRegistered)
        {
            UnregisterHotKey(handle, HotkeyId);
        }

        if (keyboardHook != nint.Zero)
        {
            UnhookWindowsHookEx(keyboardHook);
        }

        hwndSource?.RemoveHook(WndProc);
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            FileLogger.Log("WM_HOTKEY received.");
            ActivateFromShortcut();
            handled = true;
        }

        return nint.Zero;
    }

    private nint KeyboardHookCallback(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= 0)
            {
                int message = wParam.ToInt32();
                KbdLlHookStruct keyboardData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                bool isKeyDown = message is WmKeyDown or WmSystemKeyDown;
                bool isKeyUp = message is WmKeyUp or WmSystemKeyUp;

                if (IsControlKey(keyboardData.VirtualKeyCode))
                {
                    controlKeyDown = isKeyDown || (!isKeyUp && controlKeyDown);
                }
                else if (IsShiftKey(keyboardData.VirtualKeyCode))
                {
                    shiftKeyDown = isKeyDown || (!isKeyUp && shiftKeyDown);
                }
                else if (keyboardData.VirtualKeyCode == VkSpace && isKeyDown && !shortcutKeyDown)
                {
                    shortcutKeyDown = true;

                    if (controlKeyDown && shiftKeyDown)
                    {
                        FileLogger.Log("Low-level hook detected Ctrl+Shift+Space.");
                        Dispatcher.BeginInvoke(ActivateFromShortcut);
                    }
                }
                else if (keyboardData.VirtualKeyCode == VkSpace && isKeyUp)
                {
                    shortcutKeyDown = false;
                }
            }
        }
        catch (Exception ex)
        {
            // A managed exception must never escape a native callback boundary.
            FileLogger.LogException("KeyboardHookCallback failed", ex);
        }

        return CallNextHookEx(keyboardHook, code, wParam, lParam);
    }

    private void ActivateFromShortcut()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (now - lastShortcutActivation < TimeSpan.FromMilliseconds(250))
        {
            FileLogger.Log("ActivateFromShortcut debounced.");
            return;
        }

        lastShortcutActivation = now;
        FileLogger.Log("ActivateFromShortcut toggling panel.");
        TogglePanel();
    }

    private static bool IsControlKey(int virtualKey)
    {
        return virtualKey is VkControl or VkLeftControl or VkRightControl;
    }

    private static bool IsShiftKey(int virtualKey)
    {
        return virtualKey is VkShift or VkLeftShift or VkRightShift;
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (panelSettings.AutoHide && DateTimeOffset.UtcNow >= suppressAutoHideUntil)
        {
            HidePanel();
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePanel();
            e.Handled = true;
        }
    }

    private void TaskList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        taskDragStartPoint = e.GetPosition(null);
        draggedTask = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as TaskListItemViewModel;
    }

    private void TaskList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedTask is null)
        {
            return;
        }

        WpfPoint currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - taskDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - taskDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop((DependencyObject)sender, draggedTask, System.Windows.DragDropEffects.Move);
    }

    private async void TaskList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (draggedTask is null ||
            FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is not TaskListItemViewModel targetTask)
        {
            return;
        }

        TaskListItemViewModel sourceTask = draggedTask;
        draggedTask = null;
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.ReorderTasksAsync(sourceTask.Id, targetTask.Id, CancellationToken.None);
        }
    }

    private void TopicHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        topicDragStartPoint = e.GetPosition(null);
        draggedTopic = (sender as FrameworkElement)?.DataContext as TopicGroupViewModel;
    }

    private void TopicHeader_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedTopic?.TopicId is null)
        {
            return;
        }

        WpfPoint currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - topicDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - topicDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop((DependencyObject)sender, draggedTopic, System.Windows.DragDropEffects.Move);
    }

    private async void TopicHeader_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (draggedTopic?.TopicId is not Guid sourceId ||
            (sender as FrameworkElement)?.DataContext is not TopicGroupViewModel targetGroup ||
            targetGroup.TopicId is not Guid targetId)
        {
            draggedTopic = null;
            return;
        }

        draggedTopic = null;
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.ReorderTopicsAsync(sourceId, targetId, CancellationToken.None);
        }
    }

    private async void NoteTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteViewModel note } ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        await viewModel.UpdateNoteAsync(note, CancellationToken.None);
    }

    private static T? FindVisualParent<T>(DependencyObject? child)
            where T : DependencyObject
    {
        DependencyObject? parent = child;
        while (parent is not null)
        {
            if (parent is T match)
            {
                return match;
            }

            parent = parent switch
            {
                FrameworkContentElement frameworkContentElement => frameworkContentElement.Parent ?? LogicalTreeHelper.GetParent(frameworkContentElement),
                ContentElement contentElement => LogicalTreeHelper.GetParent(contentElement),
                Visual or Visual3D => VisualTreeHelper.GetParent(parent),
                _ => LogicalTreeHelper.GetParent(parent)
            };
        }

        return null;
    }

    private void ApplyPanelBounds()
    {
        Rect area = SystemParameters.WorkArea;
        double sizeRatio = Math.Clamp(panelSettings.SizePercent, 10, 90) / 100;

        if (panelSettings.SlideFrom is PanelEdge.Left or PanelEdge.Right)
        {
            Width = area.Width * sizeRatio;
            Height = area.Height;
            Top = area.Top;
            Left = panelSettings.SlideFrom == PanelEdge.Right ? area.Right : area.Left - Width;
            return;
        }

        Width = area.Width;
        Height = area.Height * sizeRatio;
        Left = area.Left;
        Top = panelSettings.SlideFrom == PanelEdge.Bottom ? area.Bottom : area.Top - Height;
    }

    private void AnimateIntoView()
    {
        Rect area = SystemParameters.WorkArea;
        double targetLeft = panelSettings.SlideFrom switch
        {
            PanelEdge.Right => area.Right - Width,
            PanelEdge.Left => area.Left,
            _ => area.Left
        };
        double targetTop = panelSettings.SlideFrom switch
        {
            PanelEdge.Bottom => area.Bottom - Height,
            PanelEdge.Top => area.Top,
            _ => area.Top
        };

        Duration duration = TimeSpan.FromMilliseconds(panelSettings.AnimationMilliseconds);
        BeginAnimation(LeftProperty, new DoubleAnimation(targetLeft, duration));
        BeginAnimation(TopProperty, new DoubleAnimation(targetTop, duration));
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, ShowWindowCommand command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, nint moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hookHandle, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    private delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KbdLlHookStruct
    {
        public readonly int VirtualKeyCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly nuint ExtraInfo;
    }

    private enum ShowWindowCommand
    {
        Restore = 9
    }
}