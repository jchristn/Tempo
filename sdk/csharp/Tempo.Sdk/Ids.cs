namespace Tempo.Sdk
{
    internal static class Ids
    {
        private const int IdLength = 32;
        private static readonly PrettyId.IdGenerator Generator = new PrettyId.IdGenerator();

        public static string DataFlowId()
        {
            return Generator.GenerateKSortable("flow_", IdLength);
        }

        public static string RequestId()
        {
            return Generator.GenerateKSortable("req_", IdLength);
        }
    }
}
