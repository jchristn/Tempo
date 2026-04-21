namespace Tempo
{
    /// <summary>Source kind for an in-process built-in step registration.</summary>
    public enum BuiltinStepSourceKind
    {
        /// <summary>Class-based <see cref="Step"/> registration.</summary>
        Class,

        /// <summary>Static method registration.</summary>
        Method
    }
}
