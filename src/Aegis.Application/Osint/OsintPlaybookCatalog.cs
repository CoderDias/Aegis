namespace Aegis.Application.Osint;

public sealed record OsintPlaybook(string Id, string Title, string Description, IReadOnlyList<string> Steps);

public static class OsintPlaybookCatalog
{
    public static IReadOnlyList<OsintPlaybook> All { get; } =
    [
        new(
            "cnpj-chain",
            "Cadeia CNPJ",
            "Validar pessoa jurídica e rastrear vínculos públicos.",
            [
                "Confirmar CNPJ ativo (BrasilAPI / Receita)",
                "Geolocalizar sede via CEP",
                "Consultar Portal da Transparência",
                "Verificar Compras.gov e contratos",
                "Buscar processos (link-out CNJ)",
                "Registrar achados na timeline"
            ]),
        new(
            "domain-br",
            "Domínio .br",
            "Mapear registro e infraestrutura de um domínio brasileiro.",
            [
                "Consultar RDAP Registro.br",
                "Resolver nameservers e IPs",
                "Correlacionar hosts Shodan/Censys",
                "Gerar dorks contextuais",
                "Verificar histórico em fontes OSINT Brazuca"
            ]),
        new(
            "person-public",
            "Pessoa / Segurança pública",
            "Fluxo conservador com link-out e minimização de PII.",
            [
                "Documentar base legal no caso",
                "Consultar portal de desaparecidos da UF",
                "Consultar procurados (MJSP / UF)",
                "Evitar scraping de fotos/dados sensíveis",
                "Registrar fontes abertas na timeline"
            ]),
        new(
            "geo-telecom",
            "Geo + Telecom",
            "Correlacionar local com infraestrutura de telecom.",
            [
                "Centralizar viewport no alvo",
                "Ativar camada ERB/ANATEL (zoom ≥ 8)",
                "Comparar com torres OSM e repetidoras",
                "Anotar operadora/tecnologia no caso",
                "Exportar coordenadas relevantes"
            ])
    ];

    public static OsintPlaybook? GetById(string id) =>
        All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
