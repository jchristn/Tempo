namespace Tempo
{
    using PrettyId;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Triggers;

    /// <summary>
    /// A data flow is an orchestrated and coordinated set of steps.
    /// </summary>
    public class DataFlow
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Identifier.
        /// </summary>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Identifier)));
        }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(TenantId)));
        }

        /// <summary>
        /// Tenant object.  This property is only used in metadata expansion.  Do not use directly.
        /// </summary>
        public Tenant Tenant { get; set; } = null;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Name)));
        }

        /// <summary>
        /// Trigger identifier.
        /// </summary>
        public string TriggerId
        {
            get => _TriggerId;
            set => _TriggerId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Trigger)));
        }

        /// <summary>
        /// Trigger object.  This property is only used in metadata expansion.  Do not use directly.
        /// </summary>
        public Trigger Trigger { get; set; } = null;

        /// <summary>
        /// Maximum runtime in milliseconds.
        /// </summary>
        public int MaxRuntimeMs
        {
            get => _MaxRuntimeMs;
            set => _MaxRuntimeMs = (value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(MaxRuntimeMs)));
        }

        /// <summary>
        /// Starting step identifier.
        /// </summary>
        public string StartStepId
        {
            get => _StartStepId;
            set => _StartStepId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(StartStepId)));
        }

        /// <summary>
        /// Dictionary of step transitions.  
        /// Key is the step identifier.
        /// Value is the StepTransition object.
        /// </summary>
        public Dictionary<string, StepTransition> Steps
        {
            get => _Steps;
            set => _Steps = (value != null ? value : throw new ArgumentNullException(nameof(Steps)));
        }

        /// <summary>
        /// Creation timestamp, in UTC.
        /// </summary>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value.ToUniversalTime();
        }

        private string _Identifier = new IdGenerator().Generate("dataflow_", 64);
        private string _TenantId = null;
        private string _Name = "My dataflow";
        private string _TriggerId = null;
        private int _MaxRuntimeMs = 0;
        private string _StartStepId = null;
        private Dictionary<string, StepTransition> _Steps = new Dictionary<string, StepTransition>();
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// A data flow is an orchestrated and coordinated set of steps.
        /// </summary>
        public DataFlow()
        {

        }

        /// <summary>
        /// Validates that the starting step exists in the steps collection
        /// </summary>
        /// <returns>True if valid.</returns>
        public bool ValidateStartingStep()
        {
            return !string.IsNullOrEmpty(StartStepId) && Steps.ContainsKey(StartStepId);
        }

        /// <summary>
        /// Validates that all referenced steps in transitions exist
        /// </summary>
        /// <param name="errors">Output parameter containing validation errors if any</param>
        /// <returns>True if valid, false otherwise</returns>
        public bool ValidateStepReferences(out List<string> errors)
        {
            errors = new List<string>();

            foreach (KeyValuePair<string, StepTransition> kvp in Steps)
            {
                string stepId = kvp.Key;
                StepTransition stepDef = kvp.Value;

                // Check each transition reference
                if (!string.IsNullOrEmpty(stepDef.OnSuccess) && !Steps.ContainsKey(stepDef.OnSuccess))
                    errors.Add($"{stepId}.OnSuccess -> {stepDef.OnSuccess}");

                if (!string.IsNullOrEmpty(stepDef.OnFailure) && !Steps.ContainsKey(stepDef.OnFailure))
                    errors.Add($"{stepId}.OnFailure -> {stepDef.OnFailure}");

                if (!string.IsNullOrEmpty(stepDef.OnException) && !Steps.ContainsKey(stepDef.OnException))
                    errors.Add($"{stepId}.OnException -> {stepDef.OnException}");
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Detects if any cycles exist in the flow
        /// </summary>
        /// <returns>True if cycles exist.</returns>
        public bool HasCycles()
        {
            HashSet<string> visited = new HashSet<string>();
            HashSet<string> recursionStack = new HashSet<string>();

            // Check from each step (handles disconnected components)
            foreach (string stepId in Steps.Keys)
            {
                if (HasCycleDFS(stepId, visited, recursionStack))
                    return true;
            }

            return false;
        }

        private bool HasCycleDFS(string stepId, HashSet<string> visited, HashSet<string> recursionStack)
        {
            // If not in steps, it's a terminal reference
            if (!Steps.ContainsKey(stepId))
                return false;

            // If in recursion stack, we found a cycle
            if (recursionStack.Contains(stepId))
                return true;

            // If already fully processed, skip
            if (visited.Contains(stepId))
                return false;

            visited.Add(stepId);
            recursionStack.Add(stepId);

            StepTransition stepDef = Steps[stepId];

            // Check all possible paths
            string[] possibleNextSteps = new string[] { stepDef.OnSuccess, stepDef.OnFailure, stepDef.OnException };
            List<string> nextSteps = new List<string>();

            foreach (string nextStep in possibleNextSteps)
            {
                if (!string.IsNullOrEmpty(nextStep) && !nextSteps.Contains(nextStep))
                    nextSteps.Add(nextStep);
            }

            foreach (string nextStep in nextSteps)
            {
                if (HasCycleDFS(nextStep, visited, recursionStack))
                    return true;
            }

            recursionStack.Remove(stepId);
            return false;
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
