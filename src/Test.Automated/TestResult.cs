namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Test result.
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// Test name.
        /// </summary>
        public string TestName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the test passed.
        /// </summary>
        public bool Passed { get; set; } = false;

        /// <summary>
        /// Error message if test failed.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Detailed output for debugging.
        /// </summary>
        public List<string> Details { get; set; } = new List<string>();

        /// <summary>
        /// Test result.
        /// </summary>
        public TestResult()
        {
        }

        /// <summary>
        /// Get formatted output for the test result.
        /// </summary>
        /// <param name="verbose">Whether to include details for passing tests.</param>
        /// <returns>Formatted output.</returns>
        public string GetFormattedOutput(bool verbose = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[{(Passed ? "PASS" : "FAIL")}] {TestName}");

            if (!Passed || verbose)
            {
                if (!string.IsNullOrEmpty(ErrorMessage))
                {
                    sb.AppendLine($"  Error: {ErrorMessage}");
                }

                if (Details.Count > 0)
                {
                    sb.AppendLine("  Details:");
                    foreach (var detail in Details)
                    {
                        sb.AppendLine($"    {detail}");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
