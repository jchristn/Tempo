namespace Tempo.Server.Runtime
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core.Services;
    using Tempo.Enums;

    /// <summary>Built-in startup samples that make built-in runtime types visible on first boot.</summary>
    internal sealed class StartupSampleClassStep : Step
    {
        public StartupSampleClassStep()
        {
            Identifier = DefaultRuntimeStepSeeder.BuiltinClassExecutionKey;
            TenantId = "global";
            Name = "Sample built-in class";
        }

        public override Task<StepResult> Run(StepRequest req)
        {
            return Task.FromResult(Success(req, "builtin-class"));
        }

        internal static StepResult Success(StepRequest req, string sample)
        {
            return new StepResult
            {
                ProtocolVersion = req.ProtocolVersion,
                TenantId = req.TenantId,
                DataFlowId = req.DataFlowId,
                FlowRunId = req.FlowRunId,
                StepRunId = req.StepRunId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = new Dictionary<string, object> { ["sample"] = sample },
                Metadata = req.Metadata
            };
        }
    }

    internal static class StartupSampleMethods
    {
        [StepMethod(DefaultRuntimeStepSeeder.BuiltinMethodExecutionKey)]
        public static Task<StepResult> RunAsync(StepRequest req)
        {
            return Task.FromResult(StartupSampleClassStep.Success(req, "builtin-method"));
        }
    }
}
