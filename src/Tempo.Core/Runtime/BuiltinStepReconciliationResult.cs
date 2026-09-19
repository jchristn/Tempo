namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;

    /// <summary>Aggregate built-in step reconciliation result.</summary>
    public class BuiltinStepReconciliationResult
    {
        /// <summary>Number of built-in steps scanned.</summary>
        public int Scanned { get; set; }
        /// <summary>Number of built-in steps whose runtime binding resolved successfully.</summary>
        public int Resolved { get; set; }
        /// <summary>Number of built-in steps that matched multiple runtime candidates.</summary>
        public int Ambiguous { get; set; }
        /// <summary>Number of built-in steps with no matching runtime candidate.</summary>
        public int Orphaned { get; set; }
        /// <summary>Number of built-in steps whose binding was updated during reconciliation.</summary>
        public int Updated { get; set; }
        /// <summary>Individual reconciliation outcome entries. Never null; defaults to an empty list.</summary>
        public List<BuiltinStepReconciliationEntry> Entries { get; set; } = new List<BuiltinStepReconciliationEntry>();

        /// <summary>
        /// Adds a reconciliation entry and updates the aggregate counters based on its state.
        /// </summary>
        /// <param name="entry">The reconciliation entry to add.</param>
        /// <param name="updated">True if the entry's binding was updated during reconciliation.</param>
        public void Add(BuiltinStepReconciliationEntry entry, bool updated)
        {
            Entries.Add(entry);
            Scanned++;
            if (updated) Updated++;
            if (entry.State == Tempo.Core.Enums.StepRuntimeBindingStateEnum.Resolved) Resolved++;
            else if (entry.State == Tempo.Core.Enums.StepRuntimeBindingStateEnum.Ambiguous) Ambiguous++;
            else if (entry.State == Tempo.Core.Enums.StepRuntimeBindingStateEnum.Orphaned) Orphaned++;
        }

        /// <summary>
        /// Merges the counters and entries from another reconciliation result into this instance.
        /// </summary>
        /// <param name="other">The result to merge; ignored when null.</param>
        public void Merge(BuiltinStepReconciliationResult other)
        {
            if (other == null) return;
            Scanned += other.Scanned;
            Resolved += other.Resolved;
            Ambiguous += other.Ambiguous;
            Orphaned += other.Orphaned;
            Updated += other.Updated;
            Entries.AddRange(other.Entries);
        }
    }
}
