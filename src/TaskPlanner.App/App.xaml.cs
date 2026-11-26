using System.IO;
using System.Windows;
using System.Windows.Threading;
using TaskPlanner.Application.Abstractions;
using TaskPlanner.Application.Notes;
using TaskPlanner.Application.Pomodoro;
using TaskPlanner.Application.Settings;
using TaskPlanner.Application.Tasks;
using TaskPlanner.Application.Topics;
using TaskPlanner.Infrastructure;
using TaskPlanner.Infrastructure.Persistence;
using TaskPlanner.Infrastructure.Scheduling;
using TaskPlanner.Infrastructure.Settings;
using TaskPlanner.App.Diagnostics;
using TaskPlanner.App.Themes;
using TaskPlanner.App.ViewModels;
using WpfApplication = System.Windows.Application;

namespace TaskPlanner.App;

public partial class App : WpfApplication
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskPlanner");

        FileLogger.Initialize(appDataPath);
        FileLogger.Log("Application starting.");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            FileLogger.Log($"AppDomain unhandled exception (terminating={args.IsTerminating}): {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            FileLogger.LogException("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        IClock clock = new SystemClock();
        ITaskRepository taskRepository = new FileTaskRepository(Path.Combine(appDataPath, "tasks.json"));
        ITopicRepository topicRepository = new FileTopicRepository(Path.Combine(appDataPath, "topics.json"));
        IReminderScheduler reminderScheduler = new InMemoryReminderScheduler();
        INoteRepository noteRepository = new FileNoteRepository(Path.Combine(appDataPath, "notes.json"));
        FileUserSettingsStore settingsStore = new(Path.Combine(appDataPath, "settings.json"));

        TaskService taskService = new(taskRepository, reminderScheduler, clock);
        TopicService topicService = new(topicRepository, taskRepository, clock);
        NoteService noteService = new(noteRepository, clock);
        TaskQueryService taskQueryService = new(taskRepository, clock);
        QuickTaskParser quickTaskParser = new(clock);
        UserSettings settings = await settingsStore.LoadAsync(CancellationToken.None);
        WindowsStartupRegistration startupRegistration = new(Environment.ProcessPath ?? throw new InvalidOperationException("The application executable path is unavailable."));
        bool startWithWindows = startupRegistration.IsEnabled();
        if (settings.StartWithWindows != startWithWindows)
        {
            settings = new UserSettings()
            {
                Activation = settings.Activation,
                Panel = settings.Panel,
                StartWithWindows = startWithWindows,
                Theme = settings.Theme,
                Pomodoro = settings.Pomodoro
            };
            await settingsStore.SaveAsync(settings, CancellationToken.None);
        }

        ThemeManager themeManager = new();
        themeManager.Apply(settings.Theme);

        PomodoroViewModel pomodoroViewModel = new(new PomodoroTimer(clock, settings.Pomodoro));
        pomodoroViewModel.SettingsChanged += async (_, pomodoroSettings) =>
        {
            try
            {
                UserSettings updatedSettings = new()
                {
                    Activation = settings.Activation,
                    Panel = settings.Panel,
                    StartWithWindows = settings.StartWithWindows,
                    Theme = settings.Theme,
                    Pomodoro = pomodoroSettings
                };
                await settingsStore.SaveAsync(updatedSettings, CancellationToken.None);
                settings = updatedSettings;
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Saving pomodoro settings failed", ex);
            }
        };

        MainViewModel viewModel = new(taskService, taskQueryService, quickTaskParser, topicService, settings.Theme, settings.StartWithWindows, pomodoroViewModel, new NotesViewModel(noteService));
        viewModel.ThemeModeChanged += async (_, themeMode) =>
        {
            themeManager.Apply(themeMode);
            UserSettings updatedSettings = new()
            {
                Activation = settings.Activation,
                Panel = settings.Panel,
                StartWithWindows = settings.StartWithWindows,
                Theme = themeMode,
                Pomodoro = settings.Pomodoro
            };
            await settingsStore.SaveAsync(updatedSettings, CancellationToken.None);
            settings = updatedSettings;
        };
        viewModel.StartWithWindowsChanged += async (_, enabled) =>
        {
            try
            {
                startupRegistration.SetEnabled(enabled);
                UserSettings updatedSettings = new()
                {
                    Activation = settings.Activation,
                    Panel = settings.Panel,
                    StartWithWindows = enabled,
                    Theme = settings.Theme,
                    Pomodoro = settings.Pomodoro
                };
                await settingsStore.SaveAsync(updatedSettings, CancellationToken.None);
                settings = updatedSettings;
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Updating Windows startup registration failed", ex);
                viewModel.SetStartWithWindowsSilently(settings.StartWithWindows);
            }
        };

        MainWindow window = new(viewModel, settings);
        MainWindow = window;
        window.Show();
        window.HidePanel();
        FileLogger.Log("Application startup complete; main window created and hidden.");
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        FileLogger.LogException("Dispatcher unhandled exception", e.Exception);

        // Keep the background/tray process alive; the error is logged for diagnosis.
        e.Handled = true;
    }
}
