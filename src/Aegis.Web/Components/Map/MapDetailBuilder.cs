using System.Text.Json;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Dtos.Map;
using Aegis.Application.Flights;
using Aegis.Application.Services;
using Aegis.Infrastructure.External.Overpass;
using Aegis.Infrastructure.Intel;
using Aegis.Web.Services;

namespace Aegis.Web.Components.Map;

public static class MapDetailBuilder
{
    public static async Task<MapDetailViewModel?> BuildMarkerAsync(
        string kind,
        string id,
        MapFeatureDto? feature,
        WorkspaceState workspace,
        FlightQueryService flights,
        IGeoIntelCache geoIntel,
        CircuitFlightFeed flightFeed)
    {
        return kind switch
        {
            "aircraft" => await BuildAircraftAsync(id, flights, flightFeed),
            "shodan" => BuildShodan(id, workspace),
            "news" => BuildNews(id, workspace),
            "ransomware" => BuildRansomware(id, workspace),
            "seismic" => BuildGeoMarker("seismic", id, geoIntel.GetSeismic()),
            "ships" => BuildGeoMarker("ships", id, geoIntel.GetShips()),
            "alerts" => BuildGeoMarker("alerts", id, geoIntel.GetWeatherAlerts()),
            "public_camera" or "erb" or "port" => BuildMapFeature(kind, feature),
            _ when feature is not null => BuildMapFeature(kind, feature),
            _ => null
        };
    }

    public static MapDetailViewModel BuildMapClick(CoordinateDto coord, GeocodeResultDto? geocode)
    {
        if (geocode is not null)
        {
            var parts = geocode.AddressParts is not null
                ? string.Join(", ", geocode.AddressParts.Values.Where(v => !string.IsNullOrWhiteSpace(v)))
                : null;

            return new MapDetailViewModel
            {
                Kind = "geocode",
                Id = geocode.DisplayName,
                Title = geocode.DisplayName,
                Badge = "LOCAL",
                Location = geocode.Coordinate,
                ExternalUrl = geocode.OsmId is not null
                    ? $"https://www.openstreetmap.org/{geocode.Type}/{geocode.OsmId}"
                    : null,
                Fields =
                [
                    new("Endereço", geocode.DisplayName),
                    new("Tipo", geocode.Type ?? "—"),
                    new("Coordenadas", $"{geocode.Coordinate.Lat:F5}, {geocode.Coordinate.Lng:F5}"),
                    new("Componentes", string.IsNullOrWhiteSpace(parts) ? "—" : parts)
                ],
                MetadataJson = JsonSerializer.Serialize(new
                {
                    displayName = geocode.DisplayName,
                    type = geocode.Type,
                    osmId = geocode.OsmId
                })
            };
        }

        return new MapDetailViewModel
        {
            Kind = "coordinate",
            Id = $"{coord.Lat:F5},{coord.Lng:F5}",
            Title = $"{coord.Lat:F5}, {coord.Lng:F5}",
            Badge = "COORDENADA",
            Location = coord,
            Fields =
            [
                new("Latitude", coord.Lat.ToString("F5")),
                new("Longitude", coord.Lng.ToString("F5"))
            ],
            MetadataJson = JsonSerializer.Serialize(new { label = $"{coord.Lat:F5},{coord.Lng:F5}" })
        };
    }

    private static async Task<MapDetailViewModel> BuildAircraftAsync(
        string id,
        FlightQueryService flights,
        CircuitFlightFeed flightFeed)
    {
        var marker = flightFeed.Latest?.Aircraft
                         .FirstOrDefault(a => string.Equals(a.Icao24, id, StringComparison.OrdinalIgnoreCase))
                     ?? await flights.GetByIcaoAsync(id);

        if (marker is null)
        {
            return new MapDetailViewModel
            {
                Kind = "aircraft",
                Id = id,
                Title = id,
                Badge = "VOO",
                Fields = [new("ICAO24", id)]
            };
        }

        var route = await flights.GetRouteAsync(marker);
        var displayName = string.IsNullOrWhiteSpace(marker.Callsign) ? marker.Icao24 : marker.Callsign.Trim();
        var routeNote = route switch
        {
            null => "—",
            { IsEstimated: true } => "Estimada (track ADS-B)",
            _ => "Filed (OpenSky)"
        };

        return new MapDetailViewModel
        {
            Kind = "aircraft",
            Id = marker.Icao24,
            Title = displayName,
            Badge = "VOO",
            Location = new CoordinateDto(marker.Lat, marker.Lng),
            Aircraft = marker,
            Fields =
            [
                new("Callsign", marker.Callsign ?? "—"),
                new("ICAO24", marker.Icao24),
                new("Tipo", FlightCategoryClassifier.ToDisplayName(FlightCategoryClassifier.Classify(marker))),
                new("Origem", route?.Origin?.Label ?? "—"),
                new("Destino", route?.Destination?.Label ?? "—"),
                new("Rota", routeNote),
                new("Altitude", marker.BaroAltitude?.ToString("F0") + " m"),
                new("Velocidade", marker.Velocity?.ToString("F0") + " m/s"),
                new("Proa", marker.Heading?.ToString("F0") + "°"),
                new("País", marker.OriginCountry ?? "—"),
                new("No solo", marker.OnGround ? "Sim" : "Não"),
                new("Último contato", marker.LastContact.ToLocalTime().ToString("g"))
            ]
        };
    }

    private static MapDetailViewModel BuildShodan(string id, WorkspaceState workspace)
    {
        var host = workspace.ShodanRegionCache
            .Concat(workspace.ShodanHosts)
            .FirstOrDefault(h => string.Equals(h.Ip, id, StringComparison.OrdinalIgnoreCase));

        if (host is null)
        {
            return new MapDetailViewModel
            {
                Kind = "shodan",
                Id = id,
                Title = id,
                Badge = "HOST",
                Fields = [new("IP", id)]
            };
        }

        return new MapDetailViewModel
        {
            Kind = "shodan",
            Id = host.Ip,
            Title = $"{host.Ip}:{host.Port}",
            Badge = host.HasExploitableVuln ? "HOST VULN" : "HOST",
            Location = new CoordinateDto(host.Lat, host.Lng),
            Host = host,
            Fields =
            [
                new("IP", host.Ip),
                new("Porta", host.Port.ToString()),
                new("Transporte", host.Transport ?? "—"),
                new("Organização", host.Org ?? "—"),
                new("Produto", host.Product ?? "—"),
                new("Cidade", host.City ?? "—"),
                new("País", host.Country ?? "—"),
                new("Risco", host.HasExploitableVuln ? "Exploit / KEV" : "—")
            ]
        };
    }

    private static MapDetailViewModel BuildNews(string id, WorkspaceState workspace)
    {
        var news = workspace.NewsRegionCache
            .FirstOrDefault(n => string.Equals(n.Id.ToString(), id, StringComparison.OrdinalIgnoreCase));

        if (news is null)
        {
            return new MapDetailViewModel
            {
                Kind = "news",
                Id = id,
                Title = "Notícia",
                Badge = "NOTÍCIA",
                Fields = [new("ID", id)]
            };
        }

        return new MapDetailViewModel
        {
            Kind = "news",
            Id = news.Id.ToString(),
            Title = news.Title,
            Badge = "NOTÍCIA",
            Location = news.Lat is not null && news.Lng is not null
                ? new CoordinateDto(news.Lat.Value, news.Lng.Value)
                : null,
            ExternalUrl = news.Link,
            News = news,
            Fields =
            [
                new("Feed", news.FeedTitle),
                new("Publicado", news.PublishedAt.ToLocalTime().ToString("g")),
                new("Resumo", string.IsNullOrWhiteSpace(news.Summary) ? "—" : HtmlTextHelper.TruncatePlain(HtmlTextHelper.StripHtml(news.Summary), 400))
            ]
        };
    }

    private static MapDetailViewModel BuildRansomware(string id, WorkspaceState workspace)
    {
        var victim = workspace.RansomwareVictims
            .FirstOrDefault(v => string.Equals(v.Url, id, StringComparison.OrdinalIgnoreCase));

        if (victim is null)
        {
            return new MapDetailViewModel
            {
                Kind = "ransomware",
                Id = id,
                Title = "Vítima ransomware",
                Badge = "RANSOMWARE",
                ExternalUrl = id
            };
        }

        return new MapDetailViewModel
        {
            Kind = "ransomware",
            Id = victim.Url,
            Title = victim.Victim,
            Badge = "RANSOMWARE",
            Location = victim.Lat is not null && victim.Lng is not null
                ? new CoordinateDto(victim.Lat.Value, victim.Lng.Value)
                : null,
            ExternalUrl = victim.Url,
            Ransomware = victim,
            Fields =
            [
                new("Grupo", victim.Group),
                new("País", victim.Country ?? "—"),
                new("Setor", victim.Activity ?? "—"),
                new("Domínio", victim.Domain ?? "—"),
                new("Descoberto", victim.DiscoveredAt.ToLocalTime().ToString("g"))
            ]
        };
    }

    private static MapDetailViewModel BuildGeoMarker(string kind, string id, IReadOnlyList<GeoMarkerDto> markers)
    {
        var marker = markers.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        var badge = kind switch
        {
            "ships" => "NAVIO",
            "alerts" => "METEO",
            _ => "SISMO"
        };

        if (marker is null)
        {
            return new MapDetailViewModel
            {
                Kind = kind,
                Id = id,
                Title = badge,
                Badge = badge,
                Fields = [new("ID", id)]
            };
        }

        var fields = new List<MapDetailViewModel.FieldItem>
        {
            new("Info", marker.Subtitle ?? marker.Detail ?? "—"),
            new("Quando", marker.Timestamp?.ToLocalTime().ToString("g") ?? "—")
        };

        if (kind == "alerts")
        {
            fields.InsertRange(0,
            [
                new("Fonte", marker.Source ?? "—"),
                new("Severidade", marker.Severity ?? "—"),
                new("Evento", marker.EventType ?? marker.Subtitle ?? "—"),
                new("Região", marker.Region ?? "—"),
                new("Válido até", marker.ValidUntil?.ToLocalTime().ToString("g") ?? "—"),
                new("Riscos", marker.Risks ?? "—"),
                new("Orientações", marker.Instructions ?? "—")
            ]);
        }

        return new MapDetailViewModel
        {
            Kind = kind,
            Id = marker.Id,
            Title = marker.Title,
            Badge = badge,
            Location = new CoordinateDto(marker.Lat, marker.Lng),
            GeoMarker = marker,
            Fields = fields
        };
    }

    private static MapDetailViewModel BuildMapFeature(string kind, MapFeatureDto? feature)
    {
        if (feature is null)
        {
            return new MapDetailViewModel
            {
                Kind = kind,
                Id = kind,
                Title = kind,
                Badge = "OSM"
            };
        }

        var badge = kind switch
        {
            "public_camera" => "CÂMERA",
            "erb" => "ERB",
            "port" => "PORTO",
            "radio_tower" => "RÁDIO",
            "repeater" => "REPETIDOR",
            _ => "OSM"
        };

        var fields = new List<MapDetailViewModel.FieldItem>
        {
            new("Nome", feature.Name ?? "—"),
            new("Categoria", feature.Category ?? "—"),
            new("ID", $"{feature.OsmType}/{feature.OsmId}"),
            new("Coordenadas", $"{feature.Centroid.Lat:F5}, {feature.Centroid.Lng:F5}")
        };

        if (MapFeatureLayers.IsErb(feature))
        {
            fields.AddRange(
            [
                new("Operadora", feature.Tags.GetValueOrDefault("operator") ?? "—"),
                new("Tecnologia", feature.Tags.GetValueOrDefault("technology") ?? "—"),
                new("Município", feature.Tags.GetValueOrDefault("addr:city") ?? "—"),
                new("UF", feature.Tags.GetValueOrDefault("addr:state") ?? "—")
            ]);
        }

        if (MapFeatureLayers.IsPort(feature))
        {
            fields.Add(new("UF", feature.Tags.GetValueOrDefault("addr:state") ?? "—"));
        }

        if (MapFeatureLayers.IsPublicCamera(feature))
        {
            var linkType = feature.Tags.GetValueOrDefault("link_type");
            fields.Add(new("Tipo de link", feature.Tags.GetValueOrDefault("link_label") ?? linkType ?? "—"));
            if (!string.IsNullOrWhiteSpace(feature.Tags.GetValueOrDefault("description")))
            {
                fields.Add(new("Descrição", feature.Tags.GetValueOrDefault("description")));
            }
        }

        return new MapDetailViewModel
        {
            Kind = kind,
            Id = $"{feature.OsmType}/{feature.OsmId}",
            Title = feature.Name ?? $"{feature.OsmType}/{feature.OsmId}",
            Badge = badge,
            Location = feature.Centroid,
            Feature = feature,
            ExternalUrl = feature.Tags.GetValueOrDefault("url"),
            Fields = fields
        };
    }
}
