namespace Tempo
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Logs;
    using Tempo.Runners;

    /// <summary>
    /// Manages both class-based steps (Step instances) and attribute-based code steps (methods decorated with [StepMethod]).
    /// Provides thread-safe storage, retrieval, and assembly scanning capabilities for step registration.
    /// </summary>
    public class StepManager
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

        /// <summary>
        /// Gets or sets the collection of registered class-based steps.
        /// Setting this property replaces all existing steps with the provided collection.
        /// </summary>
        public List<Step> Steps
        {
            get
            {
                lock (_Lock)
                {
                    return new List<Step>(_Steps.Values);
                }
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Steps));
                
                lock (_Lock)
                {
                    _Steps = new Dictionary<string, Step>();

                    foreach (Step step in value)
                    {
                        _Steps.Add(step.Identifier, step);
                    }
                }
            }
        }

        private readonly object _Lock = new object();
        private Dictionary<string, Step> _Steps = new Dictionary<string, Step>();
        private Dictionary<string, CodeAttributeStepInfo> _AttributeMethods = new Dictionary<string, CodeAttributeStepInfo>();
        private Logger _Logger = null;

        /// <summary>
        /// Initializes a new instance of the StepManager class.
        /// </summary>
        /// <param name="logger">Logger instance for logging (optional).</param>
        public StepManager(Logger logger = null)
        {
            _Logger = logger;
        }

        /// <summary>
        /// Retrieve all steps.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>List.</returns>
        public List<Step> All(string tenantId = null)
        {
            lock (_Lock)
            {
                if (tenantId == null)
                {
                    return new List<Step>(_Steps.Values);
                }
                else
                {
                    return new List<Step>(_Steps.Values.Where(s => s.TenantId.Equals(tenantId)));
                }
            }
        }

        /// <summary>
        /// Retrieve built-in registration metadata.
        /// </summary>
        /// <param name="executionKey">Optional execution key filter.</param>
        /// <param name="tenantId">Optional tenant scope filter. Global registrations are included.</param>
        /// <returns>Registered built-in step metadata.</returns>
        public List<BuiltinStepRegistration> Registrations(string executionKey = null, string tenantId = null)
        {
            lock (_Lock)
            {
                List<BuiltinStepRegistration> registrations = new List<BuiltinStepRegistration>();

                foreach (Step step in _Steps.Values)
                {
                    if (!MatchesExecutionKey(step.Identifier, executionKey)) continue;
                    if (!AppliesToTenant(step.TenantId, tenantId)) continue;
                    registrations.Add(BuildClassRegistration(step));
                }

                foreach (CodeAttributeStepInfo info in _AttributeMethods.Values)
                {
                    if (!MatchesExecutionKey(info.Identifier, executionKey)) continue;
                    if (!AppliesToTenant(info.TenantId, tenantId)) continue;
                    registrations.Add(BuildMethodRegistration(info));
                }

                return registrations.Select(r => r.Clone()).ToList();
            }
        }

        /// <summary>
        /// Retrieve by execution key.
        /// </summary>
        /// <param name="executionKey">Execution key.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>Step.</returns>
        public Step GetByExecutionKey(string executionKey, string tenantId = null)
        {
            if (String.IsNullOrEmpty(executionKey)) throw new ArgumentNullException(nameof(executionKey));

            lock (_Lock)
            {
                if (!String.IsNullOrEmpty(tenantId))
                {
                    if (_Steps.Values.Any(s => String.Equals(s.TenantId, tenantId, StringComparison.Ordinal) && s.Identifier.Equals(executionKey)))
                    {
                        return _Steps.Values.First(s => String.Equals(s.TenantId, tenantId, StringComparison.Ordinal) && s.Identifier.Equals(executionKey));
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    if (_Steps.Keys.Contains(executionKey))
                    {
                        return _Steps[executionKey];
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve by identifier. Compatibility shim for callers that still use legacy terminology.
        /// </summary>
        /// <param name="id">Execution key.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>Step.</returns>
        public Step GetByIdentifier(string id, string tenantId = null)
        {
            return GetByExecutionKey(id, tenantId);
        }

        /// <summary>
        /// Add a step.
        /// </summary>
        /// <param name="step">Step.</param>
        public void Add(Step step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));

            lock (_Lock)
            {
                _Steps.Add(step.Identifier, step);
            }
        }

        /// <summary>
        /// Register an attribute-based code step (method with [StepMethod] attribute).
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="method">Static method to invoke.</param>
        /// <param name="tenantId">Tenant identifier (optional).</param>
        /// <param name="maxRuntimeMs">Maximum runtime in milliseconds.</param>
        public void RegisterMethod(string identifier, MethodInfo method, string tenantId = null, int maxRuntimeMs = 0)
        {
            if (String.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));
            if (method == null) throw new ArgumentNullException(nameof(method));

            lock (_Lock)
            {
                string key = BuildMethodKey(identifier, tenantId);
                CodeAttributeStepInfo info = new CodeAttributeStepInfo
                {
                    Identifier = identifier,
                    Method = method,
                    TenantId = tenantId,
                    MaxRuntimeMs = maxRuntimeMs
                };
                _AttributeMethods[key] = info;
            }
        }

        /// <summary>
        /// Scan an assembly for methods decorated with [StepMethod] and register them.
        /// </summary>
        /// <param name="assembly">Assembly to scan.</param>
        /// <param name="defaultTenantId">Default tenant ID to use when attribute doesn't specify one.</param>
        /// <returns>Number of methods registered.</returns>
        public int ScanAssembly(Assembly assembly, string defaultTenantId = null)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            int count = 0;

            foreach (Type type in assembly.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    StepMethodAttribute attribute = method.GetCustomAttribute<StepMethodAttribute>();
                    if (attribute != null)
                    {
                        try
                        {
                            string tenantId = attribute.TenantId ?? defaultTenantId;
                            RegisterMethod(attribute.Identifier, method, tenantId, attribute.MaxRuntimeMs);
                            count++;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"Failed to register method '{method.DeclaringType?.FullName}.{method.Name}' as step '{attribute.Identifier}': {ex.Message}",
                                ex
                            );
                        }
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Scan the calling assembly for methods decorated with [StepMethod] and register them.
        /// </summary>
        /// <param name="defaultTenantId">Default tenant ID.</param>
        /// <returns>Number of methods registered.</returns>
        public int ScanCallingAssembly(string defaultTenantId = null)
        {
            return ScanAssembly(Assembly.GetCallingAssembly(), defaultTenantId);
        }

        /// <summary>
        /// Scan the entry assembly (main program) for methods decorated with [StepMethod] and register them.
        /// </summary>
        /// <param name="defaultTenantId">Default tenant ID.</param>
        /// <returns>Number of methods registered.</returns>
        public int ScanEntryAssembly(string defaultTenantId = null)
        {
            Assembly assembly = Assembly.GetEntryAssembly();
            if (assembly == null)
                throw new InvalidOperationException("Unable to determine entry assembly.");

            return ScanAssembly(assembly, defaultTenantId);
        }

        /// <summary>
        /// Get a step runner for the specified execution key and tenant.
        /// Checks both regular steps and attribute-based method steps.
        /// </summary>
        /// <param name="identifier">Step execution key.</param>
        /// <param name="tenantId">Tenant identifier (optional).</param>
        /// <returns>StepRunner, or null if not found.</returns>
        public StepRunner GetStepRunner(string identifier, string tenantId = null)
        {
            if (String.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));

            lock (_Lock)
            {
                // First check regular steps
                Step step = GetClassStep(identifier, tenantId);
                if (step != null)
                {
                    return new CodeStepRunner(step, _Logger);
                }

                // Then check attribute-based methods
                string key = BuildMethodKey(identifier, tenantId);
                if (_AttributeMethods.TryGetValue(key, out CodeAttributeStepInfo methodInfo))
                {
                    return new CodeAttributeStepRunner(methodInfo.Method, _Logger);
                }

                // If tenant-specific lookup failed, try global lookup
                if (!String.IsNullOrEmpty(tenantId))
                {
                    key = BuildMethodKey(identifier, null);
                    if (_AttributeMethods.TryGetValue(key, out methodInfo))
                    {
                        return new CodeAttributeStepRunner(methodInfo.Method, _Logger);
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Get max runtime for a step execution key (works for both regular and attribute-based steps).
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier (optional).</param>
        /// <returns>Max runtime in milliseconds, or 0 if not found or no timeout.</returns>
        public int GetMaxRuntimeMs(string identifier, string tenantId = null)
        {
            if (String.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));

            lock (_Lock)
            {
                // First check regular steps
                Step step = GetClassStep(identifier, tenantId);
                if (step != null)
                {
                    return step.MaxRuntimeMs;
                }

                // Then check attribute-based methods
                string key = BuildMethodKey(identifier, tenantId);
                if (_AttributeMethods.TryGetValue(key, out CodeAttributeStepInfo methodInfo))
                {
                    return methodInfo.MaxRuntimeMs;
                }

                // If tenant-specific lookup failed, try global lookup
                if (!String.IsNullOrEmpty(tenantId))
                {
                    key = BuildMethodKey(identifier, null);
                    if (_AttributeMethods.TryGetValue(key, out methodInfo))
                    {
                        return methodInfo.MaxRuntimeMs;
                    }
                }

                return 0;
            }
        }

        private string BuildMethodKey(string identifier, string tenantId)
        {
            if (String.IsNullOrEmpty(tenantId))
                return identifier;

            return $"{tenantId}::{identifier}";
        }

        private Step GetClassStep(string identifier, string tenantId)
        {
            Step step = GetByExecutionKey(identifier, tenantId);
            if (step != null) return step;

            if (!String.IsNullOrEmpty(tenantId) && _Steps.TryGetValue(identifier, out Step globalStep))
            {
                if (String.IsNullOrWhiteSpace(globalStep.TenantId) || String.Equals(globalStep.TenantId, "global", StringComparison.OrdinalIgnoreCase))
                {
                    return globalStep;
                }
            }

            return null;
        }

        private static bool MatchesExecutionKey(string candidate, string executionKey)
        {
            return String.IsNullOrWhiteSpace(executionKey) || String.Equals(candidate, executionKey, StringComparison.Ordinal);
        }

        private static bool AppliesToTenant(string registrationTenantId, string tenantId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return true;
            if (String.Equals(registrationTenantId, tenantId, StringComparison.Ordinal)) return true;
            return String.IsNullOrWhiteSpace(registrationTenantId) || String.Equals(registrationTenantId, "global", StringComparison.OrdinalIgnoreCase);
        }

        private static BuiltinStepRegistration BuildClassRegistration(Step step)
        {
            Type type = step.GetType();
            AssemblyName assembly = type.Assembly.GetName();
            MethodInfo run = type.GetMethod(nameof(Step.Run), BindingFlags.Instance | BindingFlags.Public)!;
            return new BuiltinStepRegistration
            {
                ExecutionKey = step.Identifier,
                TenantId = step.TenantId,
                SourceKind = BuiltinStepSourceKind.Class,
                DisplayName = step.Name,
                DeclaringType = type.FullName ?? String.Empty,
                AssemblyName = assembly.Name ?? String.Empty,
                AssemblyVersion = assembly.Version?.ToString() ?? String.Empty,
                SignatureHash = ComputeSignatureHash(BuildMethodSignature(run)),
                MaxRuntimeMs = step.MaxRuntimeMs
            };
        }

        private static BuiltinStepRegistration BuildMethodRegistration(CodeAttributeStepInfo info)
        {
            Type? declaringType = info.Method.DeclaringType;
            AssemblyName assembly = info.Method.Module.Assembly.GetName();
            return new BuiltinStepRegistration
            {
                ExecutionKey = info.Identifier,
                TenantId = info.TenantId,
                SourceKind = BuiltinStepSourceKind.Method,
                DisplayName = info.Identifier,
                DeclaringType = declaringType?.FullName ?? String.Empty,
                MethodName = info.Method.Name,
                AssemblyName = assembly.Name ?? String.Empty,
                AssemblyVersion = assembly.Version?.ToString() ?? String.Empty,
                SignatureHash = ComputeSignatureHash(BuildMethodSignature(info.Method)),
                MaxRuntimeMs = info.MaxRuntimeMs
            };
        }

        private static string BuildMethodSignature(MethodInfo method)
        {
            string parameters = String.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
            string declaringType = method.DeclaringType?.FullName ?? String.Empty;
            string returnType = method.ReturnType.FullName ?? method.ReturnType.Name;
            return returnType + " " + declaringType + "." + method.Name + "(" + parameters + ")";
        }

        private static string ComputeSignatureHash(string signature)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(signature);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
