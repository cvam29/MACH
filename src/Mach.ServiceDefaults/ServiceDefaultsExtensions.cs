using Azure.Monitor.OpenTelemetry.Exporter;
using Mach.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Mach.ServiceDefaults;

/// <summary>
/// Cross-cutting wiring every function host reuses: observability, resilient HTTP clients,
/// health checks and the application layer.
/// </summary>
public static class ServiceDefaultsExtensions
{
    private const string AppInsightsConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// Registers the application layer plus observability and a default health check.
    /// Call once from each host's <c>Program.cs</c>.
    /// </summary>
    public static IServiceCollection AddServiceDefaults(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplication();
        services.AddObservability(configuration);
        services.AddDefaultHealthChecks();
        return services;
    }

    /// <summary>
    /// Wires OpenTelemetry traces, metrics and logs. Exports to Azure Monitor when an
    /// Application Insights connection string is configured; otherwise to the console.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration[AppInsightsConnectionStringKey];
        var hasAzureMonitor = !string.IsNullOrWhiteSpace(connectionString);
        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? "mach";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddHttpClientInstrumentation();
                if (hasAzureMonitor)
                {
                    tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = connectionString);
                }
                else
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddHttpClientInstrumentation();
                if (hasAzureMonitor)
                {
                    metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = connectionString);
                }
                else
                {
                    metrics.AddConsoleExporter();
                }
            })
            .WithLogging(
                logging =>
                {
                    if (hasAzureMonitor)
                    {
                        logging.AddAzureMonitorLogExporter(o => o.ConnectionString = connectionString);
                    }
                    else
                    {
                        logging.AddConsoleExporter();
                    }
                },
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });

        return services;
    }

    /// <summary>
    /// Registers a named <see cref="System.Net.Http.HttpClient"/> with the standard resilience
    /// handler (per-attempt timeout → retry → circuit breaker → total timeout).
    /// </summary>
    public static IHttpClientBuilder AddResilientHttpClient(
        this IServiceCollection services, string name)
    {
        var clientBuilder = services.AddHttpClient(name);
        clientBuilder.AddStandardResilienceHandler();
        return clientBuilder;
    }

    /// <summary>Registers the default liveness health check ("self").</summary>
    public static IHealthChecksBuilder AddDefaultHealthChecks(this IServiceCollection services)
    {
        return services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
    }
}
