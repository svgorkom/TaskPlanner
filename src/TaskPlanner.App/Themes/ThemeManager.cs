using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using AppThemeMode = TaskPlanner.Application.Settings.ThemeMode;
using WpfApplication = System.Windows.Application;

namespace TaskPlanner.App.Themes;

public sealed class ThemeManager
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    private static readonly Uri LightThemeUri = new("Themes/Light.xaml", UriKind.Relative);
    private static readonly Uri DarkThemeUri = new("Themes/Dark.xaml", UriKind.Relative);

    public void Apply(AppThemeMode themeMode)
    {
        AppThemeMode effectiveThemeMode = themeMode == AppThemeMode.System ? GetSystemThemeMode() : themeMode;
        Uri themeUri = effectiveThemeMode == AppThemeMode.Dark ? DarkThemeUri : LightThemeUri;

        Collection<ResourceDictionary> mergedDictionaries = WpfApplication.Current.Resources.MergedDictionaries;

        for (int index = mergedDictionaries.Count - 1; index >= 0; index--)
        {
            ResourceDictionary dictionary = mergedDictionaries.ElementAt(index);

            if (dictionary.Source is null)
            {
                continue;
            }

            string source = dictionary.Source.ToString();

            if (source.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                source.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase))
            {
                mergedDictionaries.Remove(dictionary);
            }
        }

        mergedDictionaries.Insert(0, new ResourceDictionary { Source = themeUri });
    }

    private static AppThemeMode GetSystemThemeMode()
    {
        object? value = Registry.CurrentUser
            .OpenSubKey(PersonalizeRegistryPath)
            ?.GetValue(AppsUseLightThemeValueName);

        return value is int appsUseLightTheme && appsUseLightTheme == 0
            ? AppThemeMode.Dark
            : AppThemeMode.Light;
    }
}
