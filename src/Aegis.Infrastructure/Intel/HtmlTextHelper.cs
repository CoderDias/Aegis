using System.Globalization;
using System.Text.RegularExpressions;

namespace Aegis.Infrastructure.Intel;

public static partial class HtmlTextHelper
{
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = ImgTagPattern().Replace(html, string.Empty);
        text = TagPattern().Replace(text, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return WhitespacePattern().Replace(text, " ").Trim();
    }

    public static string? TruncatePlain(string? text, int max) =>
        string.IsNullOrEmpty(text) ? text : text.Length <= max ? text : text[..max] + "…";

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgTagPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
