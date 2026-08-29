# Aegis

[![Build](https://img.shields.io/github/actions/workflow/status/CoderDias/Aegis/dotnet-desktop.yml?branch=main&label=build&logo=githubactions&logoColor=white)](https://github.com/CoderDias/Aegis/actions/workflows/dotnet-desktop.yml)
[![Docker Image](https://img.shields.io/github/actions/workflow/status/CoderDias/Aegis/docker-image.yml?branch=main&label=docker%20image&logo=docker&logoColor=white)](https://github.com/CoderDias/Aegis/actions/workflows/docker-image.yml)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![Docker](https://img.shields.io/badge/Docker-ghcr.io-2496ED?logo=docker)](https://github.com/CoderDias/Aegis/pkgs/container/aegis)
[![License](https://img.shields.io/badge/License-MIT-blue)](LICENSE.txt)
[![Release](https://img.shields.io/github/v/release/CoderDias/Aegis)](https://github.com/CoderDias/Aegis/releases)

Plataforma local de inteligência geoespacial e OSINT — mapa interativo, investigações com assets e timeline, rastreamento aéreo (OpenSky), geocodificação, POIs via Overpass, e agregação de feeds (notícias, ransomware, sismos, navios, alertas meteorológicos, hosts). Tudo roda na sua máquina; dados de investigação ficam em SQLite local.

![Aegis — painel operacional](https://i.imgur.com/Hs6SEW2.png)

## O que monitora

| Objeto | Fonte |
|--------|-------|
| Aeronaves | [OpenSky Network](https://opensky-network.org) API (+ fallback ADSB.fi) |
| Navios (AIS) | [AISStream](https://aisstream.io) WebSocket (+ fallback Overpass/OSM) |
| Notícias (geo) | RSS público (G1, Agência Brasil, etc.) + geocodificação Nominatim |
| Ransomware | [Ransomware.live](https://ransomware.live) API |
| Sismos | [USGS](https://earthquake.usgs.gov) earthquake feed |
| Hosts / Shodan | Shodan API, Censys API, descoberta passiva (InternetDB, TCP probe) |
| Edifícios | Overpass API / [OpenStreetMap](https://www.openstreetmap.org) |
| Estradas | Overpass API / OpenStreetMap |
| POIs | Overpass API / OpenStreetMap + catálogo estático (gov.br) |
| Torres rádio | Overpass API / OpenStreetMap |
| Repetidoras | RepeaterBook (BR) + Overpass/OSM |
| ERB / ANATEL | Catálogo estático (dados ANATEL) |
| Câmeras públicas | Catálogo estático (OSINT Brazuca) |
| Portos / ANTAQ | Catálogo estático (ANTAQ / OSINT Brazuca) |
| Alertas meteorológicos | INMET, DWD, JMA, MeteoInfo (RU) |
| INPE / focos 24h | [TerraBrasilis](https://terrabrasilis.dpi.inpe.br) WMS (Queimadas) |
| Mapa de calor | Agregação local das camadas ativas no viewport |

## Como rodar
### Imagem Docker

```bash
docker pull ghcr.io/coderdias/aegis:latest
cp .env.example .env
# edite .env com suas chaves

docker run -d \
  --name aegis \
  -p 8080:8080 \
  -v aegis-data:/app/App_Data \
  --env-file .env \
  ghcr.io/coderdias/aegis:latest
```

Acesse **http://localhost:8080**.

### Docker manual (clone + build)

```bash
git clone https://github.com/CoderDias/Aegis.git
cd Aegis
cp .env.example .env
# edite .env com suas chaves

docker compose up --build
```

Ou, sem Compose:

```bash
docker build -t aegis -f src/Aegis.Web/Dockerfile .
docker run -p 8080:8080 -v aegis-data:/app/App_Data --env-file .env aegis
```

### .NET (sem Docker)

Requer [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/CoderDias/Aegis.git
cd Aegis
dotnet restore Aegis.sln
dotnet run --project src/Aegis.Web --urls http://localhost:5121
```

Acesse **http://localhost:5121**. Na primeira execução, copie e preencha os arquivos `*.example` (`appsettings.json`, `.env`).

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

## Uso ético e isenção de responsabilidade

Aegis agrega dados **públicos** para análise local — pesquisa, estudo, hobby e testes em ambiente controlado. Não foi projetado, vendido ou mantido para vigilância ilegal, perseguição, stalking, assédio, fraude, invasão de sistemas, violação de privacidade ou qualquer atividade contrária à lei.

**Você é o único responsável** pelo uso desta ferramenta, pelas configurações que escolher, pelas chaves de API que inserir, pelos dados que importar e por garantir conformidade com as leis aplicáveis na sua jurisdição (incluindo LGPD, Marco Civil, regulamentação de telecomunicações, termos de uso das APIs integradas etc.).

**Isenção:** o autor e os colaboradores **não se responsabilizam** por danos diretos, indiretos, incidentais, consequenciais ou punitivos decorrentes do uso ou mau uso do software — inclusive quando terceiros utilizarem o projeto de forma ilegal, abusiva ou não autorizada. O software é fornecido **"no estado em que se encontra"**, sem garantias de adequação a um fim específico.

Assets do tipo "Pessoa" são anotações manuais do operador — não há enriquecimento automático de PII. Se não souber se um uso é lícito, **não use**.

## Licença

Projeto de referência / uso local. Verifique licenças das fontes de dados externas antes de redistribuir.

---

*Nota pessoal: este projeto foi quase totalmente feito por IA, sem fins lucrativos, sem motivos — só por diversão e para testar os limites de um agente de IA com prompts simples.*
