namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>Runs every Tempo shared descriptor through NUnit as one test.</summary>
    [TestFixture]
    public sealed class TempoNunitFactTests : TouchstoneNunitBase
    {
        /// <inheritdoc/>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites => TempoSuites.All;

        /// <summary>Execute every descriptor.</summary>
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
