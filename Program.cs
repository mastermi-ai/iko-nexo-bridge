using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using IkoNexoBridge.Configuration;
using IkoNexoBridge.Services;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting IKO Nexo Bridge...");

    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Configure settings
    builder.Services.Configure<CloudApiSettings>(
        builder.Configuration.GetSection(CloudApiSettings.SectionName));
    builder.Services.Configure<NexoProSettings>(
        builder.Configuration.GetSection(NexoProSettings.SectionName));
    builder.Services.Configure<SyncSettings>(
        builder.Configuration.GetSection(SyncSettings.SectionName));

    // Configure HTTP client with retry policy
    var retryPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    builder.Services.AddHttpClient<CloudApiClient>()
        .AddPolicyHandler(retryPolicy);

    // Register services
    builder.Services.AddSingleton<NexoSferaService>();

    // Register background worker
    builder.Services.AddHostedService<OrderProcessingWorker>();

    // Configure as Windows Service
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "IKO Nexo Bridge";
    });

    var host = builder.Build();

    Log.Information("IKO Nexo Bridge configured successfully");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
