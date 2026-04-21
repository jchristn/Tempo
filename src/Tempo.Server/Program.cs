namespace Tempo.Server
{
    using System.Threading.Tasks;

    /// <summary>
    /// Application entry point. Delegates to <see cref="Bootstrapper"/>.
    /// </summary>
    public static class Program
    {
        /// <summary>Entry point.</summary>
        /// <param name="args">Command-line arguments.</param>
        public static async Task Main(string[] args)
        {
            await Bootstrapper.RunAsync(args).ConfigureAwait(false);
        }
    }
}
