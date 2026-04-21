namespace Test.Automated
{
    using System;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Touchstone console runner for the Tempo shared test suites.
    /// Usage: <c>dotnet run --project src/Test.Automated -- [--results path/to/results.json]</c>
    /// </summary>
    public static class Program
    {
        /// <summary>Entry point.</summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>0 when all tests pass, 1 on failure.</returns>
        public static async Task<int> Main(string[] args)
        {
            string? resultsPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--results" && i + 1 < args.Length)
                {
                    resultsPath = args[i + 1];
                    break;
                }
            }

            return await ConsoleRunner.RunAsync(
                TempoSuites.All,
                resultsPath: resultsPath).ConfigureAwait(false);
        }
    }
}
