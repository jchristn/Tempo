# Artifact Manifest Reference

Artifact packages may include `tempo.step.json` at the zip root. The manifest
describes the runtime and named entrypoints available in the package.

```json
{
  "manifestVersion": "1",
  "runtimeKey": "Artifact.Process",
  "supportedProtocolVersions": ["1.0"],
  "defaultEntrypoint": "main",
  "entrypoints": {
    "main": {
      "command": "fixture/Test.ArtifactFixture.exe",
      "args": [],
      "environmentAllowList": [],
      "inputSchema": "{}",
      "outputSchema": "{}"
    }
  },
  "environmentAllowList": [],
  "metadata": {}
}
```

For `Artifact.Process`, an entrypoint needs `command`. For
`Artifact.DotnetProcess`, the entrypoint `command` must point at a package-local
`.dll` and `handlerType` must identify the Tempo SDK handler type implemented by
the artifact. For `Artifact.Python`, an entrypoint uses `module` and `function`;
tenant config can override those fields.

Paths must be relative artifact paths. Absolute paths, `..` traversal, symlink
escape attempts, and archive entries outside the extraction root are rejected.
Environment allowlists contain variable names only. Values are read from the
server environment at execution time and are redacted from diagnostics.
