namespace Tempo.Core.Responses
{
    using Tempo.Core.Models;

    /// <summary>Public API response for a paged step list.</summary>
    public class StepListResponse : EnumerationResult<StepResponse>
    {
        /// <summary>
        /// Creates a step list response from an enumeration result of step records.
        /// </summary>
        /// <param name="records">The enumeration result of step records to convert.</param>
        /// <returns>A step list response containing the paging information and converted step responses.</returns>
        public static StepListResponse FromRecords(EnumerationResult<StepRecord> records)
        {
            StepListResponse response = new StepListResponse
            {
                PageNumber = records.PageNumber,
                PageSize = records.PageSize,
                TotalCount = records.TotalCount
            };

            foreach (StepRecord record in records.Items)
            {
                response.Items.Add(StepResponse.FromRecord(record));
            }

            return response;
        }
    }
}
