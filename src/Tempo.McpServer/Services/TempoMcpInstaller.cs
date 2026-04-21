namespace Tempo.McpServer.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Tempo.McpServer.Settings;

    /// <summary>
    /// Installs Tempo MCP configuration for Claude-compatible clients.
    /// </summary>
    public static class TempoMcpInstaller
    {
        /// <summary>Install or preview Claude configuration.</summary>
        /// <param name="settings">Settings.</param>
        /// <param name="dryRun">True to preview only.</param>
        public static void Install(TempoMcpServerSettings settings, bool dryRun)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            string mcpUrl = "http://" + settings.Http.Hostname + ":" + settings.Http.Port + settings.Http.RpcPath;
            string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string claudeJsonPath = Path.Combine(homeDirectory, ".claude.json");

            JsonObject claudeJsonRoot = ReadJsonObject(claudeJsonPath);
            JsonObject mcpServersNode = GetOrCreateObject(claudeJsonRoot, "mcpServers");
            mcpServersNode["tempo"] = new JsonObject
            {
                ["type"] = "http",
                ["url"] = mcpUrl
            };

            JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
            string claudeJsonOutput = claudeJsonRoot.ToJsonString(options);

            Console.WriteLine(dryRun ? "[DRY RUN] Tempo MCP install preview" : "Installing Tempo MCP configuration");
            Console.WriteLine();
            Console.WriteLine("Claude Code configuration: " + claudeJsonPath);
            if (dryRun)
            {
                Console.WriteLine(claudeJsonOutput);
                Console.WriteLine();
            }
            else
            {
                File.WriteAllText(claudeJsonPath, claudeJsonOutput);
                Console.WriteLine("Updated " + claudeJsonPath);
                Console.WriteLine();
            }

            string agentsDirectory = Path.Combine(homeDirectory, ".claude", "agents");
            string agentPath = Path.Combine(agentsDirectory, "tempo.md");
            string agentContent = BuildAgentContent();

            if (dryRun)
            {
                Console.WriteLine("[DRY RUN] Would write agent file " + agentPath);
                Console.WriteLine(agentContent);
                Console.WriteLine();
            }
            else
            {
                if (!Directory.Exists(agentsDirectory)) Directory.CreateDirectory(agentsDirectory);
                File.WriteAllText(agentPath, agentContent);
                Console.WriteLine("Updated " + agentPath);
                Console.WriteLine();
            }

            Console.WriteLine("MCP endpoint: " + mcpUrl);
            Console.WriteLine("Restart Claude Code after changing MCP configuration");
        }

        private static JsonObject ReadJsonObject(string path)
        {
            if (!File.Exists(path)) return new JsonObject();
            string content = File.ReadAllText(path);
            JsonNode? root = JsonNode.Parse(content);
            return root as JsonObject ?? new JsonObject();
        }

        private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
        {
            JsonObject? existing = parent[propertyName] as JsonObject;
            if (existing != null) return existing;
            JsonObject created = new JsonObject();
            parent[propertyName] = created;
            return created;
        }

        private static string BuildAgentContent()
        {
            return "---" + Environment.NewLine +
                "name: tempo" + Environment.NewLine +
                "description: Tempo workflow automation agent for creating, inspecting, and running flows, steps, triggers, runtimes, and mutable artifacts." + Environment.NewLine +
                "allowedTools:" + Environment.NewLine +
                "  - mcp__tempo__*" + Environment.NewLine +
                "---" + Environment.NewLine +
                Environment.NewLine +
                "You are a Tempo workflow automation assistant. Use Tempo MCP tools to inspect tenants, steps, flows, triggers, runs, runtimes, and artifacts." + Environment.NewLine +
                Environment.NewLine +
                "## Workflow" + Environment.NewLine +
                Environment.NewLine +
                "1. Confirm the tenant before making tenant-scoped changes." + Environment.NewLine +
                "2. Inspect existing steps and registered built-in steps before creating new flows." + Environment.NewLine +
                "3. Create or update steps, then create flows that reference those steps." + Environment.NewLine +
                "4. Add triggers for user-driven execution." + Environment.NewLine +
                "5. Run flows through triggers or queued flow runs and inspect run metadata." + Environment.NewLine +
                "6. Use mutable artifact file tools when a step package needs code changes." + Environment.NewLine +
                Environment.NewLine +
                "## Guidelines" + Environment.NewLine +
                Environment.NewLine +
                "- Prefer typed tools over tempo_request when available." + Environment.NewLine +
                "- Use tempo_request only for Tempo API endpoints not yet wrapped by a typed MCP tool." + Environment.NewLine +
                "- Treat process-backed runtime code as trusted code unless the Tempo deployment provides isolation and quotas." + Environment.NewLine;
        }
    }
}
