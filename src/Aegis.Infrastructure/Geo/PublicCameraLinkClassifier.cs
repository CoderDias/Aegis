namespace Aegis.Infrastructure.Geo;

public static class PublicCameraLinkClassifier
{
    public static string Classify(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "unknown";
        }

        var lower = url.ToLowerInvariant();

        if (lower.StartsWith("rtsp://", StringComparison.Ordinal) ||
            lower.StartsWith("rtmp://", StringComparison.Ordinal))
        {
            return "stream_direct";
        }

        if (lower.Contains(".m3u8", StringComparison.Ordinal) ||
            lower.Contains(".mp4", StringComparison.Ordinal) ||
            lower.Contains("/mjpg/", StringComparison.Ordinal) ||
            lower.Contains("/mjpeg", StringComparison.Ordinal) ||
            lower.Contains("youtube.com/embed", StringComparison.Ordinal) ||
            lower.Contains("player.vimeo.com", StringComparison.Ordinal))
        {
            return "embed";
        }

        if (lower.EndsWith(".jpg", StringComparison.Ordinal) ||
            lower.EndsWith(".jpeg", StringComparison.Ordinal) ||
            lower.EndsWith(".png", StringComparison.Ordinal) ||
            lower.Contains("snapshot", StringComparison.Ordinal) ||
            lower.Contains("imageproxy", StringComparison.Ordinal))
        {
            return "snapshot";
        }

        if (lower.Contains("/home", StringComparison.Ordinal) ||
            lower.Contains(".aspx", StringComparison.Ordinal) ||
            lower.Contains("rodoviasonline.com.br", StringComparison.Ordinal) ||
            lower.Contains("windy.com", StringComparison.Ordinal) ||
            lower.Contains("citycameras.prefeitura.sp.gov.br", StringComparison.Ordinal) ||
            lower.EndsWith("cameras.cetsp.com.br/", StringComparison.Ordinal) ||
            lower.EndsWith("cameras.cetsp.com.br", StringComparison.Ordinal) ||
            lower.Contains("/cameras/ver/", StringComparison.Ordinal) && !lower.Contains(".m3u8", StringComparison.Ordinal))
        {
            return "portal";
        }

        return "portal";
    }

    public static string DescribeLinkType(string linkType) => linkType switch
    {
        "embed" => "Stream embutível",
        "snapshot" => "Snapshot / imagem",
        "stream_direct" => "Stream direto (abrir externamente)",
        "portal" => "Portal de câmeras",
        _ => "Link externo"
    };

    public static bool SupportsIframe(string linkType) =>
        linkType is "embed" or "snapshot";
}
