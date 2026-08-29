namespace Aegis.Infrastructure.External.Censys;

public sealed class CensysOptions
{
    public const string SectionName = "Censys";

    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Organização Censys (obrigatório para search API em planos pagos).</summary>
    public string? OrganizationId { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Limite mensal de chamadas à API Censys (plano free ~250).</summary>
    public int MaxMonthlyQueries { get; set; } = 250;

    public int SearchPageSize { get; set; } = 100;

    /// <summary>IPs amostrados por lote de ingestão CIDR (sem usar quota Censys).</summary>
    public int CidrBatchSize { get; set; } = 24;

    /// <summary>Máximo de lookups Censys por requisição de viewport.</summary>
    public int MaxCensysLookupsPerRequest { get; set; } = 3;

    /// <summary>Revalidar up/down via TCP após N horas (sem chamar Censys).</summary>
    public int ProbeTtlHours { get; set; } = 24;

    public bool AllowSearchApi { get; set; } = true;
}
