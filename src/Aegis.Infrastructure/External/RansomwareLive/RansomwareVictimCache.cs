using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Intel;
using Microsoft.Extensions.Caching.Memory;

namespace Aegis.Infrastructure.External.RansomwareLive;

public sealed class RansomwareVictimCache(IMemoryCache cache) : IRansomwareVictimCache
{
    public const string CacheKey = "ransomware:victims";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(2);

    public event Action? Updated;

    public IReadOnlyList<RansomwareVictimDto> Get()
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<RansomwareVictimDto>? victims) && victims is not null)
        {
            return victims;
        }

        return [];
    }

    public void Set(IReadOnlyList<RansomwareVictimDto> victims)
    {
        if (victims.Count == 0)
        {
            return;
        }

        cache.Set(CacheKey, victims, CacheTtl);
        Updated?.Invoke();
    }
}
