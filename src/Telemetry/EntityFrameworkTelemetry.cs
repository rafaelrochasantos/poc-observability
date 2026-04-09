// Telemetry/EntityFrameworkTelemetry.cs
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApiUser.Telemetry
{
    public static class EntityFrameworkTelemetry
    {
        public static readonly ActivitySource ActivitySource = new("ApiUser.EntityFramework");

        public const string DatabaseOperationActivityName = "ef.database_operation";
        public const string QueryExecutionActivityName = "ef.query_execution";
    }

    public class DatabaseTelemetryInterceptor : DbCommandInterceptor
    {
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            using var activity = EntityFrameworkTelemetry.ActivitySource.StartActivity(
                EntityFrameworkTelemetry.QueryExecutionActivityName);

            activity?.SetTag("db.system", "sqlserver");
            activity?.SetTag("db.operation", GetOperationType(command.CommandText));
            activity?.SetTag("db.statement", command.CommandText);
            activity?.SetTag("db.execution_time_ms", eventData.Duration.TotalMilliseconds);

            var connectionString = eventData.Context?.Database.GetConnectionString();
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                var serverName = ExtractServerName(connectionString);
                activity?.SetTag("db.connection_string.server", serverName);
            }

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override async ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            using var activity = EntityFrameworkTelemetry.ActivitySource.StartActivity(
                EntityFrameworkTelemetry.DatabaseOperationActivityName);

            activity?.SetTag("db.system", "sqlserver");
            activity?.SetTag("db.operation", GetOperationType(command.CommandText));
            activity?.SetTag("db.statement", command.CommandText);
            activity?.SetTag("db.execution_time_ms", eventData.Duration.TotalMilliseconds);
            activity?.SetTag("db.rows_affected", result);

            return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }

        private static string GetOperationType(string commandText)
        {
            var text = commandText.TrimStart().ToUpperInvariant();

            if (text.StartsWith("SELECT")) return "SELECT";
            if (text.StartsWith("INSERT")) return "INSERT";
            if (text.StartsWith("UPDATE")) return "UPDATE";
            if (text.StartsWith("DELETE")) return "DELETE";
            if (text.StartsWith("CREATE")) return "CREATE";
            if (text.StartsWith("ALTER")) return "ALTER";
            if (text.StartsWith("DROP")) return "DROP";

            return "UNKNOWN";
        }

        private static string ExtractServerName(string connectionString)
        {
            try
            {
                var parts = connectionString.Split(';');
                var serverPart = parts.FirstOrDefault(p =>
                    p.TrimStart().StartsWith("Server=", StringComparison.OrdinalIgnoreCase) ||
                    p.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));

                if (serverPart != null)
                {
                    var value = serverPart.Split('=')[1].Trim();
                    return value;
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return "unknown";
        }
    }
}
