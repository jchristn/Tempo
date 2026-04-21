namespace Tempo.Core.Enums
{
    /// <summary>Current binding state for a persisted step runtime.</summary>
    public enum StepRuntimeBindingStateEnum
    {
        /// <summary>The step has not been reconciled to a concrete runtime binding.</summary>
        Unresolved,

        /// <summary>The step is bound to a concrete runtime provider.</summary>
        Resolved,

        /// <summary>The step matched more than one built-in registration.</summary>
        Ambiguous,

        /// <summary>The step references a built-in registration that is not currently available.</summary>
        Orphaned
    }
}
