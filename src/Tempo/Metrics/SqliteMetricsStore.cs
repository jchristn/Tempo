namespace Tempo.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tempo.Enumeration;
    using Tempo.Enums;

#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8605 // Unboxing a possibly null value.

    /// <summary>
    /// Sqlite metrics store.
    /// </summary>
    public class SqliteMetricsStore : MetricsStore
    {
        private string _DatabaseFilename;
        private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffffZ";

        /// <summary>
        /// Sqlite metrics store.
        /// </summary>
        public SqliteMetricsStore(string databaseFilename = "metrics.db")
        {
            _DatabaseFilename = (!String.IsNullOrEmpty(databaseFilename) ? databaseFilename : throw new ArgumentNullException(nameof(databaseFilename)));

            InitializeDatabase();
        }

        /// <summary>
        /// Write data flow run details.
        /// </summary>
        /// <param name="details">Details.</param>
        /// <returns>Task.</returns>
        public override async Task WriteDataFlowRun(DataFlowRunDetails details)
        {
            if (details == null) throw new ArgumentNullException(nameof(details));

            // Generate RowId if not already set
            if (string.IsNullOrEmpty(details.RowId))
            {
                details.RowId = Tempo.TempoIds.GenerateMetricRowId();
            }

            using (var connection = new SqliteConnection($"Data Source={_DatabaseFilename}"))
            {
                await connection.OpenAsync();

                string query = @"
                    INSERT INTO DataFlowRuns
                    (RowId, TenantId, DataFlowId, RequestId, StartUtc, EndUtc, Result)
                    VALUES
                    (@RowId, @TenantId, @DataFlowId, @RequestId, @StartUtc, @EndUtc, @Result)";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@RowId", details.RowId);
                    command.Parameters.AddWithValue("@TenantId", details.TenantId);
                    command.Parameters.AddWithValue("@DataFlowId", details.DataFlowId);
                    command.Parameters.AddWithValue("@RequestId", details.RequestId);
                    command.Parameters.AddWithValue("@StartUtc", FormatTimestamp(details.StartUtc));
                    command.Parameters.AddWithValue("@EndUtc", FormatTimestamp(details.EndUtc));
                    command.Parameters.AddWithValue("@Result", (int)details.Result);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Write step run details.
        /// </summary>
        /// <param name="details">Details.</param>
        /// <returns>Task.</returns>
        public override async Task WriteStepRun(StepRunDetails details)
        {
            if (details == null) throw new ArgumentNullException(nameof(details));

            // Generate RowId if not already set
            if (string.IsNullOrEmpty(details.RowId))
            {
                details.RowId = Tempo.TempoIds.GenerateMetricRowId();
            }

            using (var connection = new SqliteConnection($"Data Source={_DatabaseFilename}"))
            {
                await connection.OpenAsync();

                string query = @"
                    INSERT INTO StepRuns
                    (RowId, TenantId, DataFlowId, StepId, RequestId, NextStepId, StartUtc, EndUtc, Result)
                    VALUES
                    (@RowId, @TenantId, @DataFlowId, @StepId, @RequestId, @NextStepId, @StartUtc, @EndUtc, @Result)";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@RowId", details.RowId);
                    command.Parameters.AddWithValue("@TenantId", details.TenantId);
                    command.Parameters.AddWithValue("@DataFlowId", details.DataFlowId);
                    command.Parameters.AddWithValue("@StepId", details.StepId);
                    command.Parameters.AddWithValue("@RequestId", details.RequestId);
                    command.Parameters.AddWithValue("@NextStepId", details.NextStepId ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@StartUtc", FormatTimestamp(details.StartUtc));
                    command.Parameters.AddWithValue("@EndUtc", FormatTimestamp(details.EndUtc));
                    command.Parameters.AddWithValue("@Result", (int)details.Result);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Get data flow run details.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <returns>Data flow run details.</returns>
        public override async Task<DataFlowRunDetails> GetDataFlowRun(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentNullException(nameof(requestId));

            using (var connection = new SqliteConnection($"Data Source={_DatabaseFilename}"))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT RowId, TenantId, DataFlowId, RequestId, StartUtc, EndUtc, Result
                    FROM DataFlowRuns
                    WHERE RequestId = @RequestId";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@RequestId", requestId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new DataFlowRunDetails
                            {
                                RowId = reader["RowId"].ToString(),
                                TenantId = reader["TenantId"].ToString(),
                                DataFlowId = reader["DataFlowId"].ToString(),
                                RequestId = reader["RequestId"].ToString(),
                                StartUtc = ParseTimestamp(reader["StartUtc"].ToString()),
                                EndUtc = ParseTimestamp(reader["EndUtc"].ToString()),
                                Result = (StepResultTypeEnum)Convert.ToInt32(reader["Result"])
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get step run details for a given request.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <returns>List of step run details.</returns>
        public override async Task<List<StepRunDetails>> GetDataFlowStepRuns(string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentNullException(nameof(requestId));

            var stepRuns = new List<StepRunDetails>();

            using (var connection = new SqliteConnection($"Data Source={_DatabaseFilename}"))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT RowId, TenantId, DataFlowId, StepId, RequestId, NextStepId, StartUtc, EndUtc, Result
                    FROM StepRuns
                    WHERE RequestId = @RequestId
                    ORDER BY StartUtc";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@RequestId", requestId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var stepRun = new StepRunDetails
                            {
                                RowId = reader["RowId"].ToString(),
                                TenantId = reader["TenantId"].ToString(),
                                DataFlowId = reader["DataFlowId"].ToString(),
                                StepId = reader["StepId"].ToString(),
                                RequestId = reader["RequestId"].ToString(),
                                NextStepId = reader["NextStepId"] == DBNull.Value ? null : reader["NextStepId"].ToString(),
                                StartUtc = ParseTimestamp(reader["StartUtc"].ToString()),
                                EndUtc = ParseTimestamp(reader["EndUtc"].ToString()),
                                Result = (StepResultTypeEnum)Convert.ToInt32(reader["Result"])
                            };

                            stepRuns.Add(stepRun);
                        }
                    }
                }
            }

            return stepRuns;
        }

        /// <summary>
        /// Enumerate data flow runs.
        /// </summary>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        public override async Task<EnumerationResult<DataFlowRunDetails>> EnumerateDataFlowRuns(EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var result = new EnumerationResult<DataFlowRunDetails>();
            result.MaxResults = request.MaxResults;

            using (var connection = new SqliteConnection($"Data Source={_DatabaseFilename}"))
            {
                await connection.OpenAsync(token);

                // Get total count for the tenant
                string countQuery = "SELECT COUNT(*) FROM DataFlowRuns WHERE TenantId = @TenantId";
                using (var countCommand = new SqliteCommand(countQuery, connection))
                {
                    countCommand.Parameters.AddWithValue("@TenantId", request.TenantId);
                    result.TotalRecords = (long)await countCommand.ExecuteScalarAsync(token);
                }

                // Build ORDER BY clause
                string orderBy = request.Ordering switch
                {
                    EnumerationOrderEnum.IdAscending => "ORDER BY RowId ASC",
                    EnumerationOrderEnum.IdDescending => "ORDER BY RowId DESC",
                    EnumerationOrderEnum.StartUtcAscending => "ORDER BY StartUtc ASC",
                    EnumerationOrderEnum.StartUtcDescending => "ORDER BY StartUtc DESC",
                    _ => "ORDER BY StartUtc DESC"
                };

                // Get paginated results
                string query = $@"
                    SELECT RowId, TenantId, DataFlowId, RequestId, StartUtc, EndUtc, Result
                    FROM DataFlowRuns
                    WHERE TenantId = @TenantId
                    {orderBy}
                    LIMIT @MaxResults OFFSET @Skip";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@TenantId", request.TenantId);
                    command.Parameters.AddWithValue("@MaxResults", request.MaxResults);
                    command.Parameters.AddWithValue("@Skip", request.Skip);

                    using (var reader = await command.ExecuteReaderAsync(token))
                    {
                        while (await reader.ReadAsync(token))
                        {
                            var dataFlowRun = new DataFlowRunDetails
                            {
                                RowId = reader["RowId"].ToString(),
                                TenantId = reader["TenantId"].ToString(),
                                DataFlowId = reader["DataFlowId"].ToString(),
                                RequestId = reader["RequestId"].ToString(),
                                StartUtc = ParseTimestamp(reader["StartUtc"].ToString()),
                                EndUtc = ParseTimestamp(reader["EndUtc"].ToString()),
                                Result = (StepResultTypeEnum)Convert.ToInt32(reader["Result"])
                            };

                            result.Objects.Add(dataFlowRun);
                        }
                    }
                }

                // Calculate remaining records
                long recordsReturned = result.Objects.Count;
                result.RecordsRemaining = Math.Max(0, result.TotalRecords - request.Skip - recordsReturned);
                result.EndOfResults = result.RecordsRemaining == 0;
                result.Success = true;
            }

            return result;
        }

        /// <summary>
        /// Enumerate step runs.
        /// </summary>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        public override async Task<EnumerationResult<StepRunDetails>> EnumerateStepRuns(EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var result = new EnumerationResult<StepRunDetails>();
            result.MaxResults = request.MaxResults;

            using (var connection = new SqliteConnection($"Data Source={_DatabaseFilename}"))
            {
                await connection.OpenAsync(token);

                // Get total count for the tenant
                string countQuery = "SELECT COUNT(*) FROM StepRuns WHERE TenantId = @TenantId";
                using (var countCommand = new SqliteCommand(countQuery, connection))
                {
                    countCommand.Parameters.AddWithValue("@TenantId", request.TenantId);
                    result.TotalRecords = (long)await countCommand.ExecuteScalarAsync(token);
                }

                // Build ORDER BY clause
                string orderBy = request.Ordering switch
                {
                    EnumerationOrderEnum.IdAscending => "ORDER BY RowId ASC",
                    EnumerationOrderEnum.IdDescending => "ORDER BY RowId DESC",
                    EnumerationOrderEnum.StartUtcAscending => "ORDER BY StartUtc ASC",
                    EnumerationOrderEnum.StartUtcDescending => "ORDER BY StartUtc DESC",
                    _ => "ORDER BY StartUtc DESC"
                };

                // Get paginated results
                string query = $@"
                    SELECT RowId, TenantId, DataFlowId, StepId, RequestId, NextStepId, StartUtc, EndUtc, Result
                    FROM StepRuns
                    WHERE TenantId = @TenantId
                    {orderBy}
                    LIMIT @MaxResults OFFSET @Skip";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@TenantId", request.TenantId);
                    command.Parameters.AddWithValue("@MaxResults", request.MaxResults);
                    command.Parameters.AddWithValue("@Skip", request.Skip);

                    using (var reader = await command.ExecuteReaderAsync(token))
                    {
                        while (await reader.ReadAsync(token))
                        {
                            var stepRun = new StepRunDetails
                            {
                                RowId = reader["RowId"].ToString(),
                                TenantId = reader["TenantId"].ToString(),
                                DataFlowId = reader["DataFlowId"].ToString(),
                                StepId = reader["StepId"].ToString(),
                                RequestId = reader["RequestId"].ToString(),
                                NextStepId = reader["NextStepId"] == DBNull.Value ? null : reader["NextStepId"].ToString(),
                                StartUtc = ParseTimestamp(reader["StartUtc"].ToString()),
                                EndUtc = ParseTimestamp(reader["EndUtc"].ToString()),
                                Result = (StepResultTypeEnum)Convert.ToInt32(reader["Result"])
                            };

                            result.Objects.Add(stepRun);
                        }
                    }
                }

                // Calculate remaining records
                long recordsReturned = result.Objects.Count;
                result.RecordsRemaining = Math.Max(0, result.TotalRecords - request.Skip - recordsReturned);
                result.EndOfResults = result.RecordsRemaining == 0;
                result.Success = true;
            }

            return result;
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection($"Data Source={_DatabaseFilename}"))
            {
                connection.Open();

                // Create DataFlowRuns table
                string createDataFlowRunsTable = @"
                    CREATE TABLE IF NOT EXISTS DataFlowRuns (
                        RowId TEXT NOT NULL UNIQUE,
                        TenantId TEXT NOT NULL,
                        DataFlowId TEXT NOT NULL,
                        RequestId TEXT NOT NULL PRIMARY KEY,
                        StartUtc TEXT NOT NULL,
                        EndUtc TEXT NOT NULL,
                        Result INTEGER NOT NULL
                    )";

                using (var command = new SqliteCommand(createDataFlowRunsTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Create StepRuns table
                string createStepRunsTable = @"
                    CREATE TABLE IF NOT EXISTS StepRuns (
                        RowId TEXT NOT NULL UNIQUE,
                        TenantId TEXT NOT NULL,
                        DataFlowId TEXT NOT NULL,
                        StepId TEXT NOT NULL,
                        RequestId TEXT NOT NULL,
                        NextStepId TEXT,
                        StartUtc TEXT NOT NULL,
                        EndUtc TEXT NOT NULL,
                        Result INTEGER NOT NULL
                    )";

                using (var command = new SqliteCommand(createStepRunsTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Create indexes for DataFlowRuns
                CreateIndexIfNotExists(connection, "idx_dataflowruns_rowid", "DataFlowRuns", "RowId");
                CreateIndexIfNotExists(connection, "idx_dataflowruns_tenantid", "DataFlowRuns", "TenantId");
                CreateIndexIfNotExists(connection, "idx_dataflowruns_dataflowid", "DataFlowRuns", "DataFlowId");
                CreateIndexIfNotExists(connection, "idx_dataflowruns_startutc", "DataFlowRuns", "StartUtc");
                CreateIndexIfNotExists(connection, "idx_dataflowruns_endutc", "DataFlowRuns", "EndUtc");

                // Create indexes for StepRuns
                CreateIndexIfNotExists(connection, "idx_stepruns_rowid", "StepRuns", "RowId");
                CreateIndexIfNotExists(connection, "idx_stepruns_tenantid", "StepRuns", "TenantId");
                CreateIndexIfNotExists(connection, "idx_stepruns_dataflowid", "StepRuns", "DataFlowId");
                CreateIndexIfNotExists(connection, "idx_stepruns_stepid", "StepRuns", "StepId");
                CreateIndexIfNotExists(connection, "idx_stepruns_requestid", "StepRuns", "RequestId");
                CreateIndexIfNotExists(connection, "idx_stepruns_nextstepid", "StepRuns", "NextStepId");
                CreateIndexIfNotExists(connection, "idx_stepruns_startutc", "StepRuns", "StartUtc");
                CreateIndexIfNotExists(connection, "idx_stepruns_endutc", "StepRuns", "EndUtc");
            }
        }

        private void CreateIndexIfNotExists(SqliteConnection connection, string indexName, string tableName, string columnName)
        {
            string createIndex = $@"
                CREATE INDEX IF NOT EXISTS {indexName}
                ON {tableName} ({columnName})";

            using (var command = new SqliteCommand(createIndex, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private string FormatTimestamp(DateTime dt)
        {
            // Ensure the DateTime is treated as UTC
            DateTime utcDt = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return utcDt.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        }

        private DateTime ParseTimestamp(string timestamp)
        {
            // Parse the timestamp and ensure it's marked as UTC
            DateTime dt = DateTime.ParseExact(timestamp, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
    }

#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8605 // Unboxing a possibly null value.
}
