using System.Text.RegularExpressions;

namespace Aegis.Infrastructure.Intel;

public static partial class NewsPlaceExtractor
{
    private static readonly (string Place, string Query)[] Places =
    [
        ("Rio de Janeiro", "Rio de Janeiro, Brasil"),
        ("São Paulo", "São Paulo, Brasil"),
        ("Belo Horizonte", "Belo Horizonte, Brasil"),
        ("Porto Alegre", "Porto Alegre, Brasil"),
        ("Brasília", "Brasília, Brasil"),
        ("Salvador", "Salvador, Brasil"),
        ("Fortaleza", "Fortaleza, Brasil"),
        ("Recife", "Recife, Brasil"),
        ("Curitiba", "Curitiba, Brasil"),
        ("Manaus", "Manaus, Brasil"),
        ("Belém", "Belém, Brasil"),
        ("Goiânia", "Goiânia, Brasil"),
        ("Campinas", "Campinas, Brasil"),
        ("Florianópolis", "Florianópolis, Brasil"),
        ("Vitória", "Vitória, Brasil"),
        ("Natal", "Natal, Brasil"),
        ("João Pessoa", "João Pessoa, Brasil"),
        ("Maceió", "Maceió, Brasil"),
        ("Teresina", "Teresina, Brasil"),
        ("São Luís", "São Luís, Brasil"),
        ("Campo Grande", "Campo Grande, Brasil"),
        ("Cuiabá", "Cuiabá, Brasil"),
        ("Palmas", "Palmas, Brasil"),
        ("Macapá", "Macapá, Brasil"),
        ("Boa Vista", "Boa Vista, Brasil"),
        ("Porto Velho", "Porto Velho, Brasil"),
        ("Rio Branco", "Rio Branco, Brasil"),
        ("Aracaju", "Aracaju, Brasil"),
        ("Minas Gerais", "Minas Gerais, Brasil"),
        ("Bahia", "Bahia, Brasil"),
        ("Paraná", "Paraná, Brasil"),
        ("Rio Grande do Sul", "Rio Grande do Sul, Brasil"),
        ("Pernambuco", "Pernambuco, Brasil"),
        ("Ceará", "Ceará, Brasil"),
        ("Pará", "Pará, Brasil"),
        ("Goiás", "Goiás, Brasil"),
        ("Maranhão", "Maranhão, Brasil"),
        ("Amazonas", "Amazonas, Brasil"),
        ("Espírito Santo", "Espírito Santo, Brasil"),
        ("Paraíba", "Paraíba, Brasil"),
        ("Mato Grosso", "Mato Grosso, Brasil"),
        ("Mato Grosso do Sul", "Mato Grosso do Sul, Brasil"),
        ("Rondônia", "Rondônia, Brasil"),
        ("Roraima", "Roraima, Brasil"),
        ("Acre", "Acre, Brasil"),
        ("Amapá", "Amapá, Brasil"),
        ("Tocantins", "Tocantins, Brasil"),
        ("Distrito Federal", "Brasília, Brasil"),
        ("DF", "Brasília, Brasil"),
        ("Moscow", "Moscow, Russia"),
        ("Moskva", "Moscow, Russia"),
        ("Москва", "Moscow, Russia"),
        ("Washington", "Washington, DC, USA"),
        ("Kyiv", "Kyiv, Ukraine"),
        ("Kiev", "Kyiv, Ukraine"),
        ("Beijing", "Beijing, China"),
        ("Tokyo", "Tokyo, Japan"),
        ("London", "London, United Kingdom"),
        ("Paris", "Paris, France"),
        ("Berlin", "Berlin, Germany"),
        ("Jerusalem", "Jerusalem, Israel"),
        ("Tel Aviv", "Tel Aviv, Israel"),
        ("Gaza", "Gaza, Palestine"),
        ("Beirut", "Beirut, Lebanon"),
        ("Damascus", "Damascus, Syria"),
        ("Baghdad", "Baghdad, Iraq"),
        ("Tehran", "Tehran, Iran"),
        ("Riyadh", "Riyadh, Saudi Arabia"),
        ("Dubai", "Dubai, United Arab Emirates"),
        ("Delhi", "New Delhi, India"),
        ("New Delhi", "New Delhi, India"),
        ("Islamabad", "Islamabad, Pakistan"),
        ("Kabul", "Kabul, Afghanistan"),
        ("Seoul", "Seoul, South Korea"),
        ("Taipei", "Taipei, Taiwan"),
        ("Hong Kong", "Hong Kong"),
        ("Singapore", "Singapore"),
        ("Sydney", "Sydney, Australia"),
        ("Canberra", "Canberra, Australia"),
        ("Ottawa", "Ottawa, Canada"),
        ("Mexico City", "Mexico City, Mexico"),
        ("Buenos Aires", "Buenos Aires, Argentina"),
        ("Santiago", "Santiago, Chile"),
        ("Bogotá", "Bogotá, Colombia"),
        ("Lima", "Lima, Peru"),
        ("Caracas", "Caracas, Venezuela"),
        ("Cairo", "Cairo, Egypt"),
        ("Nairobi", "Nairobi, Kenya"),
        ("Addis Ababa", "Addis Ababa, Ethiopia"),
        ("Pretoria", "Pretoria, South Africa"),
        ("Johannesburg", "Johannesburg, South Africa"),
        ("Brussels", "Brussels, Belgium"),
        ("Warsaw", "Warsaw, Poland"),
        ("St. Petersburg", "Saint Petersburg, Russia"),
        ("Saint Petersburg", "Saint Petersburg, Russia"),
        ("Vladimir Putin", "Moscow, Russia"),
        ("Ukraine", "Kyiv, Ukraine"),
        ("Russia", "Moscow, Russia"),
        ("China", "Beijing, China"),
        ("Israel", "Jerusalem, Israel"),
        ("Gaza Strip", "Gaza, Palestine"),
    ];

    public static string? ExtractGeocodeQuery(string title, string? summary)
    {
        var text = $"{title} {summary}".Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var (place, query) in Places.OrderByDescending(p => p.Place.Length))
        {
            if (text.Contains(place, StringComparison.OrdinalIgnoreCase))
            {
                return query;
            }
        }

        var match = EmCityPattern().Match(text);
        if (match.Success)
        {
            var city = match.Groups["city"].Value.Trim();
            if (city.Length >= 3)
            {
                return $"{city}, Brasil";
            }
        }

        match = NoCityPattern().Match(text);
        if (match.Success)
        {
            var city = match.Groups["city"].Value.Trim();
            if (city.Length >= 3)
            {
                return $"{city}, Brasil";
            }
        }

        return null;
    }

    [GeneratedRegex(@"\bem\s+(?<city>[A-ZÁÀÂÃÉÈÊÍÏÓÔÕÖÚÇÑ][a-záàâãéèêíïóôõöúçñ\s\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EmCityPattern();

    [GeneratedRegex(@"\bno\s+(?<city>[A-ZÁÀÂÃÉÈÊÍÏÓÔÕÖÚÇÑ][a-záàâãéèêíïóôõöúçñ\s\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex NoCityPattern();
}
