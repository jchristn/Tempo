namespace Tempo.Core.Database.Common.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>Driver-agnostic implementation of <see cref="IRequestHistoryMethods"/>.</summary>
    public class RequestHistoryMethods : IRequestHistoryMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public RequestHistoryMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task CreateAsync(RequestHistoryEntry e, CancellationToken token = default)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (string.IsNullOrWhiteSpace(e.Id)) e.Id = IdGenerator.GenerateRequestHistoryId();
            string reqHeaders = JsonSerializer.Serialize(e.RequestHeaders);
            string resHeaders = JsonSerializer.Serialize(e.ResponseHeaders);
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO request_history(id, tenant_id, user_id, principal_name, method, path, url, status_code, duration_ms, source_ip, " +
                "request_headers, request_body, request_body_bytes, request_body_truncated, " +
                "response_headers, response_body, response_body_bytes, response_body_truncated, created_utc, completed_utc) VALUES (" +
                _D.Quote(e.Id) + ", " + _D.Quote(e.TenantId) + ", " + _D.Quote(e.UserId) + ", " + _D.Quote(e.PrincipalName) + ", " +
                _D.Quote(e.Method) + ", " + _D.Quote(e.Path) + ", " + _D.Quote(e.Url) + ", " +
                e.StatusCode + ", " + e.DurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + _D.Quote(e.SourceIp) + ", " +
                _D.Quote(reqHeaders) + ", " + _D.Quote(e.RequestBody) + ", " + e.RequestBodyBytes + ", " + _D.Bit(e.RequestBodyTruncated) + ", " +
                _D.Quote(resHeaders) + ", " + _D.Quote(e.ResponseBody) + ", " + e.ResponseBodyBytes + ", " + _D.Bit(e.ResponseBodyTruncated) + ", " +
                _D.Quote(e.CreatedUtc) + ", " + _D.Quote(e.CompletedUtc) + ");",
                false, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string where = "id = " + _D.Quote(id);
            if (!string.IsNullOrEmpty(tenantId)) where = "tenant_id = " + _D.Quote(tenantId) + " AND " + where;
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM request_history WHERE " + where + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string where = BuildWhere(filter);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM request_history" + where + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT id, tenant_id, user_id, principal_name, method, path, url, status_code, duration_ms, source_ip, " +
                "request_body_bytes, request_body_truncated, response_body_bytes, response_body_truncated, created_utc, completed_utc " +
                "FROM request_history" + where + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<RequestHistoryEntry> r = new EnumerationResult<RequestHistoryEntry> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(MapSummary(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (!filter.FromUtc.HasValue || !filter.ToUtc.HasValue) throw new ArgumentException("FromUtc and ToUtc are required for summary.");
            string where = BuildWhere(filter);
            DataTable totalTable = await _Driver.ExecuteQueryAsync(
                "SELECT COUNT(*) AS total, " +
                "SUM(CASE WHEN status_code >= 200 AND status_code < 400 THEN 1 ELSE 0 END) AS succ, " +
                "SUM(CASE WHEN status_code >= 400 THEN 1 ELSE 0 END) AS fail, " +
                "AVG(duration_ms) AS avg_dur FROM request_history" + where + ";",
                false, token).ConfigureAwait(false);

            RequestHistorySummary summary = new RequestHistorySummary();
            if (totalTable.Rows.Count > 0)
            {
                summary.TotalCount = Converters.Int(totalTable.Rows[0], "total");
                summary.TotalSuccess = Converters.Int(totalTable.Rows[0], "succ");
                summary.TotalFailure = Converters.Int(totalTable.Rows[0], "fail");
                summary.AverageDurationMs = Converters.Double(totalTable.Rows[0], "avg_dur");
            }

            DateTime from = filter.FromUtc!.Value.ToUniversalTime();
            DateTime to = filter.ToUtc!.Value.ToUniversalTime();
            int bucketMinutes = filter.BucketMinutes;
            DataTable rows = await _Driver.ExecuteQueryAsync(
                "SELECT status_code, duration_ms, created_utc FROM request_history" + where + ";",
                false, token).ConfigureAwait(false);

            List<RequestHistoryBucket> buckets = new List<RequestHistoryBucket>();
            DateTime cursor = from;
            while (cursor < to)
            {
                DateTime end = cursor.AddMinutes(bucketMinutes);
                if (end > to) end = to;
                buckets.Add(new RequestHistoryBucket { BucketStartUtc = cursor, BucketEndUtc = end });
                cursor = end;
            }

            foreach (DataRow row in rows.Rows)
            {
                DateTime created = Converters.DateTime(row, "created_utc");
                int status = Converters.Int(row, "status_code");
                double duration = Converters.Double(row, "duration_ms");
                int index = (int)Math.Floor((created - from).TotalMinutes / bucketMinutes);
                if (index < 0 || index >= buckets.Count) continue;
                RequestHistoryBucket b = buckets[index];
                if (status >= 200 && status < 400) b.SuccessCount++;
                else if (status >= 400) b.FailureCount++;
                b.AverageDurationMs += duration;
            }
            foreach (RequestHistoryBucket b in buckets)
            {
                int totalBucket = b.SuccessCount + b.FailureCount;
                b.AverageDurationMs = totalBucket > 0 ? b.AverageDurationMs / totalBucket : 0;
            }
            summary.Buckets = buckets;
            return summary;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string where = "id = " + _D.Quote(id);
            if (!string.IsNullOrEmpty(tenantId)) where = "tenant_id = " + _D.Quote(tenantId) + " AND " + where;
            await _Driver.ExecuteQueryAsync("DELETE FROM request_history WHERE " + where + ";", false, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc/>
        public async Task<int> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string where = BuildWhere(filter);
            DataTable before = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM request_history" + where + ";", false, token).ConfigureAwait(false);
            int count = before.Rows.Count == 0 ? 0 : Convert.ToInt32(before.Rows[0][0]);
            await _Driver.ExecuteQueryAsync("DELETE FROM request_history" + where + ";", false, token).ConfigureAwait(false);
            return count;
        }

        /// <inheritdoc/>
        public async Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            DataTable before = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM request_history WHERE created_utc < " + _D.Quote(olderThanUtc) + ";", false, token).ConfigureAwait(false);
            int count = before.Rows.Count == 0 ? 0 : Convert.ToInt32(before.Rows[0][0]);
            await _Driver.ExecuteQueryAsync("DELETE FROM request_history WHERE created_utc < " + _D.Quote(olderThanUtc) + ";", false, token).ConfigureAwait(false);
            return count;
        }

        private string BuildWhere(RequestHistoryFilter filter)
        {
            List<string> clauses = new List<string>();
            if (!string.IsNullOrEmpty(filter.TenantId)) clauses.Add("tenant_id = " + _D.Quote(filter.TenantId));
            if (!string.IsNullOrEmpty(filter.UserId)) clauses.Add("user_id = " + _D.Quote(filter.UserId));
            if (!string.IsNullOrEmpty(filter.Method)) clauses.Add("method = " + _D.Quote(filter.Method.ToUpperInvariant()));
            if (filter.StatusCode.HasValue) clauses.Add("status_code = " + filter.StatusCode.Value);
            if (!string.IsNullOrEmpty(filter.PathContains)) clauses.Add("path LIKE " + _D.Quote("%" + filter.PathContains + "%"));
            if (filter.FromUtc.HasValue) clauses.Add("created_utc >= " + _D.Quote(filter.FromUtc));
            if (filter.ToUtc.HasValue) clauses.Add("created_utc < " + _D.Quote(filter.ToUtc));
            return clauses.Count == 0 ? "" : " WHERE " + string.Join(" AND ", clauses);
        }

        private static RequestHistoryEntry Map(DataRow row)
        {
            RequestHistoryEntry e = MapSummary(row);
            e.RequestHeaders = ParseHeaderMap(Converters.StringOrNull(row, "request_headers"));
            e.RequestBody = Converters.StringOrNull(row, "request_body");
            e.ResponseHeaders = ParseHeaderMap(Converters.StringOrNull(row, "response_headers"));
            e.ResponseBody = Converters.StringOrNull(row, "response_body");
            return e;
        }

        private static RequestHistoryEntry MapSummary(DataRow row)
        {
            return new RequestHistoryEntry
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.StringOrNull(row, "tenant_id"),
                UserId = Converters.StringOrNull(row, "user_id"),
                PrincipalName = Converters.StringOrNull(row, "principal_name"),
                Method = Converters.String(row, "method"),
                Path = Converters.String(row, "path"),
                Url = Converters.String(row, "url"),
                StatusCode = Converters.Int(row, "status_code"),
                DurationMs = Converters.Double(row, "duration_ms"),
                SourceIp = Converters.StringOrNull(row, "source_ip"),
                RequestBodyBytes = Converters.Long(row, "request_body_bytes"),
                RequestBodyTruncated = Converters.Bool(row, "request_body_truncated"),
                ResponseBodyBytes = Converters.Long(row, "response_body_bytes"),
                ResponseBodyTruncated = Converters.Bool(row, "response_body_truncated"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                CompletedUtc = Converters.DateTimeOrNull(row, "completed_utc")
            };
        }

        private static Dictionary<string, string> ParseHeaderMap(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new Dictionary<string, string>();
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(); }
            catch (JsonException) { return new Dictionary<string, string>(); }
        }
    }
}
