namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
#if NET10_0
    using SyslogLogging;
#endif
    using Tempo.Core;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;
#if NET10_0
    using Tempo.Core.Settings;
    using Tempo.Server;
    using Tempo.Server.Serialization;
#endif
    using Touchstone.Core;

    public static class RuntimeConfigSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RuntimeConfig",
                displayName: "Runtime config serialization and persistence",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("RuntimeConfig", "RuntimeKeyValidation", "Runtime keys use bounded dotted token format", async _ =>
                    {
                        await Task.CompletedTask;
                        Assert2.Equal("External.Rest", new RuntimeKey(" External.Rest ").ToString(), "trimmed");
                        Assert2.Throws<ArgumentException>(() => new RuntimeKey("External_Rest"), "dot required and underscore rejected");
                        Assert2.Throws<ArgumentException>(() => new RuntimeKey("External."), "empty token rejected");
                        Assert2.Throws<ArgumentOutOfRangeException>(() => new RuntimeKey("A." + new string('B', RuntimeKey.MaxLength)), "bounded length");
                    }),
                    new TestCaseDescriptor("RuntimeConfig", "PolymorphicRoundTrip", "Every default runtime config round-trips with runtimeKey discriminator", async _ =>
                    {
                        await Task.CompletedTask;
                        StepRuntimeConfig[] configs =
                        {
                            new BuiltinClassRuntimeConfig { Identifier = "class_step", TypeName = "Steps.Validate" },
                            new BuiltinMethodRuntimeConfig { Identifier = "method_step", MethodName = "Run" },
                            new BuiltinUnknownRuntimeConfig { Identifier = "legacy_step" },
                            new ExternalRestRuntimeConfig { Method = "POST", Url = "https://example.com", TimeoutMs = 1000 },
                            new LegacyInlineRestRuntimeConfig { Method = "GET", Url = "https://example.com", TimeoutMs = 1000 },
                            new ArtifactProcessRuntimeConfig { ArtifactId = "art_1", ArtifactVersion = "1" },
                            new ArtifactPythonRuntimeConfig { ArtifactId = "art_1", ArtifactVersion = "1", Module = "handler" },
                            new ArtifactJavaScriptRuntimeConfig { ArtifactId = "art_1", ArtifactVersion = "1", Module = "handler.js" },
                            new ArtifactDotnetProcessRuntimeConfig { ArtifactId = "art_1", ArtifactVersion = "1" },
                            new HostExecutableRuntimeConfig { AllowListKey = "tool" }
                        };

                        foreach (StepRuntimeConfig config in configs)
                        {
                            string json = JsonSerializer.Serialize<StepRuntimeConfig>(config, StepRuntimeSerialization.Options);
                            Assert2.True(json.Contains("\"runtimeKey\":\"" + config.RuntimeKey + "\"", StringComparison.Ordinal), "runtime discriminator for " + config.RuntimeKey);
                            StepRuntimeConfig? read = JsonSerializer.Deserialize<StepRuntimeConfig>(json, StepRuntimeSerialization.Options);
                            Assert2.NotNull(read, "read " + config.RuntimeKey);
                            Assert2.Equal(config.GetType(), read!.GetType(), "type " + config.RuntimeKey);
                        }
                    }),
                    new TestCaseDescriptor("RuntimeConfig", "UnknownDiscriminatorFails", "Unknown runtime discriminators fail deserialization", async _ =>
                    {
                        await Task.CompletedTask;
                        Assert2.Throws<JsonException>(() =>
                        {
                            JsonSerializer.Deserialize<StepRuntimeConfig>("{\"runtimeKey\":\"External.Nope\"}", StepRuntimeSerialization.Options);
                        }, "unknown discriminator");
                    }),
                    new TestCaseDescriptor("RuntimeConfig", "NoJsonElementConfigProperties", "Fixed runtime config DTOs do not expose JsonElement properties", async _ =>
                    {
                        await Task.CompletedTask;
                        Type[] configTypes =
                        {
                            typeof(BuiltinClassRuntimeConfig),
                            typeof(BuiltinMethodRuntimeConfig),
                            typeof(BuiltinUnknownRuntimeConfig),
                            typeof(ExternalRestRuntimeConfig),
                            typeof(LegacyInlineRestRuntimeConfig),
                            typeof(ArtifactProcessRuntimeConfig),
                            typeof(ArtifactPythonRuntimeConfig),
                            typeof(ArtifactJavaScriptRuntimeConfig),
                            typeof(ArtifactDotnetProcessRuntimeConfig),
                            typeof(HostExecutableRuntimeConfig)
                        };
                        foreach (Type type in configTypes)
                        {
                            bool hasJsonElement = type.GetProperties().Any(p => p.PropertyType == typeof(JsonElement));
                            Assert2.False(hasJsonElement, type.Name + " has no JsonElement properties");
                        }
                    }),
                    new TestCaseDescriptor("RuntimeConfig", "StepRuntimeColumnsRoundTrip", "Step runtime columns round-trip through StepMethods", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            StepRecord created = await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                ExecutionKey = "call_api",
                                Name = "Call API",
                                RuntimeKey = StepRuntimeKeys.ExternalRest,
                                RuntimeConfig = new ExternalRestRuntimeConfig { Method = "POST", Url = "https://example.com/orders", TimeoutMs = 1234 },
                                ContractType = StepContractTypeEnum.Schema,
                                InputSchema = "{\"type\":\"object\"}",
                                OutputSchema = "{\"type\":\"object\"}",
                                ValidateInput = true,
                                ValidateOutput = true,
                                ArtifactId = "art_unused",
                                ArtifactVersion = "v1"
                            }, ct);

                            StepRecord? read = await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "call_api", ct);
                            Assert2.NotNull(read, "read");
                            Assert2.Equal(created.Id, read!.Id, "id");
                            Assert2.Equal(StepRuntimeKeys.ExternalRest, read.RuntimeKey, "runtime key");
                            Assert2.Equal(typeof(ExternalRestRuntimeConfig), read.RuntimeConfig!.GetType(), "runtime config type");
                            Assert2.Equal(StepContractTypeEnum.Schema, read.ContractType, "contract type");
                            Assert2.True(read.ValidateInput, "validate input");
                            Assert2.True(read.ValidateOutput, "validate output");
                            Assert2.Equal("art_unused", read.ArtifactId!, "artifact id");
                            Assert2.Equal("v1", read.ArtifactVersion!, "artifact version");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RuntimeConfig", "LegacyRestHydratesRuntimeConfig", "Legacy Rest rows hydrate to External.Rest runtime config", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            StepRecord created = await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                Name = "Legacy REST",
                                StepType = PersistedStepTypeEnum.Rest,
                                Rest = new Tempo.RestStepConfiguration { Method = "GET", Url = "https://example.com", TimeoutMs = 5000 }
                            }, ct);

                            StepRecord? read = await driver.Steps.ReadAsync(tenant.Id, created.Id, ct);
                            Assert2.NotNull(read, "read");
                            Assert2.Equal(StepRuntimeKeys.ExternalRest, read!.RuntimeKey, "runtime key");
                            Assert2.Equal(typeof(ExternalRestRuntimeConfig), read.RuntimeConfig!.GetType(), "runtime config type");
                            ExternalRestRuntimeConfig config = (ExternalRestRuntimeConfig)read.RuntimeConfig;
                            Assert2.Equal("https://example.com", config.Url, "url");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
#if NET10_0
                    ,
                    new TestCaseDescriptor("RuntimeConfig", "StepRoutesUseTypedRuntimeConfigRequests", "Step routes create and update steps with typed runtime config request DTOs", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.AdminApiKey = "step-route-key";
                            settings.RequestHistory.Enabled = false;
                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Route Tenant" }, ct);
                            using HttpClient client = new HttpClient();
                            client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                            client.DefaultRequestHeaders.Add(Constants.HeaderApiKey, "step-route-key");

                            string createBody = "{\"executionKey\":\"call_api\",\"name\":\"Call API\",\"runtimeKey\":\"External.Rest\",\"runtimeConfig\":{\"runtimeKey\":\"External.Rest\",\"method\":\"POST\",\"url\":\"https://example.com/orders\",\"headers\":{\"x-test\":\"1\"},\"timeoutMs\":1200},\"contractType\":\"Loose\",\"maxRuntimeMs\":0,\"active\":true}";
                            HttpResponseMessage createResp = await client.PostAsync(
                                "/v1.0/tenants/" + tenant.Id + "/steps",
                                new StringContent(createBody, Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.Created, createResp.StatusCode, "created");
                            string createJson = await createResp.Content.ReadAsStringAsync(ct);
                            Assert2.True(!createJson.Contains("\"stepType\"", StringComparison.Ordinal), "step response omits legacy stepType");
                            Assert2.True(!createJson.Contains("\"rest\"", StringComparison.Ordinal), "step response omits legacy rest config");
                            StepResponse created = Deserialize<StepResponse>(createJson);
                            Assert2.Equal(StepRuntimeKeys.ExternalRest, created.RuntimeKey, "created runtime key");
                            Assert2.Equal(typeof(ExternalRestRuntimeConfig), created.RuntimeConfig!.GetType(), "created config type");

                            HttpResponseMessage listResp = await client.GetAsync("/v1.0/tenants/" + tenant.Id + "/steps", ct);
                            Assert2.Equal(HttpStatusCode.OK, listResp.StatusCode, "list steps");
                            StepListResponse list = Deserialize<StepListResponse>(await listResp.Content.ReadAsStringAsync(ct));
                            Assert2.Equal(1, list.TotalCount, "list total");
                            Assert2.Equal(created.Id, list.Items[0].Id, "list response item");

                            string updateBody = "{\"name\":\"Call API v2\",\"runtimeKey\":\"External.Rest\",\"runtimeConfig\":{\"runtimeKey\":\"External.Rest\",\"method\":\"PUT\",\"url\":\"https://example.com/orders/{id}\",\"headers\":{},\"timeoutMs\":1500},\"active\":false}";
                            HttpResponseMessage updateResp = await client.PutAsync(
                                "/v1.0/tenants/" + tenant.Id + "/steps/" + created.Id,
                                new StringContent(updateBody, Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.OK, updateResp.StatusCode, "updated");
                            StepResponse updated = Deserialize<StepResponse>(await updateResp.Content.ReadAsStringAsync(ct));
                            Assert2.Equal("Call API v2", updated.Name, "updated name");
                            Assert2.False(updated.Active, "updated active");
                            ExternalRestRuntimeConfig updatedConfig = (ExternalRestRuntimeConfig)updated.RuntimeConfig!;
                            Assert2.Equal("PUT", updatedConfig.Method, "updated method");
                            Assert2.Equal(1500, updatedConfig.TimeoutMs, "updated timeout");

                            string invalidBody = "{\"executionKey\":\"bad_rest\",\"name\":\"Bad REST\",\"runtimeKey\":\"External.Rest\",\"runtimeConfig\":{\"runtimeKey\":\"External.Rest\",\"method\":\"GET\",\"url\":\"\",\"timeoutMs\":1000}}";
                            HttpResponseMessage invalidResp = await client.PostAsync(
                                "/v1.0/tenants/" + tenant.Id + "/steps",
                                new StringContent(invalidBody, Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.BadRequest, invalidResp.StatusCode, "invalid runtime config rejected");

                            HttpResponseMessage runtimeResp = await client.GetAsync("/v1.0/runtimes/External.Rest", ct);
                            Assert2.Equal(HttpStatusCode.OK, runtimeResp.StatusCode, "read runtime descriptor");
                            string runtimeJson = await runtimeResp.Content.ReadAsStringAsync(ct);
                            Assert2.True(runtimeJson.Contains("\"runtimeKey\":\"External.Rest\"", StringComparison.Ordinal), "runtime descriptor key");
                        }
                        finally
                        {
                            try { server?.Dispose(); } catch { }
                            await TempTestStore.DisposeAsync(driver);
                        }
                    }),
                    new TestCaseDescriptor("RuntimeConfig", "OpenApiDocumentsTypedRuntimeRequests", "OpenAPI documents typed runtime step and validation request bodies", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.AdminApiKey = "openapi-route-key";
                            settings.RequestHistory.Enabled = false;
                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            using HttpClient client = new HttpClient();
                            client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                            client.DefaultRequestHeaders.Add(Constants.HeaderApiKey, "openapi-route-key");

                            HttpResponseMessage response = await client.GetAsync("/openapi.json", ct);
                            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "openapi response");
                            string json = await response.Content.ReadAsStringAsync(ct);
                            using JsonDocument doc = JsonDocument.Parse(json);
                            JsonElement root = doc.RootElement;
                            JsonElement paths = root.GetProperty("paths");

                            JsonElement createStep = paths.GetProperty("/v1.0/tenants/{tenantId}/steps").GetProperty("post");
                            JsonElement createSchema = createStep.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
                            JsonElement runtimeConfig = createSchema.GetProperty("properties").GetProperty("runtimeConfig");
                            Assert2.True(runtimeConfig.TryGetProperty("oneOf", out JsonElement oneOf), "step create runtimeConfig oneOf schema");
                            Assert2.Equal(10, oneOf.GetArrayLength(), "runtimeConfig branch count");
                            Assert2.True(runtimeConfig.TryGetProperty("discriminator", out JsonElement discriminator), "step create runtimeConfig discriminator");
                            Assert2.Equal("runtimeKey", discriminator.GetProperty("propertyName").GetString(), "runtimeConfig discriminator property");
                            JsonElement mapping = discriminator.GetProperty("mapping");
                            Assert2.Equal("#/components/schemas/ExternalRestRuntimeConfig", mapping.GetProperty("External.Rest").GetString(), "External.Rest mapping");
                            Assert2.Equal("#/components/schemas/ArtifactProcessRuntimeConfig", mapping.GetProperty("Artifact.Process").GetString(), "Artifact.Process mapping");
                            Assert2.Equal("#/components/schemas/ArtifactPythonRuntimeConfig", mapping.GetProperty("Artifact.Python").GetString(), "Artifact.Python mapping");
                            Assert2.Equal("#/components/schemas/ArtifactJavaScriptRuntimeConfig", mapping.GetProperty("Artifact.JavaScript").GetString(), "Artifact.JavaScript mapping");
                            Assert2.Equal("#/components/schemas/ArtifactDotnetProcessRuntimeConfig", mapping.GetProperty("Artifact.DotnetProcess").GetString(), "Artifact.DotnetProcess mapping");
                            Assert2.Equal("#/components/schemas/HostExecutableRuntimeConfig", mapping.GetProperty("Host.Executable").GetString(), "Host.Executable mapping");
                            Assert2.True(!runtimeConfig.TryGetProperty("properties", out _), "runtimeConfig no flattened properties");

                            JsonElement schemas = root.GetProperty("components").GetProperty("schemas");
                            string[] componentNames =
                            {
                                "BuiltinClassRuntimeConfig",
                                "BuiltinMethodRuntimeConfig",
                                "BuiltinUnknownRuntimeConfig",
                                "ExternalRestRuntimeConfig",
                                "LegacyInlineRestRuntimeConfig",
                                "ArtifactProcessRuntimeConfig",
                                "ArtifactPythonRuntimeConfig",
                                "ArtifactJavaScriptRuntimeConfig",
                                "ArtifactDotnetProcessRuntimeConfig",
                                "HostExecutableRuntimeConfig"
                            };
                            foreach (string componentName in componentNames)
                            {
                                Assert2.True(schemas.TryGetProperty(componentName, out _), componentName + " component schema");
                            }

                            AssertRequired(schemas.GetProperty("ExternalRestRuntimeConfig"), "runtimeKey", "method", "url", "timeoutMs");
                            AssertRequired(schemas.GetProperty("ArtifactProcessRuntimeConfig"), "runtimeKey", "artifactId");
                            AssertRequired(schemas.GetProperty("ArtifactPythonRuntimeConfig"), "runtimeKey", "artifactId", "function");
                            AssertRequired(schemas.GetProperty("ArtifactJavaScriptRuntimeConfig"), "runtimeKey", "artifactId", "function");
                            AssertRequired(schemas.GetProperty("ArtifactDotnetProcessRuntimeConfig"), "runtimeKey", "artifactId");
                            AssertRequired(schemas.GetProperty("HostExecutableRuntimeConfig"), "runtimeKey", "allowListKey");
                            AssertSingleRuntimeKey(schemas.GetProperty("ExternalRestRuntimeConfig"), "External.Rest");
                            AssertSingleRuntimeKey(schemas.GetProperty("ArtifactProcessRuntimeConfig"), "Artifact.Process");
                            AssertSingleRuntimeKey(schemas.GetProperty("ArtifactPythonRuntimeConfig"), "Artifact.Python");
                            AssertSingleRuntimeKey(schemas.GetProperty("ArtifactJavaScriptRuntimeConfig"), "Artifact.JavaScript");
                            AssertSingleRuntimeKey(schemas.GetProperty("ArtifactDotnetProcessRuntimeConfig"), "Artifact.DotnetProcess");
                            AssertSingleRuntimeKey(schemas.GetProperty("HostExecutableRuntimeConfig"), "Host.Executable");

                            JsonElement validateRuntime = paths.GetProperty("/v1.0/tenants/{tenantId}/runtimes/validate").GetProperty("post");
                            JsonElement validateSchema = validateRuntime.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
                            Assert2.True(validateSchema.GetProperty("properties").TryGetProperty("runtimeKey", out _), "runtime validate runtimeKey schema");
                            JsonElement validateConfig = validateSchema.GetProperty("properties").GetProperty("config");
                            Assert2.True(validateConfig.TryGetProperty("oneOf", out _), "runtime validate config oneOf schema");
                            Assert2.True(validateConfig.TryGetProperty("discriminator", out _), "runtime validate config discriminator");
                            Assert2.True(paths.TryGetProperty("/v1.0/runtimes/{runtimeKey}", out _), "runtime descriptor route documented");
                            Assert2.True(paths.TryGetProperty("/v1.0/tenants/{tenantId}/steps/source", out _), "source step route documented");

                            JsonElement artifactCreate = paths.GetProperty("/v1.0/tenants/{tenantId}/artifacts").GetProperty("post");
                            JsonElement artifactCreateSchema = artifactCreate.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
                            Assert2.True(artifactCreateSchema.GetProperty("properties").TryGetProperty("name", out _), "artifact create request schema");
                            JsonElement artifactUpload = paths.GetProperty("/v1.0/tenants/{tenantId}/artifacts/{id}/versions").GetProperty("post");
                            Assert2.True(artifactUpload.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/octet-stream", out _), "artifact upload binary request body");
                            Assert2.True(paths.TryGetProperty("/v1.0/migrations/inline-rest", out _), "inline REST migration route documented");

                            static void AssertRequired(JsonElement schema, params string[] expected)
                            {
                                JsonElement required = schema.GetProperty("required");
                                foreach (string name in expected)
                                {
                                    bool found = false;
                                    foreach (JsonElement item in required.EnumerateArray())
                                    {
                                        if (item.GetString() == name) { found = true; break; }
                                    }
                                    Assert2.True(found, schema.GetProperty("description").GetString() + " requires " + name);
                                }
                            }

                            static void AssertSingleRuntimeKey(JsonElement schema, string expected)
                            {
                                JsonElement runtimeKey = schema.GetProperty("properties").GetProperty("runtimeKey");
                                JsonElement values = runtimeKey.GetProperty("enum");
                                Assert2.Equal(1, values.GetArrayLength(), expected + " runtimeKey enum length");
                                Assert2.Equal(expected, values[0].GetString(), expected + " runtimeKey enum");
                            }
                        }
                        finally
                        {
                            try { server?.Dispose(); } catch { }
                            await TempTestStore.DisposeAsync(driver);
                        }
                    })
#endif
                });
        }

#if NET10_0
        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static T Deserialize<T>(string json)
        {
            T? value = Serializer.Deserialize<T>(json);
            if (value == null) throw new InvalidOperationException("Could not deserialize " + typeof(T).Name + ": " + json);
            return value;
        }
#endif
    }
}
