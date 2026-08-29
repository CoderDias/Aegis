using System.Text;
using Aegis.Application;
using Aegis.Infrastructure;
using Aegis.Infrastructure.Data;
using Aegis.Web.Components;
using Aegis.Web.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    var builder = WebApplication.CreateBuilder(args);

    var appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
    var logsPath = Path.Combine(builder.Environment.ContentRootPath, "logs");
    Directory.CreateDirectory(appDataPath);
    Directory.CreateDirectory(logsPath);
    builder.Configuration["ConnectionStrings:DefaultConnection"] =
        $"Data Source={Path.Combine(appDataPath, "aegis.db")}";

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddMemoryCache();

    builder.Services.AddScoped<WorkspaceState>();
    builder.Services.AddScoped<UiNotificationService>();
    builder.Services.AddScoped<CircuitFlightFeed>();

    var app = builder.Build();

    await app.Services.InitializeDatabaseAsync();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

    if (!string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_HTTPS_PORT"]))
    {
        app.UseHttpsRedirection();
    }

    app.UseAntiforgery();

    app.MapGet("/healthz", async (AegisDbContext db, CancellationToken ct) =>
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(ct);
            return canConnect
                ? Results.Ok(new { status = "healthy" })
                : Results.StatusCode(503);
        }
        catch
        {
            return Results.StatusCode(503);
        }
    });

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Log.Information("Aegis Web starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aegis Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
