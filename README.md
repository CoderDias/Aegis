# Aegis (OsirisV2)

Plataforma local de inteligência geoespacial e OSINT, inspirada no estilo operacional Palantir Gotham/Foundry. O repositório chama-se **OsirisV2**; o produto chama-se **Aegis**.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Conexão com internet para tiles de mapa, OpenSky, Nominatim e Overpass (investigações e SQLite funcionam offline)
- Docker Desktop (opcional, para subir via Compose)

## Primeira configuração

Os arquivos com secrets **não vão para o git**. Use os templates:

```powershell
# Windows (PowerShell)
Copy-Item src/Aegis.Web/appsettings.json.example src/Aegis.Web/appsettings.json
Copy-Item src/Aegis.Web/appsettings.Development.json.example src/Aegis.Web/appsettings.Development.json
Copy-Item .env.example .env
```

```bash
# Linux / macOS
cp src/Aegis.Web/appsettings.json.example src/Aegis.Web/appsettings.json
cp src/Aegis.Web/appsettings.Development.json.example src/Aegis.Web/appsettings.Development.json
cp .env.example .env
```

Edite `src/Aegis.Web/appsettings.json` e preencha os placeholders:

| Chave | Descrição |
|-------|-----------|
| `OpenSky:ClientId` / `ClientSecret` | OAuth OpenSky v1.1 |
| `AirStream:ApiToken` | Token ADSB.fi (fallback de voos) |
| `Shodan:ApiKey` | Opcional — hosts enriquecidos |
| `Censys:ApiToken` | Opcional — descoberta de hosts |
| `GeoIntel:AisStreamApiKey` | Opcional — navios via AISStream |
| `Nominatim:UserAgent` | **Obrigatório** — identifique sua instalação |

Alternativa: [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) ou variáveis de ambiente (`OpenSky__ClientSecret`, etc.).

## Execução local (.NET)

```bash
dotnet restore Aegis.sln
dotnet run --project src/Aegis.Web --urls http://localhost:5121
```

Abra **http://localhost:5121**.

### Banco de dados

O SQLite é criado automaticamente em `src/Aegis.Web/App_Data/aegis.db` na primeira execução (`Database:MigrateOnStartup=true`).

Com `appsettings.Development.json`, `Database:SeedDemo=true` cria investigações demo (**Operação Alpha** e **Corredor Aéreo — Demo**).

## Docker Compose

```bash
cp .env.example .env
# edite .env com suas chaves

docker compose up --build
```

Acesse **http://localhost:8080**. Dados persistem no volume `aegis-data`.

Build manual (sem Compose):

```bash
docker build -t aegis -f src/Aegis.Web/Dockerfile .
docker run -p 8080:8080 -v aegis-data:/app/App_Data --env-file .env aegis
```

## Configuração

Referência completa em `src/Aegis.Web/appsettings.json.example`:

| Seção | Descrição |
|-------|-----------|
| `ConnectionStrings:DefaultConnection` | Caminho do SQLite |
| `OpenSky` | URL, intervalo de polling (15s padrão), credenciais OAuth |
| `Nominatim` | URL e User-Agent **obrigatório** |
| `OpenStreetMap` | Tiles Carto Dark + fallback OSM |
| `Map` | Centro padrão (Brasil), zoom min/max |
| `Overpass` | Instância e limites de área |
| `Flights` | Retenção de tracks (7 dias), máx. markers |
| `RegionalPrefetch` | Cache regional de hosts/OSM em background |

## Funcionalidades

- **Mapa interativo** — Leaflet, tema escuro, modal unificado no clique, adicionar ao caso
- **Rastreamento aéreo** — OpenSky Network, filtros, histórico local
- **Geocodificação** — Nominatim (busca + reverse), cache 7 dias
- **POIs/edifícios** — Overpass API em zoom alto
- **Investigações** — assets, anotações, timeline, geofences
- **Intel multi-fonte** — notícias RSS, ransomware, sismos, navios, alertas meteorológicos, hosts

## Arquitetura

```
Aegis.Web          → Blazor Server (UI)
Aegis.Application  → Casos de uso, DTOs
Aegis.Domain       → Entidades, value objects, regras
Aegis.Infrastructure → EF Core, HTTP clients, background jobs
```

## Testes

```bash
dotnet test Aegis.sln
```

## Limites das APIs gratuitas

| Fonte | Limite | Boas práticas |
|-------|--------|---------------|
| **OpenSky** (anônimo) | ~1 req/10s por IP | Polling 15s, só viewport visível |
| **Nominatim** | 1 req/s | User-Agent identificável, cache local |
| **Overpass** | Instâncias públicas compartilhadas | Throttle, bbox pequeno, zoom ≥ 14 |
| **Tiles OSM/Carto** | Uso moderado | Attribution visível no mapa |

## Atribuições

- Map tiles: © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors, © [CARTO](https://carto.com/attributions)
- Dados aéreos: [OpenSky Network](https://opensky-network.org)
- Geocoding: [Nominatim](https://nominatim.org) / OSM Foundation

## Uso ético

Aegis agrega dados **públicos** para análise local. Não use para vigilância ilegal, crime ou targeting de pessoas. Assets do tipo "Pessoa" são anotações manuais do operador — não há enriquecimento automático de PII.

## Licença

Projeto de referência / uso local. Verifique licenças das fontes de dados externas antes de redistribuir.
