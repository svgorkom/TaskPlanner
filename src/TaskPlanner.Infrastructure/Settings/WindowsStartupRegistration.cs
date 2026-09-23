using Microsoft.Win32;
using System.Runtime.Versioning;

namespace TaskPlanner.Infrastructure.Settings;

[SupportedOSPlatform("windows")]
public sealed class WindowsStartupRegistration
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TaskPlanner";

    private readonly string executablePath;

    public WindowsStartupRegistration(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        this.executablePath = executablePath;
    }

    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
        string? value = key?.GetValue(ValueName) as string;

        return string.Equals(value, BuildStartupCommand(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (enabled)
        {
            key.SetValue(ValueName, BuildStartupCommand(), RegistryValueKind.String);
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private string BuildStartupCommand()
    {
        return $"\"{executablePath}\"";
    }
}
