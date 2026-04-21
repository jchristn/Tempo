namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;

    /// <summary>Aggregate compatibility migration result.</summary>
    public class StepCompatibilityMigrationResult
    {
        public int FlowsScanned { get; set; }
        public int FlowsUpdated { get; set; }
        public int InlineRestStepsFound { get; set; }
        public int StepsCreated { get; set; }
        public int StepsReused { get; set; }
        public List<StepCompatibilityMigrationEntry> Entries { get; set; } = new List<StepCompatibilityMigrationEntry>();

        public void Add(StepCompatibilityMigrationEntry entry)
        {
            Entries.Add(entry);
            InlineRestStepsFound++;
            if (entry.StepCreated) StepsCreated++;
            else StepsReused++;
        }

        public void Merge(StepCompatibilityMigrationResult other)
        {
            if (other == null) return;
            FlowsScanned += other.FlowsScanned;
            FlowsUpdated += other.FlowsUpdated;
            InlineRestStepsFound += other.InlineRestStepsFound;
            StepsCreated += other.StepsCreated;
            StepsReused += other.StepsReused;
            Entries.AddRange(other.Entries);
        }
    }
}
