namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using global::Xunit;
    using global::Xunit.Abstractions;

    /// <summary>
    /// Exposes every non-skipped descriptor as its own xUnit theory row so the IDE
    /// test explorer can run and report on them individually.
    /// </summary>
    public sealed class TempoTheoryTests
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>Instantiate with xUnit output helper.</summary>
        public TempoTheoryTests(ITestOutputHelper output) { _Output = output; }

        /// <summary>Data source for the theory — one row per test case.</summary>
        public static TheoryData<TestCaseDescriptor> Cases
        {
            get
            {
                TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();
                foreach (TestSuiteDescriptor suite in TempoSuites.All)
                {
                    foreach (TestCaseDescriptor c in suite.Cases)
                    {
                        if (!c.Skip) data.Add(c);
                    }
                }
                return data;
            }
        }

        /// <summary>Execute a single descriptor.</summary>
        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            _Output.WriteLine("[" + testCase.SuiteId + "/" + testCase.CaseId + "] " + testCase.DisplayName);
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
