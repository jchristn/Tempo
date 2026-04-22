import { useEffect, useMemo, useState } from 'react';
import Modal from './Modal';
import TenantPicker from './TenantPicker';
import CopyableId from './CopyableId';
import CopyButton from './CopyButton';
import { formatTime } from '../utils/formatters';

export function codeTemplate(language, kind = 'echo') {
  if (language === 'CSharp') {
    if (kind === 'random') {
      return {
        fileName: 'RandomNumberHandler.cs',
        function: 'run',
        handlerType: 'Tempo.UserSteps.RandomNumberHandler',
        code: 'using System;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing Tempo;\nusing Tempo.Protocol;\n\nnamespace Tempo.UserSteps;\n\npublic sealed class RandomNumberHandler : ITempoStepHandler\n{\n    private static readonly Random Random = new Random();\n\n    public Task<StepResult> RunAsync(StepRequest request, CancellationToken token = default)\n    {\n        int value = Random.Next(1, 11);\n        Console.Error.WriteLine("Random number step generated value: " + value);\n        return Task.FromResult(TempoStepHost.Success(request, new { value, min = 1, max = 10 }));\n    }\n}\n'
      };
    }
    if (kind === 'double') {
      return {
        fileName: 'DoubleNumberHandler.cs',
        function: 'run',
        handlerType: 'Tempo.UserSteps.DoubleNumberHandler',
        code: 'using System;\nusing System.Text.Json;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing Tempo;\nusing Tempo.Protocol;\n\nnamespace Tempo.UserSteps;\n\npublic sealed class DoubleNumberHandler : ITempoStepHandler\n{\n    public Task<StepResult> RunAsync(StepRequest request, CancellationToken token = default)\n    {\n        double input = ReadNumber(request.Data);\n        Console.Error.WriteLine("Double number step received value: " + input);\n        return Task.FromResult(TempoStepHost.Success(request, new { input, value = input * 2 }));\n    }\n\n    private static double ReadNumber(object? data)\n    {\n        if (data is JsonElement element)\n        {\n            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double number)) return number;\n            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("value", out JsonElement value) && value.TryGetDouble(out double nested)) return nested;\n        }\n        return 0;\n    }\n}\n'
      };
    }
    return {
      fileName: 'Handler.cs',
      function: 'run',
      handlerType: 'Tempo.UserSteps.Handler',
      code: 'using System;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing Tempo;\nusing Tempo.Protocol;\n\nnamespace Tempo.UserSteps;\n\npublic sealed class Handler : ITempoStepHandler\n{\n    public Task<StepResult> RunAsync(StepRequest request, CancellationToken token = default)\n    {\n        Console.Error.WriteLine("Echo step received input: " + request.Data);\n        return Task.FromResult(TempoStepHost.Success(request, new { ok = true, input = request.Data }));\n    }\n}\n'
    };
  }
  if (language === 'Python') {
    if (kind === 'random') {
      return {
        fileName: 'random_number_handler.py',
        function: 'run',
        handlerType: 'Tempo.UserSteps.Handler',
        code: 'import random\n\n\ndef run(req):\n    value = random.randint(1, 10)\n    print(f"Random number step generated value: {value}")\n    return {\"value\": value, \"min\": 1, \"max\": 10}\n'
      };
    }
    if (kind === 'double') {
      return {
        fileName: 'double_number_handler.py',
        function: 'run',
        handlerType: 'Tempo.UserSteps.Handler',
        code: 'def run(req):\n    data = req.get(\"data\") or {}\n    value = data if isinstance(data, (int, float)) else data.get(\"value\", 0)\n    print(f"Double number step received value: {value}")\n    return {\"input\": value, \"value\": value * 2}\n'
      };
    }
    return {
      fileName: 'handler.py',
      function: 'run',
      handlerType: 'Tempo.UserSteps.Handler',
      code: 'def run(req):\n    print("Echo step received input:", req.get("data"))\n    return {\"ok\": True, \"input\": req.get(\"data\")}\n'
    };
  }
  if (kind === 'random') {
    return {
      fileName: 'random-number-handler.js',
      function: 'run',
      handlerType: 'Tempo.UserSteps.Handler',
      code: 'exports.run = async function(req) {\n  const value = Math.floor(Math.random() * 10) + 1;\n  console.log("Random number step generated value:", value);\n  return { value, min: 1, max: 10 };\n};\n'
    };
  }
  if (kind === 'double') {
    return {
      fileName: 'double-number-handler.js',
      function: 'run',
      handlerType: 'Tempo.UserSteps.Handler',
      code: 'exports.run = async function(req) {\n  const data = req.data || {};\n  const value = typeof data === "number" ? data : Number(data.value || 0);\n  console.log("Double number step received value:", value);\n  return { input: value, value: value * 2 };\n};\n'
    };
  }
  return {
    fileName: 'handler.js',
    function: 'run',
    handlerType: 'Tempo.UserSteps.Handler',
    code: 'exports.run = async function(req) {\n  console.log("Echo step received input:", req.data);\n  return { ok: true, input: req.data };\n};\n'
  };
}

function normalizeError(err) {
  if (!err) return 'Request failed.';
  if (err.body) {
    try {
      const parsed = JSON.parse(err.body);
      return parsed.message || parsed.details || err.message;
    } catch { return err.body; }
  }
  return err.message || String(err);
}

function compactJson(text) {
  const trimmed = (text || '').trim();
  if (!trimmed) return '{}';
  try {
    return JSON.stringify(JSON.parse(trimmed));
  } catch {
    return trimmed;
  }
}

function shellSingleQuote(value) {
  return "'" + String(value).replace(/'/g, "'\\''") + "'";
}

function windowsCmdDoubleQuote(value) {
  return '"' + String(value).replace(/"/g, '\\\"') + '"';
}

function clientPlatform() {
  if (typeof navigator === 'undefined') return '';
  return navigator.userAgentData?.platform || navigator.platform || '';
}

function runItYourselfCommand(triggerUrl, inputJson, method = 'POST') {
  const normalizedMethod = (method || 'POST').toUpperCase();
  const hasBody = normalizedMethod !== 'GET' && (typeof inputJson === 'string' ? inputJson.trim().length > 0 : inputJson !== null && inputJson !== undefined);
  const payload = hasBody ? compactJson(inputJson) : '';
  const platform = clientPlatform();
  if (/win/i.test(platform)) {
    const base = normalizedMethod === 'GET'
      ? 'curl.exe ' + windowsCmdDoubleQuote(triggerUrl)
      : 'curl.exe -X ' + normalizedMethod + ' ' + windowsCmdDoubleQuote(triggerUrl);
    return {
      label: 'Windows cmd.exe',
      command: hasBody ? base + ' -H "Content-Type: application/json" -d ' + windowsCmdDoubleQuote(payload) : base
    };
  }
  const base = normalizedMethod === 'GET'
    ? 'curl ' + shellSingleQuote(triggerUrl)
    : 'curl -X ' + normalizedMethod + ' ' + shellSingleQuote(triggerUrl);
  return {
    label: /mac/i.test(platform) ? 'macOS/Linux shell' : 'Shell',
    command: hasBody ? base + " -H 'Content-Type: application/json' --data-raw " + shellSingleQuote(payload) : base
  };
}

const SOURCE_RUNTIME_BY_LANGUAGE = {
  JavaScript: 'Artifact.JavaScript',
  Python: 'Artifact.Python',
  CSharp: 'Artifact.DotnetProcess'
};
const SOURCE_LANGUAGE_ORDER = ['JavaScript', 'Python', 'CSharp'];
const FULL_ID_MAX = 128;
const RANDOM_STEP_DEFAULTS = {
  executionKey: 'random_number_step',
  name: 'Random number step',
  description: 'Generates a random integer between 1 and 10.',
  entrypoint: 'main',
  artifactName: 'Random number step source package'
};
const DOUBLE_STEP_DEFAULTS = {
  executionKey: 'double_number_step',
  name: 'Double number step',
  description: 'Multiplies the previous step output by 2.',
  entrypoint: 'main',
  artifactName: 'Double number step source package'
};

function stepDefaults(language, kind) {
  if (kind === 'random') return { ...RANDOM_STEP_DEFAULTS, ...codeTemplate(language, 'random') };
  if (kind === 'double') return { ...DOUBLE_STEP_DEFAULTS, ...codeTemplate(language, 'double') };
  return {
    executionKey: 'echo_step',
    name: 'Echo step',
    description: 'Echoes the input payload.',
    entrypoint: 'main',
    artifactName: 'Echo step source package',
    ...codeTemplate(language)
  };
}

function sourceLanguageAvailability(runtimes) {
  const descriptors = Array.isArray(runtimes) ? runtimes : [];
  return SOURCE_LANGUAGE_ORDER.filter((language) => {
    const runtimeKey = SOURCE_RUNTIME_BY_LANGUAGE[language];
    const descriptor = descriptors.find((item) => item.runtimeKey === runtimeKey || item.RuntimeKey === runtimeKey);
    const availability = descriptor?.availability || descriptor?.Availability;
    return availability === 'Available';
  });
}

function SetupWizard({ open, apiClient, principal, onClose }) {
  const [stepIndex, setStepIndex] = useState(0);
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [language, setLanguage] = useState('JavaScript');
  const [stepForm, setStepForm] = useState(() => stepDefaults('JavaScript', 'echo'));
  const [randomStepForm, setRandomStepForm] = useState(() => stepDefaults('JavaScript', 'random'));
  const [doubleStepForm, setDoubleStepForm] = useState(() => stepDefaults('JavaScript', 'double'));
  const [flowName, setFlowName] = useState('Echo data flow');
  const [flowDescription, setFlowDescription] = useState('Runs the echo step and returns the input payload.');
  const [chainFlowName, setChainFlowName] = useState('Random doubled data flow');
  const [chainFlowDescription, setChainFlowDescription] = useState('Generates a random number, passes it into a second step, and returns the doubled value.');
  const [triggerName, setTriggerName] = useState('Echo HTTP trigger');
  const [chainTriggerName, setChainTriggerName] = useState('Random doubled HTTP trigger');
  const [runInput, setRunInput] = useState('{\n  "value": 123\n}');
  const [createdStep, setCreatedStep] = useState(null);
  const [createdArtifact, setCreatedArtifact] = useState(null);
  const [createdChainSteps, setCreatedChainSteps] = useState({ random: null, double: null });
  const [createdChainArtifacts, setCreatedChainArtifacts] = useState({ random: null, double: null });
  const [createdFlow, setCreatedFlow] = useState(null);
  const [createdChainFlow, setCreatedChainFlow] = useState(null);
  const [createdTrigger, setCreatedTrigger] = useState(null);
  const [createdChainTrigger, setCreatedChainTrigger] = useState(null);
  const [createdRun, setCreatedRun] = useState(null);
  const [createdChainRun, setCreatedChainRun] = useState(null);
  const [runDetails, setRunDetails] = useState(null);
  const [chainRunDetails, setChainRunDetails] = useState(null);
  const [triggerResponseBody, setTriggerResponseBody] = useState('');
  const [chainTriggerResponseBody, setChainTriggerResponseBody] = useState('');
  const [triggerResponseHeaders, setTriggerResponseHeaders] = useState({});
  const [chainTriggerResponseHeaders, setChainTriggerResponseHeaders] = useState({});
  const [availableSourceLanguages, setAvailableSourceLanguages] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const changeLanguage = (next) => {
    setLanguage(next);
    setStepForm((current) => ({ ...current, ...codeTemplate(next) }));
    setRandomStepForm((current) => ({ ...current, ...codeTemplate(next, 'random') }));
    setDoubleStepForm((current) => ({ ...current, ...codeTemplate(next, 'double') }));
    setError('');
  };

  useEffect(() => {
    if (open) setTenantId(principal?.tenantId || tenantId || '');
  }, [open, principal?.tenantId]);

  useEffect(() => {
    let cancelled = false;
    if (!open || !tenantId) return;
    apiClient.listTenantRuntimes(tenantId)
      .then((runtimes) => {
        if (cancelled) return;
        const languages = sourceLanguageAvailability(runtimes);
        setAvailableSourceLanguages(languages);
        if (languages.length > 0) {
          setLanguage((current) => {
            if (languages.includes(current)) return current;
            const next = languages[0];
            setStepForm((form) => ({ ...form, ...codeTemplate(next) }));
            setRandomStepForm((form) => ({ ...form, ...codeTemplate(next, 'random') }));
            setDoubleStepForm((form) => ({ ...form, ...codeTemplate(next, 'double') }));
            return next;
          });
        }
      })
      .catch(() => {
        if (!cancelled) setAvailableSourceLanguages(null);
      });
    return () => { cancelled = true; };
  }, [open, tenantId, apiClient]);

  const steps = useMemo(() => ['Welcome', 'Steps', 'Flows', 'Triggers', 'Runs', 'Done'], []);
  const runtimeOptionsLoaded = availableSourceLanguages !== null;
  const selectedRuntimeAvailable = !runtimeOptionsLoaded || availableSourceLanguages.includes(language);
  const triggerUrl = createdTrigger?.id ? (apiClient?.baseUrl || '') + '/v1.0/triggers/http/' + createdTrigger.id : '';
  const chainTriggerUrl = createdChainTrigger?.id ? (apiClient?.baseUrl || '') + '/v1.0/triggers/http/' + createdChainTrigger.id : '';
  const runCommand = useMemo(() => runItYourselfCommand(triggerUrl, runInput, 'POST'), [triggerUrl, runInput]);
  const chainRunCommand = useMemo(() => runItYourselfCommand(chainTriggerUrl, null, 'GET'), [chainTriggerUrl]);
  const responseHeaderText = useMemo(() => {
    const entries = Object.entries(triggerResponseHeaders || {})
      .filter(([key]) => key.toLowerCase().startsWith('x-'))
      .sort(([a], [b]) => a.localeCompare(b));
    return entries.map(([key, value]) => key + ': ' + value).join('\n');
  }, [triggerResponseHeaders]);
  const chainResponseHeaderText = useMemo(() => {
    const entries = Object.entries(chainTriggerResponseHeaders || {})
      .filter(([key]) => key.toLowerCase().startsWith('x-'))
      .sort(([a], [b]) => a.localeCompare(b));
    return entries.map(([key, value]) => key + ': ' + value).join('\n');
  }, [chainTriggerResponseHeaders]);

  const createStep = async () => {
    if (!selectedRuntimeAvailable) {
      setError('The selected source runtime is not available. Configure the runtime command in Tempo.Server settings or choose another language.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      const response = await apiClient.createSourceStep(tenantId, {
        executionKey: stepForm.executionKey,
        name: stepForm.name,
        description: stepForm.description || null,
        language,
        code: stepForm.code,
        fileName: stepForm.fileName,
        artifactName: stepForm.artifactName || null,
        entrypoint: stepForm.entrypoint || 'main',
        function: stepForm.function || 'run',
        handlerType: stepForm.handlerType || 'Tempo.UserSteps.Handler',
        contractType: 'Loose',
        active: true
      });
      const randomResponse = await apiClient.createSourceStep(tenantId, {
        executionKey: randomStepForm.executionKey,
        name: randomStepForm.name,
        description: randomStepForm.description || null,
        code: randomStepForm.code,
        fileName: randomStepForm.fileName,
        artifactName: randomStepForm.artifactName || null,
        entrypoint: randomStepForm.entrypoint || 'main',
        function: randomStepForm.function || 'run',
        handlerType: randomStepForm.handlerType || 'Tempo.UserSteps.Handler',
        language,
        contractType: 'Loose',
        active: true
      });
      const doubleResponse = await apiClient.createSourceStep(tenantId, {
        executionKey: doubleStepForm.executionKey,
        name: doubleStepForm.name,
        description: doubleStepForm.description || null,
        code: doubleStepForm.code,
        fileName: doubleStepForm.fileName,
        artifactName: doubleStepForm.artifactName || null,
        entrypoint: doubleStepForm.entrypoint || 'main',
        function: doubleStepForm.function || 'run',
        handlerType: doubleStepForm.handlerType || 'Tempo.UserSteps.Handler',
        language,
        contractType: 'Loose',
        active: true
      });
      setCreatedStep(response.step);
      setCreatedArtifact(response.artifact);
      setCreatedChainSteps({ random: randomResponse.step, double: doubleResponse.step });
      setCreatedChainArtifacts({ random: randomResponse.artifact, double: doubleResponse.artifact });
      setStepIndex(2);
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setBusy(false);
    }
  };

  const createFlow = async () => {
    setBusy(true);
    setError('');
    try {
      const executionKey = createdStep.executionKey;
      const flow = await apiClient.createFlow(tenantId, {
        name: flowName || 'First data flow',
        description: flowDescription || null,
        startStepId: executionKey,
        transitions: {
          [executionKey]: { OnSuccess: null, OnFailure: null, OnException: null }
        },
        maxRuntimeMs: 0,
        active: true
      });
      const randomKey = createdChainSteps.random.executionKey;
      const doubleKey = createdChainSteps.double.executionKey;
      const chainFlow = await apiClient.createFlow(tenantId, {
        name: chainFlowName || 'Random doubled data flow',
        description: chainFlowDescription || null,
        startStepId: randomKey,
        transitions: {
          [randomKey]: { Name: 'Generate random number', OnSuccess: doubleKey, OnFailure: null, OnException: null },
          [doubleKey]: { Name: 'Double number', OnSuccess: null, OnFailure: null, OnException: null }
        },
        maxRuntimeMs: 0,
        active: true
      });
      setCreatedFlow(flow);
      setCreatedChainFlow(chainFlow);
      setStepIndex(3);
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setBusy(false);
    }
  };

  const createTrigger = async () => {
    setBusy(true);
    setError('');
    try {
      const trigger = await apiClient.createTrigger(tenantId, {
        name: triggerName || 'Echo HTTP trigger',
        triggerType: 'Http',
        dataFlowId: createdFlow.id,
        configuration: JSON.stringify({ allowedMethods: ['POST'], headers: {}, bodySchema: null }, null, 2),
        active: true
      });
      const chainTrigger = await apiClient.createTrigger(tenantId, {
        name: chainTriggerName || 'Random doubled HTTP trigger',
        triggerType: 'Http',
        dataFlowId: createdChainFlow.id,
        configuration: JSON.stringify({ allowedMethods: ['GET'], headers: {}, bodySchema: null }, null, 2),
        active: true
      });
      setCreatedTrigger(trigger);
      setCreatedChainTrigger(chainTrigger);
      setStepIndex(4);
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setBusy(false);
    }
  };

  const runFlow = async () => {
    setBusy(true);
    setError('');
    try {
      const data = runInput.trim() ? JSON.parse(runInput) : {};
      const response = await apiClient.fireHttpTriggerDetailed(createdTrigger.id, data);
      setTriggerResponseBody(response.body || '');
      setTriggerResponseHeaders(response.headers || {});

      const runId = response.headers?.['x-run-id'];
      const runState = response.headers?.['x-run-state'];
      const createdUtc = response.headers?.['x-run-created-utc'];
      const completedUtc = response.headers?.['x-run-completed-utc'];
      let details = null;
      if (runId) {
        details = await apiClient.readRun(tenantId, runId).catch(() => null);
      }
      setCreatedRun(details || { id: runId, state: runState, createdUtc, completedUtc });
      setRunDetails(details);

      const chainResponse = await apiClient.fireHttpTriggerDetailed(createdChainTrigger.id, null, 'GET');
      setChainTriggerResponseBody(chainResponse.body || '');
      setChainTriggerResponseHeaders(chainResponse.headers || {});

      const chainRunId = chainResponse.headers?.['x-run-id'];
      const chainRunState = chainResponse.headers?.['x-run-state'];
      const chainCreatedUtc = chainResponse.headers?.['x-run-created-utc'];
      const chainCompletedUtc = chainResponse.headers?.['x-run-completed-utc'];
      let chainedDetails = null;
      if (chainRunId) {
        chainedDetails = await apiClient.readRun(tenantId, chainRunId).catch(() => null);
      }
      setCreatedChainRun(chainedDetails || { id: chainRunId, state: chainRunState, createdUtc: chainCreatedUtc, completedUtc: chainCompletedUtc });
      setChainRunDetails(chainedDetails);
      setStepIndex(5);
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setBusy(false);
    }
  };

  const close = () => {
    onClose?.();
  };

  if (!open) return null;

  return (
    <Modal open={open} onClose={close} title="Setup wizard" size="large"
      footer={<WizardFooter stepIndex={stepIndex} busy={busy} canCreateStep={selectedRuntimeAvailable} onClose={close} onStart={() => setStepIndex(1)} onStep={createStep} onFlow={createFlow} onTrigger={createTrigger} onRun={runFlow} />}>
      <div className="wizard-steps">
        {steps.map((label, index) => (
          <div key={label} className={'wizard-step' + (index === stepIndex ? ' active' : index < stepIndex ? ' done' : '')}>
            <span>{index + 1}</span>
            {label}
          </div>
        ))}
      </div>

      {error && <div className="login-error">{error}</div>}

      {stepIndex === 0 && (
        <div>
          <WizardExplanation
            title="Build and run your first data flows"
            what="Tempo runs work as steps connected together in a data flow. This setup creates an echo flow and a second flow that chains a random-number step into a multiply-by-2 step."
            why="A working one-step flow and a working chained flow give you the core model before you add more steps, artifacts, runtime options, and external integrations."
            how="Tempo packages small source files as artifact-backed steps, connects those steps into flows, creates reusable trigger URLs, then POSTs sample JSON through both flows."
          />
          <div className="wizard-start-list">
            <div>
              <strong>Welcome</strong>
              <span>Review exactly what setup will create.</span>
            </div>
            <div>
              <strong>Steps</strong>
              <span>Create one echo step and two chained math steps.</span>
            </div>
            <div>
              <strong>Flows</strong>
              <span>Create one echo flow and one random-to-double flow.</span>
            </div>
            <div>
              <strong>Triggers</strong>
              <span>Create one HTTP trigger for each flow.</span>
            </div>
            <div>
              <strong>Runs</strong>
              <span>POST sample JSON through both triggers.</span>
            </div>
            <div>
              <strong>Done</strong>
              <span>Review IDs, response bodies, headers, and copyable commands.</span>
            </div>
          </div>
        </div>
      )}

      {stepIndex === 1 && (
        <div>
          <WizardExplanation
            title="Create the first steps"
            what="Tempo creates one echo step plus a random-number step and a double-number step in the same language."
            why="Flows reference steps by execution key. The chained sample uses the random step output as the double step input, so you can see how data moves through a flow."
            how="Choose an available language and edit any of the three starter definitions. Tempo packages all three source files as artifact-backed steps for the selected runtime."
          />
          {runtimeOptionsLoaded && availableSourceLanguages.length === 0 && (
            <div className="callout callout-warning">No source-code runtime is available. Configure Node.js, Python, or .NET in Tempo.Server settings before creating source-backed setup steps.</div>
          )}
          <div className="grid-2">
            <div className="form-row">
              <label title="Tenant that owns the generated steps, artifacts, flows, triggers, and runs">Tenant</label>
              <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            </div>
            <div className="form-row">
              <label title="Source language for the generated artifact-backed steps">Language</label>
              <select value={language} onChange={(e) => changeLanguage(e.target.value)}>
                {SOURCE_LANGUAGE_ORDER.map((item) => {
                  const disabled = runtimeOptionsLoaded && !availableSourceLanguages.includes(item);
                  const label = item === 'CSharp' ? 'C#' : item;
                  return <option key={item} value={item} disabled={disabled}>{label}{disabled ? ' (unavailable)' : ''}</option>;
                })}
              </select>
            </div>
          </div>
          <div className="form-row">
            <label>Steps this phase creates</label>
            <div className="data-table-wrapper">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Flow</th>
                    <th>Step</th>
                    <th>Execution key</th>
                    <th>Artifact package</th>
                    <th>Source</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td>Echo data flow</td>
                    <td>{stepForm.name || 'Echo step'}</td>
                    <td className="monospace">{stepForm.executionKey || 'echo_step'}</td>
                    <td>{stepForm.artifactName || 'Echo step source package'}</td>
                    <td>Editable below</td>
                  </tr>
                  <tr>
                    <td>Random doubled data flow</td>
                    <td>{randomStepForm.name || 'Random number step'}</td>
                    <td className="monospace">{randomStepForm.executionKey || 'random_number_step'}</td>
                    <td>{randomStepForm.artifactName || 'Random number step source package'}</td>
                    <td>Editable below</td>
                  </tr>
                  <tr>
                    <td>Random doubled data flow</td>
                    <td>{doubleStepForm.name || 'Double number step'}</td>
                    <td className="monospace">{doubleStepForm.executionKey || 'double_number_step'}</td>
                    <td>{doubleStepForm.artifactName || 'Double number step source package'}</td>
                    <td>Editable below</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <SourceStepEditor title="Echo step" language={language} form={stepForm} setForm={setStepForm} />
          <SourceStepEditor title="Random number step" language={language} form={randomStepForm} setForm={setRandomStepForm} />
          <SourceStepEditor title="Double number step" language={language} form={doubleStepForm} setForm={setDoubleStepForm} />
        </div>
      )}

      {stepIndex === 2 && (
        <div>
          <WizardExplanation
            title="Connect steps in data flows"
            what="Tempo creates one echo flow and one chained flow that starts with the random-number step and continues into the double-number step."
            why="The second flow demonstrates orchestration: each successful step writes output, and Tempo passes that output as the next step input."
            how="The echo flow stops after one step. The chained flow routes OnSuccess from the random step to the double step, then returns the double step output."
          />
          <div className="callout callout-success">Created steps <strong>{createdStep.name}</strong>, <strong>{createdChainSteps.random?.name}</strong>, and <strong>{createdChainSteps.double?.name}</strong>.</div>
          <div className="form-row">
            <label>Flows this phase creates</label>
            <div className="data-table-wrapper">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Flow</th>
                    <th>Start step</th>
                    <th>Success path</th>
                    <th>Returned output</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td>{flowName || 'Echo data flow'}</td>
                    <td className="monospace">{createdStep.executionKey}</td>
                    <td className="monospace">{createdStep.executionKey}{' -> '}stop</td>
                    <td>Echo step output</td>
                  </tr>
                  <tr>
                    <td>{chainFlowName || 'Random doubled data flow'}</td>
                    <td className="monospace">{createdChainSteps.random?.executionKey}</td>
                    <td className="monospace">{createdChainSteps.random?.executionKey}{' -> '}{createdChainSteps.double?.executionKey}{' -> '}stop</td>
                    <td>Double number step output</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div className="grid-2">
            <div className="form-row"><label title="Data flow display name">Flow name</label><input value={flowName} onChange={(e) => setFlowName(e.target.value)} /></div>
            <div className="form-row"><label title="First step executed by the flow">Start step</label><input value={createdStep.executionKey} readOnly /></div>
          </div>
          <div className="form-row"><label title="Optional flow description">Flow description</label><input value={flowDescription} onChange={(e) => setFlowDescription(e.target.value)} /></div>
          <pre className="code-block">{JSON.stringify({ [createdStep.executionKey]: { OnSuccess: null, OnFailure: null, OnException: null } }, null, 2)}</pre>
          <div className="grid-2" style={{ marginTop: 'var(--spacing-md)' }}>
            <div className="form-row"><label title="Chained data flow display name">Chained flow name</label><input value={chainFlowName} onChange={(e) => setChainFlowName(e.target.value)} /></div>
            <div className="form-row"><label title="First step executed by the chained flow">Chained start step</label><input value={createdChainSteps.random?.executionKey || ''} readOnly /></div>
          </div>
          <div className="form-row"><label title="Optional chained flow description">Chained flow description</label><input value={chainFlowDescription} onChange={(e) => setChainFlowDescription(e.target.value)} /></div>
          <pre className="code-block">{JSON.stringify({
            [createdChainSteps.random?.executionKey || 'random_number_step']: { OnSuccess: createdChainSteps.double?.executionKey || 'double_number_step', OnFailure: null, OnException: null },
            [createdChainSteps.double?.executionKey || 'double_number_step']: { OnSuccess: null, OnFailure: null, OnException: null }
          }, null, 2)}</pre>
        </div>
      )}

      {stepIndex === 3 && (
        <div>
          <WizardExplanation
            title="Create HTTP triggers"
            what="Tempo creates one trigger for the echo flow and one trigger for the random doubled flow."
            why="Triggers are how outside callers start a data flow without opening the dashboard. Each trigger URL is a reusable entry point for webhooks, scripts, and applications."
            how="The echo trigger accepts POST with JSON. The chained trigger accepts GET because the random-number step generates its own starting value."
          />
          <div className="callout callout-success">Created flows <strong>{createdFlow.name}</strong> and <strong>{createdChainFlow.name}</strong>.</div>
          <div className="form-row">
            <label>Triggers this phase creates</label>
            <div className="data-table-wrapper">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Trigger</th>
                    <th>Connected flow</th>
                    <th>Trigger type</th>
                    <th>Method</th>
                    <th>What the caller receives</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td>{triggerName || 'Echo HTTP trigger'}</td>
                    <td>{createdFlow.name}</td>
                    <td>Http</td>
                    <td>POST</td>
                    <td>Echo step output</td>
                  </tr>
                  <tr>
                    <td>{chainTriggerName || 'Random doubled HTTP trigger'}</td>
                    <td>{createdChainFlow.name}</td>
                    <td>Http</td>
                    <td>GET</td>
                    <td>Double number step output</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div className="details-kv" style={{ marginBottom: 'var(--spacing-md)' }}>
            <dt>Echo flow</dt><dd><CopyableId value={createdFlow.id} max={FULL_ID_MAX} /></dd>
            <dt>Echo start step</dt><dd className="monospace">{createdFlow.startStepId}</dd>
            <dt>Chained flow</dt><dd><CopyableId value={createdChainFlow.id} max={FULL_ID_MAX} /></dd>
            <dt>Chained start step</dt><dd className="monospace">{createdChainFlow.startStepId}</dd>
          </div>
          <div className="grid-2">
            <div className="form-row"><label title="Trigger display name">Trigger name</label><input value={triggerName} onChange={(e) => setTriggerName(e.target.value)} /></div>
            <div className="form-row"><label title="Chained trigger display name">Chained trigger name</label><input value={chainTriggerName} onChange={(e) => setChainTriggerName(e.target.value)} /></div>
          </div>
          <div className="grid-2">
            <div className="form-row">
              <label title="HTTP trigger configuration saved with the echo trigger">Echo trigger configuration</label>
              <pre className="code-block">{JSON.stringify({ allowedMethods: ['POST'], headers: {}, bodySchema: null }, null, 2)}</pre>
            </div>
            <div className="form-row">
              <label title="HTTP trigger configuration saved with the chained trigger">Chained trigger configuration</label>
              <pre className="code-block">{JSON.stringify({ allowedMethods: ['GET'], headers: {}, bodySchema: null }, null, 2)}</pre>
            </div>
          </div>
        </div>
      )}

      {stepIndex === 4 && (
        <div>
          <WizardExplanation
            title="Run through the triggers"
            what="Tempo sends JSON to the echo trigger and calls the chained trigger with GET, then records both executions."
            why="This is the same path you will use later from curl, webhooks, scheduled jobs, or applications. The trigger makes each flow runnable from outside the dashboard."
            how="The echo flow returns the input. The chained flow generates a number from 1 to 10, passes it to the double step, and returns the doubled value."
          />
          <div className="callout callout-success">Created triggers <strong>{createdTrigger.name}</strong> and <strong>{createdChainTrigger.name}</strong>.</div>
          <div className="form-row">
            <label>Runs this phase submits</label>
            <div className="data-table-wrapper">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Run</th>
                    <th>Trigger</th>
                    <th>Flow path</th>
                    <th>Response body source</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td>Echo run</td>
                    <td>{createdTrigger.name}</td>
                    <td className="monospace">{createdStep.executionKey}</td>
                    <td>Echo step output</td>
                  </tr>
                  <tr>
                    <td>Chained run</td>
                    <td>{createdChainTrigger.name}</td>
                    <td className="monospace">{createdChainSteps.random?.executionKey}{' -> '}{createdChainSteps.double?.executionKey}</td>
                    <td>Double number step output</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div className="details-kv" style={{ marginBottom: 'var(--spacing-md)' }}>
            <dt>Echo flow</dt><dd><CopyableId value={createdFlow.id} max={FULL_ID_MAX} /></dd>
            <dt>Echo trigger</dt><dd><CopyableId value={createdTrigger.id} max={FULL_ID_MAX} /></dd>
            <dt>Echo trigger URL</dt>
            <dd style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <code className="monospace">{triggerUrl}</code>
              <CopyButton value={triggerUrl} />
            </dd>
            <dt>Chained flow</dt><dd><CopyableId value={createdChainFlow.id} max={FULL_ID_MAX} /></dd>
            <dt>Chained trigger</dt><dd><CopyableId value={createdChainTrigger.id} max={FULL_ID_MAX} /></dd>
            <dt>Chained trigger URL</dt>
            <dd style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <code className="monospace">{chainTriggerUrl}</code>
              <CopyButton value={chainTriggerUrl} />
            </dd>
          </div>
          <div className="grid-2">
            <div className="form-row">
            <label title="JSON body posted to the echo data flow trigger">Echo data flow input</label>
            <textarea rows={8} value={runInput} onChange={(e) => setRunInput(e.target.value)} style={{ fontFamily: 'var(--font-mono)' }} />
            </div>
            <div className="form-row">
              <label title="The chained data flow trigger uses GET and does not need request JSON">Chained data flow request body</label>
              <textarea rows={8} readOnly value={'No request body.\n\nThe random-number step generates the first value, then the double-number step returns the final output.'} style={{ fontFamily: 'var(--font-mono)' }} />
            </div>
          </div>
        </div>
      )}

      {stepIndex === 5 && (
        <div>
          <WizardExplanation
            title="First flows are ready"
            what="Tempo created an echo flow and a chained random doubled flow, then ran both through HTTP triggers."
            why="These are the same objects you will use for real automation: steps do work, flows orchestrate steps, artifacts version external code, triggers start flows, and runs show what happened."
            how="POST JSON to the echo trigger or GET the chained trigger whenever you want to run them again. The chained trigger response body is the output from the double-number step."
          />
          <div className="summary-tiles">
            <div className="summary-tile"><div className="label">Steps</div><div className="value">3</div></div>
            <div className="summary-tile"><div className="label">Flows</div><div className="value">2</div></div>
            <div className="summary-tile"><div className="label">Triggers</div><div className="value">2</div></div>
            <div className="summary-tile"><div className="label">Echo run</div><div className="value">{runDetails?.state || createdRun?.state || triggerResponseHeaders?.['x-run-state'] || '-'}</div></div>
            <div className="summary-tile"><div className="label">Chained run</div><div className="value">{chainRunDetails?.state || createdChainRun?.state || chainTriggerResponseHeaders?.['x-run-state'] || '-'}</div></div>
          </div>
          <section className="details-section">
            <div className="details-section-header" style={{ cursor: 'default' }}>Echo data flow invocation</div>
            <div className="details-section-body">
              <dl className="details-kv">
                <dt>Step ID</dt><dd><CopyableId value={createdStep?.id} max={FULL_ID_MAX} /></dd>
                <dt>Artifact ID</dt><dd><CopyableId value={createdArtifact?.id} max={FULL_ID_MAX} /></dd>
                <dt>Flow ID</dt><dd><CopyableId value={createdFlow?.id} max={FULL_ID_MAX} /></dd>
                <dt>Trigger ID</dt><dd><CopyableId value={createdTrigger?.id} max={FULL_ID_MAX} /></dd>
                <dt>Run ID</dt><dd><CopyableId value={createdRun?.id} max={FULL_ID_MAX} /></dd>
                <dt>Run state</dt><dd>{runDetails?.state || createdRun?.state || triggerResponseHeaders?.['x-run-state'] || '-'}</dd>
                <dt>Queued</dt><dd>{formatTime(runDetails?.createdUtc || createdRun?.createdUtc || triggerResponseHeaders?.['x-run-created-utc'])}</dd>
                <dt>Started</dt><dd>{formatTime(runDetails?.startedUtc || createdRun?.startedUtc || triggerResponseHeaders?.['x-run-started-utc'])}</dd>
                <dt>Completed</dt><dd>{formatTime(runDetails?.completedUtc || createdRun?.completedUtc || triggerResponseHeaders?.['x-run-completed-utc'])}</dd>
                <dt>Runtime</dt><dd>{triggerResponseHeaders?.['x-runtime-ms'] ? triggerResponseHeaders['x-runtime-ms'] + ' ms' : '-'}</dd>
                <dt>Trigger URL</dt>
                <dd style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                  <code className="monospace">{triggerUrl}</code>
                  <CopyButton value={triggerUrl} />
                </dd>
              </dl>
              <div className="form-row" style={{ marginTop: 'var(--spacing-md)' }}>
                <label>Run echo flow yourself ({runCommand.label})</label>
                <div className="command-copy-row">
                  <pre className="code-block">{runCommand.command}</pre>
                  <CopyButton value={runCommand.command} title="Copy command" />
                </div>
              </div>
              <div className="form-row" style={{ marginTop: 'var(--spacing-md)' }}>
                <label>Echo response body</label>
                <pre className="code-block">{triggerResponseBody || runDetails?.outputData || 'null'}</pre>
              </div>
              <div className="form-row" style={{ marginTop: 'var(--spacing-md)' }}>
                <label>Echo response headers</label>
                <pre className="code-block">{responseHeaderText || '(none)'}</pre>
              </div>
            </div>
          </section>
          <section className="details-section">
            <div className="details-section-header" style={{ cursor: 'default' }}>Chained data flow invocation</div>
            <div className="details-section-body">
              <dl className="details-kv">
                <dt>Random step ID</dt><dd><CopyableId value={createdChainSteps.random?.id} max={FULL_ID_MAX} /></dd>
                <dt>Double step ID</dt><dd><CopyableId value={createdChainSteps.double?.id} max={FULL_ID_MAX} /></dd>
                <dt>Random artifact ID</dt><dd><CopyableId value={createdChainArtifacts.random?.id} max={FULL_ID_MAX} /></dd>
                <dt>Double artifact ID</dt><dd><CopyableId value={createdChainArtifacts.double?.id} max={FULL_ID_MAX} /></dd>
                <dt>Flow ID</dt><dd><CopyableId value={createdChainFlow?.id} max={FULL_ID_MAX} /></dd>
                <dt>Trigger ID</dt><dd><CopyableId value={createdChainTrigger?.id} max={FULL_ID_MAX} /></dd>
                <dt>Run ID</dt><dd><CopyableId value={createdChainRun?.id} max={FULL_ID_MAX} /></dd>
                <dt>Run state</dt><dd>{chainRunDetails?.state || createdChainRun?.state || chainTriggerResponseHeaders?.['x-run-state'] || '-'}</dd>
                <dt>Queued</dt><dd>{formatTime(chainRunDetails?.createdUtc || createdChainRun?.createdUtc || chainTriggerResponseHeaders?.['x-run-created-utc'])}</dd>
                <dt>Started</dt><dd>{formatTime(chainRunDetails?.startedUtc || createdChainRun?.startedUtc || chainTriggerResponseHeaders?.['x-run-started-utc'])}</dd>
                <dt>Completed</dt><dd>{formatTime(chainRunDetails?.completedUtc || createdChainRun?.completedUtc || chainTriggerResponseHeaders?.['x-run-completed-utc'])}</dd>
                <dt>Runtime</dt><dd>{chainTriggerResponseHeaders?.['x-runtime-ms'] ? chainTriggerResponseHeaders['x-runtime-ms'] + ' ms' : '-'}</dd>
                <dt>Trigger URL</dt>
                <dd style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                  <code className="monospace">{chainTriggerUrl}</code>
                  <CopyButton value={chainTriggerUrl} />
                </dd>
              </dl>
              <div className="form-row" style={{ marginTop: 'var(--spacing-md)' }}>
                <label>Run chained flow yourself ({chainRunCommand.label})</label>
                <div className="command-copy-row">
                  <pre className="code-block">{chainRunCommand.command}</pre>
                  <CopyButton value={chainRunCommand.command} title="Copy command" />
                </div>
              </div>
              <div className="form-row" style={{ marginTop: 'var(--spacing-md)' }}>
                <label>Chained response body</label>
                <pre className="code-block">{chainTriggerResponseBody || chainRunDetails?.outputData || 'null'}</pre>
              </div>
              <div className="form-row" style={{ marginTop: 'var(--spacing-md)' }}>
                <label>Chained response headers</label>
                <pre className="code-block">{chainResponseHeaderText || '(none)'}</pre>
              </div>
            </div>
          </section>
        </div>
      )}
    </Modal>
  );
}

function WizardExplanation({ title, what, why, how }) {
  return (
    <section className="wizard-explanation">
      <h3>{title}</h3>
      <div className="wizard-explanation-grid">
        <div><strong>What</strong><span>{what}</span></div>
        <div><strong>Why</strong><span>{why}</span></div>
        <div><strong>How</strong><span>{how}</span></div>
      </div>
    </section>
  );
}

function SourceStepEditor({ title, language, form, setForm }) {
  return (
    <section className="wizard-explanation" style={{ marginTop: 'var(--spacing-md)' }}>
      <h3>{title}</h3>
      <div className="grid-2">
        <div className="form-row">
          <label title="Stable key referenced by the flow transition graph">Execution key</label>
          <input value={form.executionKey} onChange={(e) => setForm({ ...form, executionKey: e.target.value })} />
        </div>
        <div className="form-row">
          <label title="Display name for the generated step">Step name</label>
          <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </div>
      </div>
      <div className="form-row">
        <label title="Description shown in the dashboard">Description</label>
        <input value={form.description || ''} onChange={(e) => setForm({ ...form, description: e.target.value })} />
      </div>
      <div className="grid-2">
        <div className="form-row">
          <label title="Source file name stored in the generated artifact">File name</label>
          <input value={form.fileName} onChange={(e) => setForm({ ...form, fileName: e.target.value })} />
        </div>
        <div className="form-row">
          <label title="Generated artifact display name">Artifact name</label>
          <input value={form.artifactName} onChange={(e) => setForm({ ...form, artifactName: e.target.value })} />
        </div>
      </div>
      <div className="grid-2">
        <div className="form-row">
          <label title="Artifact entrypoint name">Entrypoint</label>
          <input value={form.entrypoint} onChange={(e) => setForm({ ...form, entrypoint: e.target.value })} />
        </div>
        {language === 'CSharp' ? (
          <div className="form-row">
            <label title="C# handler type implementing Tempo.Protocol.ITempoStepHandler">Handler type</label>
            <input value={form.handlerType} onChange={(e) => setForm({ ...form, handlerType: e.target.value })} />
          </div>
        ) : (
          <div className="form-row">
            <label title="Function/export called when the step runs">Function</label>
            <input value={form.function} onChange={(e) => setForm({ ...form, function: e.target.value })} />
          </div>
        )}
      </div>
      <div className="form-row">
        <label title="Complete source file contents for this step">Source code</label>
        <textarea rows={12} value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} spellCheck={false} style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }} />
      </div>
    </section>
  );
}

function WizardFooter({ stepIndex, busy, canCreateStep, onClose, onStart, onStep, onFlow, onTrigger, onRun }) {
  if (stepIndex === 0) {
    return (
      <>
        <button className="button-secondary" onClick={onClose}>Skip setup</button>
        <button className="button-primary" onClick={onStart}>Start setup</button>
      </>
    );
  }
  if (stepIndex === 1) {
    return (
      <>
        <button className="button-secondary" onClick={onClose}>Skip setup</button>
        <button className="button-primary" onClick={onStep} disabled={busy || !canCreateStep}>{busy ? 'Creating...' : 'Create steps'}</button>
      </>
    );
  }
  if (stepIndex === 2) {
    return (
      <>
        <button className="button-secondary" onClick={onClose}>Close</button>
        <button className="button-primary" onClick={onFlow} disabled={busy}>{busy ? 'Creating...' : 'Create flows'}</button>
      </>
    );
  }
  if (stepIndex === 3) {
    return (
      <>
        <button className="button-secondary" onClick={onClose}>Close</button>
        <button className="button-primary" onClick={onTrigger} disabled={busy}>{busy ? 'Creating...' : 'Create triggers'}</button>
      </>
    );
  }
  if (stepIndex === 4) {
    return (
      <>
        <button className="button-secondary" onClick={onClose}>Close</button>
        <button className="button-primary" onClick={onRun} disabled={busy}>{busy ? 'Running...' : 'Run triggers'}</button>
      </>
    );
  }
  return <button className="button-primary" onClick={onClose}>Done</button>;
}

export default SetupWizard;
