using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TaskPlanner.Application.Tasks;
using TaskPlanner.App.Diagnostics;

namespace TaskPlanner.App;

public static class LinkTextBlockBehavior
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.RegisterAttached(
        "Segments",
        typeof(IEnumerable<TaskTextSegment>),
        typeof(LinkTextBlockBehavior),
        new PropertyMetadata(null, OnSegmentsChanged));

    public static void SetSegments(DependencyObject element, IEnumerable<TaskTextSegment>? value)
    {
        element.SetValue(SegmentsProperty, value);
    }

    public static IEnumerable<TaskTextSegment>? GetSegments(DependencyObject element)
    {
        return (IEnumerable<TaskTextSegment>?)element.GetValue(SegmentsProperty);
    }

    private static void OnSegmentsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Inlines.Clear();

        if (e.NewValue is not IEnumerable<TaskTextSegment> segments)
        {
            return;
        }

        foreach (TaskTextSegment segment in segments)
        {
            if (segment.Uri is null)
            {
                textBlock.Inlines.Add(new Run(segment.Text));
                continue;
            }

            Hyperlink hyperlink = new(new Run(segment.Text))
            {
                NavigateUri = segment.Uri
            };
            hyperlink.Click += Hyperlink_Click;
            textBlock.Inlines.Add(hyperlink);
        }
    }

    private static void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Hyperlink { NavigateUri: not null } hyperlink)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(hyperlink.NavigateUri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            FileLogger.LogException($"Failed to open task link '{hyperlink.NavigateUri}'.", ex);
        }
    }
}
