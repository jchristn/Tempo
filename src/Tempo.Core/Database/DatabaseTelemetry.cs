namespace Tempo.Core.Database
{
    using System;
    using System.Data;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Telemetry;

    /// <summary>
    /// Wraps database driver execution with an integration client span and integration metrics.
    /// The span carries only the database system and the bounded operation name (the leading SQL verb);
    /// SQL text is never recorded because statements embed values. Spans are only created inside an
    /// existing trace so background polling does not produce orphan root traces; metrics are always recorded.
    /// This class is stateless and thread-safe.
    /// </summary>
    public static class DatabaseTelemetry
    {
        /// <summary>Operation name used for multi-statement batches.</summary>
        public const string BatchOperation = "batch";

        /// <summary>
        /// Execute a database call under telemetry. Exceptions from <paramref name="execute"/> propagate unchanged.
        /// </summary>
        /// <param name="system">Bounded database system name (sqlite, mysql, postgresql, sqlserver).</param>
        /// <param name="operation">Bounded operation name; see <see cref="OperationOf(string)"/>.</param>
        /// <param name="execute">The database call. Must not be null.</param>
        /// <param name="token">Cancellation token passed through for classification only.</param>
        /// <returns>The result of <paramref name="execute"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="execute"/> is null.</exception>
        public static async Task<DataTable> ExecuteAsync(string system, string operation, Func<Task<DataTable>> execute, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(execute);

            long start = Stopwatch.GetTimestamp();
            using Activity? activity = TempoTelemetry.StartIntegration(system, operation, requireParent: true);
            activity?.SetTag(TelemetryConstants.AttrDbSystem, system);
            activity?.SetTag(TelemetryConstants.AttrDbOperation, operation);

            try
            {
                DataTable result = await execute().ConfigureAwait(false);
                TempoTelemetry.SetOk(activity);
                TempoTelemetry.RecordIntegration(system, operation, TelemetryConstants.OutcomeSuccess, TempoTelemetry.SecondsSince(start));
                return result;
            }
            catch (OperationCanceledException ex) when (token.IsCancellationRequested)
            {
                TempoTelemetry.RecordException(activity, ex);
                TempoTelemetry.RecordIntegration(system, operation, TelemetryConstants.OutcomeCancelled, TempoTelemetry.SecondsSince(start));
                throw;
            }
            catch (Exception ex)
            {
                TempoTelemetry.RecordException(activity, ex);
                TempoTelemetry.RecordIntegration(system, operation, TelemetryConstants.OutcomeException, TempoTelemetry.SecondsSince(start));
                TempoTelemetry.RecordError("database", ex);
                throw;
            }
        }

        /// <summary>
        /// Derive a bounded operation name from a SQL statement: the lowercase leading verb when it is a common
        /// statement type (select, insert, update, delete, create, alter, drop, begin, pragma, with), otherwise <c>other</c>.
        /// </summary>
        /// <param name="query">SQL text. Null or whitespace returns <c>other</c>.</param>
        /// <returns>The operation name.</returns>
        public static string OperationOf(string? query)
        {
            if (String.IsNullOrWhiteSpace(query)) return "other";

            ReadOnlySpan<char> span = query.AsSpan().TrimStart();
            int end = 0;
            while (end < span.Length && Char.IsLetter(span[end])) end++;
            if (end == 0) return "other";

            ReadOnlySpan<char> verb = span.Slice(0, end);
            if (verb.Equals("select", StringComparison.OrdinalIgnoreCase)) return "select";
            if (verb.Equals("insert", StringComparison.OrdinalIgnoreCase)) return "insert";
            if (verb.Equals("update", StringComparison.OrdinalIgnoreCase)) return "update";
            if (verb.Equals("delete", StringComparison.OrdinalIgnoreCase)) return "delete";
            if (verb.Equals("create", StringComparison.OrdinalIgnoreCase)) return "create";
            if (verb.Equals("alter", StringComparison.OrdinalIgnoreCase)) return "alter";
            if (verb.Equals("drop", StringComparison.OrdinalIgnoreCase)) return "drop";
            if (verb.Equals("begin", StringComparison.OrdinalIgnoreCase)) return "begin";
            if (verb.Equals("pragma", StringComparison.OrdinalIgnoreCase)) return "pragma";
            if (verb.Equals("with", StringComparison.OrdinalIgnoreCase)) return "with";
            return "other";
        }
    }
}
