namespace Aegis.Application.Dtos.Osint;

public record CnpjLookupDto(
    string Cnpj,
    string RazaoSocial,
    string? NomeFantasia,
    string? SituacaoCadastral,
    string? Logradouro,
    string? Numero,
    string? Bairro,
    string? Municipio,
    string? Uf,
    string? Cep,
    double? Lat,
    double? Lng);
