namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Tempo.Core.Settings;

    /// <summary>Executes an artifact JavaScript module through a generated Tempo protocol shim.</summary>
    public class ArtifactJavaScriptStepRunner : ArtifactProcessStepRunner
    {
        private readonly string _NodeExecutable;
        private readonly string _Module;
        private readonly string _Function;

        public ArtifactJavaScriptStepRunner(
            string tenantId,
            ArtifactVersionSnapshot artifact,
            string artifactRoot,
            string entrypoint,
            string nodeExecutable,
            string module,
            string function,
            IEnumerable<string> arguments,
            IEnumerable<string> environmentReferences,
            ExternalExecutionSettings settings,
            ExternalRuntimeCapacityManager capacity,
            int maxRuntimeMs = 0)
            : base(tenantId, artifact, artifactRoot, entrypoint, ".", arguments, environmentReferences, settings, capacity, maxRuntimeMs)
        {
            _NodeExecutable = string.IsNullOrWhiteSpace(nodeExecutable) ? "node" : nodeExecutable;
            _Module = module;
            _Function = function;
        }

        protected override ProcessStartInfo BuildStartInfo(string scratch)
        {
            string shim = Path.Combine(scratch, "__tempo_javascript_shim.cjs");
            File.WriteAllText(shim, ShimSource(), Encoding.UTF8);
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = _NodeExecutable,
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
const fs = require("fs");
const path = require("path");
const { pathToFileURL } = require("url");

function readStdin() {
  return new Promise((resolve, reject) => {
    let data = "";
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", chunk => data += chunk);
    process.stdin.on("end", () => resolve(data));
    process.stdin.on("error", reject);
  });
}

function correlate(result, req) {
  return {
    ...result,
    protocolVersion: req.protocolVersion || result.protocolVersion || "1.0",
    tenantId: req.tenantId,
    dataFlowId: req.dataFlowId,
    flowRunId: req.flowRunId,
    stepRunId: req.stepRunId,
    requestId: req.requestId
  };
}

function isStepResult(value) {
  return value && typeof value === "object" && typeof value.result === "string";
}

function resolveModule(moduleName) {
  if (!moduleName) throw new Error("module is required.");
  const root = process.env.TEMPO_ARTIFACT_ROOT || process.cwd();
  const candidate = path.isAbsolute(moduleName) ? moduleName : path.resolve(root, moduleName);
  const normalizedRoot = path.resolve(root) + path.sep;
  const normalizedCandidate = path.resolve(candidate);
  if (normalizedCandidate !== path.resolve(root) && !normalizedCandidate.startsWith(normalizedRoot)) {
    throw new Error("module escaped artifact root.");
  }
  return normalizedCandidate;
}

async function loadModule(modulePath) {
  try {
    return require(modulePath);
  } catch (err) {
    if (err && err.code === "ERR_REQUIRE_ESM") {
      return await import(pathToFileURL(modulePath).href);
    }
    throw err;
  }
}

async function main() {
  const moduleName = process.argv[2];
  const functionName = process.argv[3] || "run";
  const raw = await readStdin();
  const req = JSON.parse(raw || "{}");
  try {
    const modulePath = resolveModule(moduleName);
    const mod = await loadModule(modulePath);
    const fn = mod[functionName] || (mod.default && mod.default[functionName]) || (functionName === "default" ? mod.default : null);
    if (typeof fn !== "function") throw new Error("function not found: " + functionName);
    const data = await fn(req);
    const result = isStepResult(data)
      ? correlate(data, req)
      : {
          protocolVersion: req.protocolVersion || "1.0",
          tenantId: req.tenantId,
          dataFlowId: req.dataFlowId,
          flowRunId: req.flowRunId,
          stepRunId: req.stepRunId,
          requestId: req.requestId,
          result: "Success",
          data,
          metadata: req.metadata
        };
    process.stdout.write(JSON.stringify(result));
  } catch (err) {
    process.stdout.write(JSON.stringify({
      protocolVersion: req.protocolVersion || "1.0",
      tenantId: req.tenantId,
      dataFlowId: req.dataFlowId,
      flowRunId: req.flowRunId,
      stepRunId: req.stepRunId,
      requestId: req.requestId,
      result: "Exception",
      data: null,
      exception: err && err.message ? err.message : String(err),
      metadata: { stack: err && err.stack ? err.stack : String(err) }
    }));
  }
}

main().catch(err => {
  process.stdout.write(JSON.stringify({ protocolVersion: "1.0", result: "Exception", exception: err && err.message ? err.message : String(err) }));
});
""";
        }
    }
}
