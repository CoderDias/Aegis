using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Aegis.Infrastructure.Intel;

public static partial class RssXmlHelper
{
    public const string FeedUserAgent =
        "Mozilla/5.0 (compatible; Aegis-OSINT/1.0; +https://github.com/)";

    public static void EnsureEncodingsRegistered() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static async Task<string> ReadContentAsStringAsync(
        HttpContent content,
        CancellationToken cancellationToken = default)
    {
        var bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        bytes = StripByteOrderMark(bytes);
        var charset = content.Headers.ContentType?.CharSet;
        var encoding = ResolveEncoding(charset);
        return TrimXmlPreamble(encoding.GetString(bytes));
    }

    private static byte[] StripByteOrderMark(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return bytes[3..];
        }

        return bytes;
    }

    private static string TrimXmlPreamble(string text)
    {
        var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        var xmlIndex = trimmed.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        if (xmlIndex < 0)
        {
            xmlIndex = trimmed.IndexOf("<rss", StringComparison.OrdinalIgnoreCase);
        }

        if (xmlIndex < 0)
        {
            xmlIndex = trimmed.IndexOf("<feed", StringComparison.OrdinalIgnoreCase);
        }

        return xmlIndex > 0 ? trimmed[xmlIndex..] : trimmed;
    }

    public static XDocument ParseFeedDocument(string text)
    {
        text = TrimXmlPreamble(text);

        if (IsHtmlResponse(text))
        {
            throw new InvalidOperationException("Feed URL returned HTML instead of RSS/Atom XML.");
        }

        var sanitized = SanitizeXml(text);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            CheckCharacters = false
        };

        using var reader = XmlReader.Create(new StringReader(sanitized), settings);
        return XDocument.Load(reader);
    }

    public static bool IsHtmlResponse(string text)
    {
        var trimmed = text.AsSpan().TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeXml(string xml) =>
        InvalidAmpersandRegex().Replace(xml, "&amp;");

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim().Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    [GeneratedRegex("&(?!(?:amp|lt|gt|quot|apos|#\\d+|#x[\\da-fA-F]+);)", RegexOptions.Compiled)]
    private static partial Regex InvalidAmpersandRegex();
}
