using Aegis.Application.Dtos.Osint;

namespace Aegis.Application.Abstractions;

public interface IBrasilApiClient
{
    Task<CnpjLookupDto?> GetCnpjAsync(string cnpj, CancellationToken cancellationToken = default);

    Task<CepLookupDto?> GetCepAsync(string cep, CancellationToken cancellationToken = default);
}
