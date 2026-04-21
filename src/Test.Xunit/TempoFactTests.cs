namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using Xunit;

    /// <summary>
    /// Runs every Tempo shared descriptor through xUnit's Fact infrastructure.
    /// The entire suite runs as a single [Fact] that fails if any descriptor fails.
    /// </summary>
    public sealed class TempoFactTests : TouchstoneFactBase
    {
        /// <inheritdoc/>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites => TempoSuites.All;

        /// <summary>Execute every descriptor.</summary>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
