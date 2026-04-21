namespace Tempo.Core.Responses
{
    using Tempo.Core.Models;

    /// <summary>Public API response for a paged step list.</summary>
    public class StepListResponse : EnumerationResult<StepResponse>
    {
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
