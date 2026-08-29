using Aegis.Application.Dtos.Osint;

namespace Aegis.Application.Abstractions;

public interface IOsintBrazucaCatalog
{
    int TotalCount { get; }

    IReadOnlyList<OsintSourceDto> GetAllSources();

    IReadOnlyList<string> GetCategories();

    IReadOnlyList<string> GetInputTypes();

    IReadOnlyList<OsintSourceDto> Search(OsintSearchQuery query);

    OsintSourceDto? GetById(string fonteId);
}
