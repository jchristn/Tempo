# Artifact Python Quickstart

`Artifact.Python` runs a Python module from an uploaded artifact through a
generated Tempo protocol shim. Your function receives the request dictionary and
returns JSON-serializable data.

`handler.py`:

```python
def run(req):
    order = req["data"]
    return {
        "orderId": order["orderId"],
        "score": 100
    }
```

`tempo.step.json`:

```json
{
  "manifestVersion": "1",
  "runtimeKey": "Artifact.Python",
  "supportedProtocolVersions": ["1.0"],
  "defaultEntrypoint": "main",
  "entrypoints": {
    "main": {
      "module": "handler",
      "function": "run"
    }
  }
}
```

Zip both files, create an artifact, upload a version, then create a step:

```json
{
  "executionKey": "score_order",
  "name": "Score order",
  "runtimeKey": "Artifact.Python",
  "runtimeConfig": {
    "runtimeKey": "Artifact.Python",
    "artifactId": "art_...",
    "artifactVersion": "latest",
    "module": "handler",
    "function": "run"
  }
}
```

Dependency installation is disabled unless the operator enables it in runtime
settings. When enabled, Tempo creates a cached virtual environment from the
artifact's declared requirements file.

