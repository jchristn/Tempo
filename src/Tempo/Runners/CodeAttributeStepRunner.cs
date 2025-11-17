namespace Tempo.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Enums;
    using Tempo.Logs;

    /// <summary>
    /// Step runner for attribute-based code steps (static methods decorated with [StepMethod]).
    /// </summary>
    public class CodeAttributeStepRunner : StepRunner
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.

        private readonly MethodInfo _Method;

        /// <summary>
        /// Code attribute step runner.
        /// </summary>
        /// <param name="method">Static method to invoke.</param>
        /// <param name="logger">Logger instance for logging (optional).</param>
        public CodeAttributeStepRunner(MethodInfo method, Logger logger = null)
        {
            _Method = method ?? throw new ArgumentNullException(nameof(method));
            Logger = logger;

            if (!method.IsStatic)
                throw new ArgumentException("Method must be static.", nameof(method));

            // Validate method signature: static Task<StepResult> MethodName(StepRequest req)
            if (method.ReturnType != typeof(Task<StepResult>))
                throw new ArgumentException($"Method '{method.Name}' must return Task<StepResult>.", nameof(method));

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(StepRequest))
                throw new ArgumentException($"Method '{method.Name}' must have exactly one parameter of type StepRequest.", nameof(method));
        }

        /// <summary>
        /// Execute the step by invoking the static method.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Step result.</returns>
        protected override async Task<StepResult> ExecuteInternal(StepRequest req, CancellationToken token)
        {
            try
            {
                Task<StepResult> task = (Task<StepResult>)_Method.Invoke(null, new object[] { req });
                return await task.ConfigureAwait(false);
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception from reflection
                throw ex.InnerException ?? ex;
            }
        }

#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
