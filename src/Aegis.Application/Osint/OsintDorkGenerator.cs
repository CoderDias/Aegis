using Aegis.Application.Dtos.Osint;

namespace Aegis.Application.Osint;

public static class OsintDorkGenerator
{
    public static IReadOnlyList<string> Generate(OsintContext context)
    {
        var dorks = new List<string>();

        if (!string.IsNullOrWhiteSpace(context.Domain))
        {
            dorks.Add($"site:{context.Domain}");
            dorks.Add($"\"{context.Domain}\" filetype:pdf");
            dorks.Add($"intext:\"{context.Domain}\" site:gov.br");
        }

        if (!string.IsNullOrWhiteSpace(context.Cnpj))
        {
            dorks.Add($"\"{context.Cnpj}\" site:gov.br");
            dorks.Add($"\"{context.Cnpj}\" filetype:pdf OR filetype:xls");
        }

        if (!string.IsNullOrWhiteSpace(context.Ip))
        {
            dorks.Add($"\"{context.Ip}\" site:cert.br OR site:gov.br");
        }

        if (!string.IsNullOrWhiteSpace(context.Placa))
        {
            dorks.Add($"\"{context.Placa}\" site:anac.gov.br OR site:gov.br");
        }

        if (!string.IsNullOrWhiteSpace(context.Cep))
        {
            dorks.Add($"\"{context.Cep}\" site:correios.com.br OR site:gov.br");
        }

        return dorks.Take(8).ToList();
    }
}

public static class OsintTransparencyLinks
{
    public static IReadOnlyList<(string Label, string Url)> ForCnpj(string cnpj)
    {
        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        if (digits.Length != 14)
        {
            return [];
        }

        return
        [
            ("Portal da Transparência", $"https://portaldatransparencia.gov.br/busca?termo={digits}"),
            ("Compras.gov", $"https://www.gov.br/compras/pt-br/acesso-a-informacao/consulta-detalhada/consulta-detalhada?cnpj={digits}"),
            ("Receita — situação cadastral", "https://solucoes.receita.fazenda.gov.br/servicos/cnpjreva/cnpjreva_solicitacao.asp")
        ];
    }
}

public static class OsintAviationLinks
{
    public static IReadOnlyList<(string Label, string Url)> ForAircraft(string? callsign, string icao24)
    {
        var links = new List<(string Label, string Url)>
        {
            ("RAB / ANAC", "https://ais.cavok.in/rab/"),
            ("ANAC — consultas", "https://www.gov.br/anac/pt-br/servicos/consultas")
        };

        if (!string.IsNullOrWhiteSpace(callsign))
        {
            links.Add(("Busca callsign", $"https://www.google.com/search?q={Uri.EscapeDataString(callsign + " ANAC RAB")}"));
        }

        links.Add(("ICAO24", $"https://www.google.com/search?q={Uri.EscapeDataString(icao24 + " aircraft")}"));
        return links;
    }
}
