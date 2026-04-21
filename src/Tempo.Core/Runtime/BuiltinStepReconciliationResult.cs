namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;

    /// <summary>Aggregate built-in step reconciliation result.</summary>
    public class BuiltinStepReconciliationResult
    {
        public int Scanned { get; set; }
        public int Resolved { get; set; }
        public int Ambiguous { get; set; }
        public int Orphaned { get; set; }
        public int Updated { get; set; }
        public List<BuiltinStepReconciliationEntry> Entries { get; set; } = new List<BuiltinStepReconciliationEntry>();

        public void Add(BuiltinStepReconciliationEntry entry, bool updated)
        {
            Entries.Add(entry);
            Scanned++;
            if (updated) Updated++;
            if (entry.State == Tempo.Core.Enums.StepRuntimeBindingStateEnum.Resolved) Resolved++;
            else if (entry.State == Tempo.Core.Enums.StepRuntimeBindingStateEnum.Ambiguous) Ambiguous++;
            else if (entry.State == Tempo.Core.Enums.StepRuntimeBindingStateEnum.Orphaned) Orphaned++;
        }

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
