using Aegis.Application.Abstractions;
using Aegis.Application.Osint;
using Aegis.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aegis.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<InvestigationService>();
        services.AddScoped<AssetService>();
        services.AddScoped<AnnotationService>();
        services.AddScoped<GeofenceService>();
        services.AddScoped<FlightQueryService>();
        services.AddScoped<ViewportQueryService>();
        services.AddScoped<AlertingService>();
        services.AddScoped<RssFeedService>();
        services.AddSingleton<InvestigationExportService>();

        return services;
    }
}
