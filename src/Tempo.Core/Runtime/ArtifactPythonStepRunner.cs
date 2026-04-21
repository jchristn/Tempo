namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Tempo.Core.Settings;

    /// <summary>Executes an artifact Python module through a generated Tempo protocol shim.</summary>
    public class ArtifactPythonStepRunner : ArtifactProcessStepRunner
    {
        private readonly string _PythonExecutable;
        private readonly string _Module;
        private readonly string _Function;

        public ArtifactPythonStepRunner(
            string tenantId,
            ArtifactVersionSnapshot artifact,
            string artifactRoot,
            string entrypoint,
            string pythonExecutable,
            string module,
            string function,
            IEnumerable<string> arguments,
            IEnumerable<string> environmentReferences,
            ExternalExecutionSettings settings,
            ExternalRuntimeCapacityManager capacity,
            int maxRuntimeMs = 0)
            : base(tenantId, artifact, artifactRoot, entrypoint, ".", arguments, environmentReferences, settings, capacity, maxRuntimeMs)
        {
            _PythonExecutable = pythonExecutable;
            _Module = module;
            _Function = function;
        }

        protected override ProcessStartInfo BuildStartInfo(string scratch)
        {
            string shim = Path.Combine(scratch, "__tempo_python_shim.py");
            File.WriteAllText(shim, ShimSource(), Encoding.UTF8);
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = _PythonExecutable,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _ArtifactRoot
            };
            psi.ArgumentList.Add(shim);
            psi.ArgumentList.Add(_Module);
            psi.ArgumentList.Add(_Function);
            foreach (string arg in _Arguments) psi.ArgumentList.Add(arg);
            psi.Environment["TEMPO_ARTIFACT_ROOT"] = _ArtifactRoot;
            psi.Environment["TEMPO_SCRATCH_DIR"] = scratch;
            psi.Environment["PYTHONPATH"] = _ArtifactRoot + (psi.Environment.TryGetValue("PYTHONPATH", out string? existing) && !string.IsNullOrEmpty(existing) ? Path.PathSeparator + existing : string.Empty);
            foreach (string name in _EnvironmentReferences)
            {
                string? value = System.Environment.GetEnvironmentVariable(name);
                if (value != null) psi.Environment[name] = value;
            }
            WrapWithLinuxProcessGroup(psi);
            return psi;
        }

        private static string ShimSource()
        {
            return """
import importlib
import json
import sys
import traceback

def main():
    module_name = sys.argv[1]
    function_name = sys.argv[2]
    raw = sys.stdin.read()
    req = json.loads(raw)
    try:
        module = importlib.import_module(module_name)
        fn = getattr(module, function_name)
        data = fn(req)
        result = {
            "protocolVersion": req.get("protocolVersion", "1.0"),
            "tenantId": req.get("tenantId"),
            "dataFlowId": req.get("dataFlowId"),
            "flowRunId": req.get("flowRunId"),
            "stepRunId": req.get("stepRunId"),
            "requestId": req.get("requestId"),
            "result": "Success",
            "data": data,
            "metadata": req.get("metadata")
        }
    except Exception as exc:
        result = {
            "protocolVersion": req.get("protocolVersion", "1.0"),
            "tenantId": req.get("tenantId"),
            "dataFlowId": req.get("dataFlowId"),
            "flowRunId": req.get("flowRunId"),
            "stepRunId": req.get("stepRunId"),
            "requestId": req.get("requestId"),
            "result": "Exception",
            "data": None,
            "exception": str(exc),
            "metadata": {"traceback": traceback.format_exc(limit=8)}
        }
    sys.stdout.write(json.dumps(result, separators=(",", ":")))

if __name__ == "__main__":
    main()
""";
        }
    }
}
