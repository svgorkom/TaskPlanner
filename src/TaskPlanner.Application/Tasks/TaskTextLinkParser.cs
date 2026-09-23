using System.Text.RegularExpressions;

namespace TaskPlanner.Application.Tasks;

public sealed record TaskTextSegment(string Text, Uri? Uri)
{
    public bool IsLink => Uri is not null;
}

public static partial class TaskTextLinkParser
{
    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')', ']', '}'];

    public static IReadOnlyList<TaskTextSegment> Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        List<TaskTextSegment> segments = [];
        int currentIndex = 0;

        foreach (Match match in LinkRegex().Matches(text))
        {
            AddTextSegment(segments, text[currentIndex..match.Index]);

            string matchedText = match.Value;
            string linkText = matchedText.TrimEnd(TrailingPunctuation);
            string trailingText = matchedText[linkText.Length..];

            if (Uri.TryCreate(linkText, UriKind.Absolute, out Uri? uri) && IsSupported(uri))
            {
                segments.Add(new TaskTextSegment(linkText, uri));
                AddTextSegment(segments, trailingText);
            }
            else
            {
                AddTextSegment(segments, matchedText);
            }

            currentIndex = match.Index + match.Length;
        }

        AddTextSegment(segments, text[currentIndex..]);

        return segments;
    }

    private static void AddTextSegment(List<TaskTextSegment> segments, string text)
    {
        if (text.Length > 0)
        {
            segments.Add(new TaskTextSegment(text, null));
        }
    }

    private static bool IsSupported(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();
}
