using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ApiUser.Telemetry;

public static class OpenTelemetryConfig {

    public const string ActivitySourceName = "api.roll-dice";
    public const string MeterName = "api.roll-dice.Metrics";
    public static void AppOpenTelemetryConfig(this WebApplicationBuilder builder, string serviceName, string serviceVersion)
    {
        var assemblyVersion = serviceVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        // Config por ambiente (com defaults sensatos)
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: assemblyVersion, serviceInstanceId: Environment.MachineName)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object?>
            {
                ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
            });

        builder.Logging.AddOpenTelemetry(o =>
        {
            o.SetResourceBuilder(resourceBuilder);
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
            o.ParseStateValues = true;
            o.AddOtlpExporter();
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("ApiUser", "1.0.0")
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["service.instance.id"] = Environment.MachineName,
                            ["service.version"] = "1.0.0"
                        }))
                    .AddSource("ApiUser.UsersController")
                    .AddSource("ApiUser.UserService")
                    .AddSource("ApiUser.EntityFramework")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            activity.SetTag("http.request.body.size", httpRequest.ContentLength ?? 0);
                            activity.SetTag("http.user_agent", httpRequest.Headers.UserAgent.ToString());
                        };
                        options.EnrichWithHttpResponse = (activity, httpResponse) =>
                        {
                            activity.SetTag("http.response.body.size", httpResponse.ContentLength ?? 0);
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter()
                    .AddOtlpExporter(); // Para exportar para sistemas como Grafana, Prometheus, etc.
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(MeterName)                      // métricas manuais
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter();
            });
    }
}