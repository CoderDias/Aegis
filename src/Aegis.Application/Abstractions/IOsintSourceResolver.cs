using Aegis.Application.Dtos.Osint;

namespace Aegis.Application.Abstractions;

public interface IOsintSourceResolver
{
    IReadOnlyList<OsintSourceDto> SuggestForContext(OsintContext context, int limit = 5);

    string BuildUrl(OsintSourceDto source, OsintContext context);
}
