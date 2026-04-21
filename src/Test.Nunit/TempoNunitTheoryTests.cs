namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>Runs every Tempo descriptor as a separate NUnit TestCaseSource.</summary>
    [TestFixture]
    public sealed class TempoNunitTheoryTests
    {
        /// <summary>Data source for parameterized runs.</summary>
        public static IEnumerable Cases()
        {
            return new TouchstoneTestCaseSource(TempoSuites.All);
        }

        /// <summary>Execute a single descriptor.</summary>
        [Test]
        [TestCaseSource(nameof(Cases))]
        public async Task Run(TestCaseDescriptor descriptor)
        {
            await descriptor.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
