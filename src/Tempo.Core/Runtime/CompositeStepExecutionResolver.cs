namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Tries multiple resolvers in order.</summary>
    public class CompositeStepExecutionResolver : IStepExecutionResolver
    {
        private readonly List<IStepExecutionResolver> _Resolvers = new List<IStepExecutionResolver>();

        /// <summary>Instantiate.</summary>
        public CompositeStepExecutionResolver(params IStepExecutionResolver[] resolvers)
        {
            if (resolvers == null) throw new ArgumentNullException(nameof(resolvers));
            _Resolvers.AddRange(resolvers);
        }

        /// <inheritdoc/>
        public async Task<ResolvedStepExecution> ResolveAsync(string tenantId, string executionKey, FlowRunExecutionSnapshot snapshot, CancellationToken token = default)
        {
            List<string> errors = new List<string>();
            foreach (IStepExecutionResolver resolver in _Resolvers)
            {
                try
                {
                    return await resolver.ResolveAsync(tenantId, executionKey, snapshot, token).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    errors.Add(ex.Message);
                }
            }

            throw new InvalidOperationException("Unable to resolve step '" + executionKey + "' for tenant '" + tenantId + "'. " + string.Join(" ", errors));
        }
    }
}
