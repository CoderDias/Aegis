using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Osint;

namespace Aegis.Infrastructure.External.OsintBrazuca;

public sealed class OsintSourceResolver(IOsintBrazucaCatalog catalog) : IOsintSourceResolver
{
    public IReadOnlyList<OsintSourceDto> SuggestForContext(OsintContext context, int limit = 5)
    {
        var availableInputs = GetAvailableInputs(context);
        if (availableInputs.Count == 0)
        {
            return [];
        }

        return catalog
            .Search(new OsintSearchQuery(Limit: 500))
            .Select(source => (Source: source, Score: ScoreSource(source, context, availableInputs)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Source.Fonte, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 20))
            .Select(x => x.Source)
            .ToList();
    }

    public string BuildUrl(OsintSourceDto source, OsintContext context)
    {
        var url = source.Url;

        if (context.Cnpj is not null && source.Input.Contains("cnpj", StringComparer.OrdinalIgnoreCase))
        {
            if (url.Contains("brasilapi.com.br", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://brasilapi.com.br/docs#tag/CNPJ";
            }

            if (url.Contains("receita.fazenda.gov.br", StringComparison.OrdinalIgnoreCase) &&
                url.Contains("cnpj", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
        }

        if (context.Domain is not null && source.Input.Contains("dominio", StringComparer.OrdinalIgnoreCase))
        {
            if (url.Contains("registro.br", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://registro.br/tecnologia/ferramentas/whois?search={Uri.EscapeDataString(context.Domain)}";
            }
        }

        if (context.Ip is not null && source.Input.Contains("ip", StringComparer.OrdinalIgnoreCase))
        {
            if (url.Contains("{ip}", StringComparison.OrdinalIgnoreCase))
            {
                return url.Replace("{ip}", context.Ip, StringComparison.OrdinalIgnoreCase);
            }
        }

        if (context.Cep is not null && source.Input.Contains("cep", StringComparer.OrdinalIgnoreCase))
        {
            if (url.Contains("viacep.com.br", StringComparison.OrdinalIgnoreCase))
            {
                var cep = new string(context.Cep.Where(char.IsDigit).ToArray());
                return $"https://viacep.com.br/ws/{cep}/json/";
            }
        }

        return url;
    }

    private static HashSet<string> GetAvailableInputs(OsintContext context)
    {
        var inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (context.Cnpj is not null) inputs.Add("cnpj");
        if (context.Domain is not null) inputs.Add("dominio");
        if (context.Ip is not null) inputs.Add("ip");
        if (context.Cep is not null) inputs.Add("cep");
        if (context.Cpf is not null) inputs.Add("cpf");
        if (context.Placa is not null)
        {
            inputs.Add("placa");
            inputs.Add("prefixo_aeronave");
        }

        if (context.Uf is not null) inputs.Add("uf");
        return inputs;
    }

    private static int ScoreSource(
        OsintSourceDto source,
        OsintContext context,
        HashSet<string> availableInputs)
    {
        var score = 0;

        foreach (var input in source.Input)
        {
            if (!availableInputs.Contains(input))
            {
                continue;
            }

            score += 10;
            if (source.Output.Contains("api_json", StringComparer.OrdinalIgnoreCase))
            {
                score += 5;
            }
        }

        if (context.Uf is not null &&
            source.Uf?.Equals(context.Uf, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 8;
        }

        if (source.Output.Contains("rdap", StringComparer.OrdinalIgnoreCase) && context.Domain is not null)
        {
            score += 6;
        }

        return score;
    }
}
