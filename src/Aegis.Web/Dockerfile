FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Aegis.Domain/Aegis.Domain.csproj src/Aegis.Domain/
COPY src/Aegis.Application/Aegis.Application.csproj src/Aegis.Application/
COPY src/Aegis.Infrastructure/Aegis.Infrastructure.csproj src/Aegis.Infrastructure/
COPY src/Aegis.Web/Aegis.Web.csproj src/Aegis.Web/

RUN dotnet restore src/Aegis.Web/Aegis.Web.csproj

COPY src/ src/
RUN cp src/Aegis.Web/appsettings.json.example src/Aegis.Web/appsettings.json
# Publish must restore implicit SDK packages (Microsoft.AspNetCore.App.Internal.Assets)
# that supply _framework/blazor.web.js. --no-restore skips them when restore ran
# before .razor files were copied, causing 404 on blazor.web.js in the container.
RUN dotnet publish src/Aegis.Web/Aegis.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV Database__MigrateOnStartup=true
ENV Database__SeedDemo=false

RUN mkdir -p /app/App_Data /app/logs

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Aegis.Web.dll"]
