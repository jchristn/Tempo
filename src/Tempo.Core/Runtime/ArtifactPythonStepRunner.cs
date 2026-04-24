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
            RunLogSession? runLogs = null,
            RunLogStepScope? runLogStep = null,
            int maxRuntimeMs = 0)
            : base(tenantId, artifact, artifactRoot, entrypoint, ".", arguments, environmentReferences, settings, capacity, runLogs, runLogStep, maxRuntimeMs)
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
import builtins
import json
import logging
import os
import sys
import traceback
from datetime import datetime


class TempoLogWriter:
    def __init__(self, path):
        self._path = path

    def _write_line(self, level, text):
        if not self._path or text is None:
            return
        message = str(text)
        if not message:
            return
        with open(self._path, "a", encoding="utf-8") as handle:
            for line in message.splitlines() or [message]:
                handle.write(f"{datetime.utcnow().isoformat()}Z [{level}] {line}\n")

    def write(self, text):
        if text and text.strip():
            self._write_line("STDERR", text.rstrip("\n"))

    def flush(self):
        return

    def log(self, level, text):
        self._write_line(level, text)


def configure_logging():
    path = os.environ.get("TEMPO_RUN_LOG_FILE")
    if not path:
        return None
    writer = TempoLogWriter(path)

    def tempo_print(*args, sep=" ", end="\n", file=None, flush=False):
        text = sep.join("" if arg is None else str(arg) for arg in args)
        if end and end != "\n":
            text += end
        writer.log("INFO", text.rstrip("\n"))

    builtins.print = tempo_print
    sys.stderr = writer

    class TempoHandler(logging.Handler):
        def emit(self, record):
            try:
                writer.log(record.levelname, self.format(record))
            except Exception:
                pass

    handler = TempoHandler()
    handler.setFormatter(logging.Formatter("%(message)s"))
    root = logging.getLogger()
    root.handlers = [handler]
    root.setLevel(logging.INFO)
    return writer

def main():
    module_name = sys.argv[1]
    function_name = sys.argv[2]
    raw = sys.stdin.read()
    req = json.loads(raw)
    configure_logging()
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
