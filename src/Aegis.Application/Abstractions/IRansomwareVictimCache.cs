using Aegis.Application.Dtos.Intel;

namespace Aegis.Application.Abstractions;

public interface IRansomwareVictimCache
{
    event Action? Updated;

    IReadOnlyList<RansomwareVictimDto> Get();

    void Set(IReadOnlyList<RansomwareVictimDto> victims);
}
