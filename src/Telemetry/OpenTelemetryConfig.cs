using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ApiUser.Telemetry;

public static class OpenTelemetryConfig
{
    public const string ActivitySourceName = "ApiUser";
    public const string MeterName = "ApiUser.Metrics";

    public static void AppOpenTelemetryConfig(this WebApplicationBuilder builder, string serviceName, string serviceVersion)
    {
        var assemblyVersion = serviceVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: assemblyVersion, serviceInstanceId: Environment.MachineName)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
            });

        if (IsAutoInstrumentationEnabled())
        {
            return;
        }

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
            options.AddOtlpExporter();
        });

        builder.Services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
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
                    .AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter();
            });
    }

    private static bool IsAutoInstrumentationEnabled()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_DOTNET_AUTO_HOME")))
        {
            return true;
        }

        var startupHooks = Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS");
        if (!string.IsNullOrWhiteSpace(startupHooks) &&
            startupHooks.Contains("OpenTelemetry.AutoInstrumentation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var profilerEnabled = Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING");
        var profilerId = Environment.GetEnvironmentVariable("CORECLR_PROFILER");

        return profilerEnabled == "1" &&
               string.Equals(
                   profilerId,
                   "{918728DD-259F-4A6A-AC2B-B85E1B658318}",
                   StringComparison.OrdinalIgnoreCase);
    }
}
