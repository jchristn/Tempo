namespace Tempo
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
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
        /// Retrieve by identifier.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>Step.</returns>
        public Step GetByIdentifier(string id, string tenantId = null)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            lock (_Lock)
            {
                if (!String.IsNullOrEmpty(tenantId))
                {
                    if (_Steps.Values.Any(s => s.TenantId.Equals(tenantId) && s.Identifier.Equals(id)))
                    {
                        return _Steps.Values.First(s => s.TenantId.Equals(tenantId) && s.Identifier.Equals(id));
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    if (_Steps.Keys.Contains(id))
                    {
                        return _Steps[id];
                    }
                    else
                    {
                        return null;
                    }
                }
            }
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
        /// Get a step runner for the specified identifier and tenant.
        /// Checks both regular steps and attribute-based method steps.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier (optional).</param>
        /// <returns>StepRunner, or null if not found.</returns>
        public StepRunner GetStepRunner(string identifier, string tenantId = null)
        {
            if (String.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));

            lock (_Lock)
            {
                // First check regular steps
                Step step = GetByIdentifier(identifier, tenantId);
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
        /// Get max runtime for a step (works for both regular and attribute-based steps).
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
                Step step = GetByIdentifier(identifier, tenantId);
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

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
