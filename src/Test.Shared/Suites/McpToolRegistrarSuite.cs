namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Tempo.McpServer.Tools;
    using Touchstone.Core;
    using Voltaic.Core;

    /// <summary>
    /// Tests for the Voltaic MCP argument bridge that adapts transport-level
    /// <see cref="RpcParameters"/> into the <see cref="JsonElement"/>? shape the Tempo tool handlers consume.
    /// </summary>
    public static class McpToolRegistrarSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "McpToolRegistrar",
                displayName: "MCP tool argument marshaling",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("McpToolRegistrar", "ParsesPopulatedParameters", "Populated RpcParameters marshal into a JSON object element that preserves every property", async _ =>
                    {
                        await Task.CompletedTask;
                        RpcParameters parameters = RpcParameters.FromObject(new { id = "art_abc123", count = 7, enabled = true });

                        JsonElement? element = TempoToolRegistrar.ToJsonElement(parameters);

                        Assert2.NotNull(element, "populated parameters marshal to a value");
                        Assert2.Equal(JsonValueKind.Object, element!.Value.ValueKind, "marshaled element is a JSON object");
                        Assert2.Equal("art_abc123", element.Value.GetProperty("id").GetString()!, "string property preserved");
                        Assert2.Equal(7, element.Value.GetProperty("count").GetInt32(), "numeric property preserved");
                        Assert2.True(element.Value.GetProperty("enabled").GetBoolean(), "boolean property preserved");
                    }),
                    new TestCaseDescriptor("McpToolRegistrar", "ParsedElementSurvivesSourceDocument", "Marshaled element stays readable after the source JSON document is released (defensive clone)", async _ =>
                    {
                        await Task.CompletedTask;
                        JsonElement? element = TempoToolRegistrar.ToJsonElement(RpcParameters.FromObject(new { nested = new { value = "deep" } }));

                        Assert2.NotNull(element, "nested parameters marshal to a value");
                        // Reading a nested property proves the returned element is not backed by a disposed JsonDocument.
                        Assert2.Equal("deep", element!.Value.GetProperty("nested").GetProperty("value").GetString()!, "nested property readable after parse");
                    }),
                    new TestCaseDescriptor("McpToolRegistrar", "NullParametersMarshalToNull", "A null RpcParameters (no arguments supplied by the transport) marshals to null rather than throwing", async _ =>
                    {
                        await Task.CompletedTask;
                        JsonElement? element = TempoToolRegistrar.ToJsonElement(null);
                        Assert2.IsNull(element, "null parameters marshal to null");
                    }),
                    new TestCaseDescriptor("McpToolRegistrar", "EmptyRawJsonMarshalsToNull", "RpcParameters carrying empty raw JSON marshal to null instead of a parse failure", async _ =>
                    {
                        await Task.CompletedTask;
                        JsonElement? element = TempoToolRegistrar.ToJsonElement(new RpcParameters(string.Empty));
                        Assert2.IsNull(element, "empty raw JSON marshals to null");
                    })
                });
        }
    }
}
