namespace Aegis.Application.Dtos.Osint;

public record OsintSourceDto(
    string FonteId,
    string Fonte,
    string Categoria,
    string CategoriaId,
    string Url,
    string? Descricao,
    string? TipoFonte,
    string? Uf,
    IReadOnlyList<string> Input,
    IReadOnlyList<string> Output);

public record OsintSearchQuery(
    string? Text = null,
    string? Categoria = null,
    string? CategoriaId = null,
    string? InputType = null,
    string? OutputType = null,
    string? Uf = null,
    int Limit = 80);

public record OsintContext(
    string? Cnpj = null,
    string? Domain = null,
    string? Ip = null,
    string? Cep = null,
    string? Cpf = null,
    string? Placa = null,
    string? Uf = null);
