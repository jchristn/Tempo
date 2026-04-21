namespace Tempo
{
    /// <summary>
    /// Generates Tempo identifiers in the form prefix_timestamp_random at a total length of 32 characters.
    /// </summary>
    internal static class TempoIds
    {
        private const int IdLength = 32;
        private static readonly PrettyId.IdGenerator Generator = new PrettyId.IdGenerator();

        public static string GenerateTenantId()
        {
            return Generator.GenerateKSortable("ten_", IdLength);
        }

        public static string GenerateDataFlowId()
        {
            return Generator.GenerateKSortable("flow_", IdLength);
        }

        public static string GenerateStepId()
        {
            return Generator.GenerateKSortable("step_", IdLength);
        }

        public static string GenerateTriggerId()
        {
            return Generator.GenerateKSortable("trg_", IdLength);
        }

        public static string GenerateRequestId()
        {
            return Generator.GenerateKSortable("req_", IdLength);
        }

        public static string GenerateMetricRowId()
        {
            return Generator.GenerateKSortable("row_", IdLength);
        }
    }
}
