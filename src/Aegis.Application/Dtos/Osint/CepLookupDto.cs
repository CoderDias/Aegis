namespace Aegis.Application.Dtos.Osint;

public record CepLookupDto(
    string Cep,
    string Logradouro,
    string? Complemento,
    string Bairro,
    string Municipio,
    string Uf,
    string? Ibge,
    double? Lat,
    double? Lng);
