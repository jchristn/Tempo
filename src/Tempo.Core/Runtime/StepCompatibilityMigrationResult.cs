namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;

    /// <summary>Aggregate compatibility migration result.</summary>
    public class StepCompatibilityMigrationResult
    {
        /// <summary>Number of flows scanned during migration.</summary>
        public int FlowsScanned { get; set; }
        /// <summary>Number of flows updated during migration.</summary>
        public int FlowsUpdated { get; set; }
        /// <summary>Number of inline REST steps found during migration.</summary>
        public int InlineRestStepsFound { get; set; }
        /// <summary>Number of steps created during migration.</summary>
        public int StepsCreated { get; set; }
        /// <summary>Number of existing steps reused during migration.</summary>
        public int StepsReused { get; set; }
        /// <summary>Individual migration outcome entries. Default: empty list.</summary>
        public List<StepCompatibilityMigrationEntry> Entries { get; set; } = new List<StepCompatibilityMigrationEntry>();

        /// <summary>Adds a migration entry and updates the inline REST step and created/reused counters accordingly.</summary>
        /// <param name="entry">The migration entry to add.</param>
        public void Add(StepCompatibilityMigrationEntry entry)
        {
            Entries.Add(entry);
            InlineRestStepsFound++;
            if (entry.StepCreated) StepsCreated++;
            else StepsReused++;
        }

        /// <summary>Merges the counters and entries from another result into this instance.</summary>
        /// <param name="other">The result to merge. Ignored when null.</param>
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
