namespace Tempo.Worker
{
    using System.Threading.Tasks;

    /// <summary>
    /// Process entry point for the Tempo worker daemon.
    /// </summary>
    public static class Program
    {
        /// <summary>Run the worker daemon.</summary>
        public static Task Main(string[] args)
        {
            return Bootstrapper.RunAsync(args);
        }
    }
}
