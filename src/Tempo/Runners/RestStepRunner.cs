namespace Tempo.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using RestWrapper;
    using Tempo.Enums;
    using Tempo.Logs;

    /// <summary>
    /// Step runner for REST API calls using RestWrapper.
    /// </summary>
    public class RestStepRunner : StepRunner
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.

        private readonly HttpMethod _HttpMethod;
        private readonly string _UrlTemplate;
        private readonly NameValueCollection _DefaultHeaders;
        private readonly int _TimeoutMs;

        /// <summary>
        /// REST step runner.
        /// </summary>
        /// <param name="httpMethod">HTTP method (GET, POST, PUT, DELETE, PATCH).</param>
        /// <param name="urlTemplate">URL template with optional {tokens} for runtime substitution.</param>
        /// <param name="defaultHeaders">Default headers to include in requests.</param>
        /// <param name="timeoutMs">Timeout in milliseconds (default 30000).</param>
        /// <param name="logger">Logger instance for logging (optional).</param>
        public RestStepRunner(
            HttpMethod httpMethod,
            string urlTemplate,
            NameValueCollection defaultHeaders = null,
            int timeoutMs = 30000,
            Logger logger = null)
        {
            _HttpMethod = httpMethod ?? throw new ArgumentNullException(nameof(httpMethod));
            _UrlTemplate = (!String.IsNullOrEmpty(urlTemplate) ? urlTemplate : throw new ArgumentNullException(nameof(urlTemplate)));
            _DefaultHeaders = defaultHeaders ?? new NameValueCollection();
            _TimeoutMs = timeoutMs > 0 ? timeoutMs : 30000;
            Logger = logger;
        }

        /// <summary>
        /// Create REST step runner from REST step configuration.
        /// </summary>
        /// <param name="config">REST step configuration.</param>
        /// <param name="logger">Logger instance for logging (optional).</param>
        /// <returns>RestStepRunner instance.</returns>
        public static RestStepRunner FromConfig(RestStepConfiguration config, Logger logger = null)
        {
            ArgumentNullException.ThrowIfNull(config);

            // Parse HTTP method
            string methodStr = config.Method.ToUpperInvariant();
            HttpMethod httpMethod = methodStr switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                _ => throw new ArgumentException($"Unsupported HTTP method: {methodStr}")
            };

            // Convert headers dictionary to NameValueCollection
            NameValueCollection headers = new NameValueCollection();
            if (config.Headers != null)
            {
                foreach (var kvp in config.Headers)
                {
                    headers[kvp.Key] = kvp.Value;
                }
            }

            return new RestStepRunner(httpMethod, config.Url, headers, config.TimeoutMs, logger);
        }

        /// <summary>
        /// Execute the REST API call.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Step result.</returns>
        protected override async Task<StepResult> ExecuteInternal(StepRequest req, CancellationToken token)
        {
            try
            {
                // Extract runtime data from StepRequest
                byte[] requestBody = null;
                Dictionary<string, string> urlParams = new Dictionary<string, string>();
                Dictionary<string, string> runtimeHeaders = new Dictionary<string, string>();

                if (req.Data != null)
                {
                    if (req.Data is Dictionary<string, object> dataDict)
                    {
                        // Extract body
                        if (dataDict.TryGetValue("Body", out object bodyObj) && bodyObj != null)
                        {
                            if (bodyObj is byte[] bodyBytes)
                            {
                                requestBody = bodyBytes;
                            }
                            else if (bodyObj is string bodyStr)
                            {
                                requestBody = Encoding.UTF8.GetBytes(bodyStr);
                            }
                        }

                        // Extract URL parameters
                        if (dataDict.TryGetValue("UrlParams", out object urlParamsObj) && urlParamsObj != null)
                        {
                            if (urlParamsObj is Dictionary<string, string> urlParamsDict)
                            {
                                urlParams = urlParamsDict;
                            }
                            else if (urlParamsObj is Dictionary<string, object> urlParamsObjDict)
                            {
                                foreach (var kvp in urlParamsObjDict)
                                {
                                    urlParams[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                                }
                            }
                        }

                        // Extract runtime headers
                        if (dataDict.TryGetValue("Headers", out object headersObj) && headersObj != null)
                        {
                            if (headersObj is Dictionary<string, string> headersDict)
                            {
                                runtimeHeaders = headersDict;
                            }
                            else if (headersObj is Dictionary<string, object> headersObjDict)
                            {
                                foreach (var kvp in headersObjDict)
                                {
                                    runtimeHeaders[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                                }
                            }
                        }
                    }
                }

                // Build final URL with template substitution
                string finalUrl = BuildUrl(_UrlTemplate, urlParams);

                // Merge headers (runtime overrides default)
                NameValueCollection finalHeaders = new NameValueCollection(_DefaultHeaders);
                foreach (var kvp in runtimeHeaders)
                {
                    finalHeaders[kvp.Key] = kvp.Value;
                }

                // Make the HTTP request using RestWrapper
                RestRequest restRequest = new RestRequest(
                    finalUrl,
                    _HttpMethod,
                    finalHeaders,
                    null  // contentType will be in headers if needed
                );

                RestResponse restResponse;

                if (requestBody != null && requestBody.Length > 0)
                {
                    restResponse = await restRequest.SendAsync(requestBody).ConfigureAwait(false);
                }
                else
                {
                    restResponse = await restRequest.SendAsync().ConfigureAwait(false);
                }

                // Map HTTP status to StepResult
                StepResultTypeEnum resultType = MapStatusCodeToResult(restResponse.StatusCode);

                // Convert response headers to Dictionary
                Dictionary<string, string> responseHeaders = new Dictionary<string, string>();
                if (restResponse.Headers != null)
                {
                    foreach (string key in restResponse.Headers.AllKeys)
                    {
                        if (key != null)
                        {
                            responseHeaders[key] = restResponse.Headers[key];
                        }
                    }
                }

                // Build result data
                Dictionary<string, object> resultData = new Dictionary<string, object>
                {
                    ["StatusCode"] = restResponse.StatusCode,
                    ["ResponseBody"] = restResponse.DataAsBytes ?? Array.Empty<byte>(),
                    ["Headers"] = responseHeaders
                };

                StepResult result = new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = resultType,
                    Data = resultData,
                    Metadata = req.Metadata
                };

                // For error/exception results, include response details in exception
                if (resultType == StepResultTypeEnum.Error || resultType == StepResultTypeEnum.Exception)
                {
                    string responseBodyStr = restResponse.DataAsString ?? "(no response body)";
                    result.Exception = new HttpRequestException(
                        $"HTTP request failed with status {restResponse.StatusCode}: {responseBodyStr}"
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                // Network or other failure
                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Exception,
                    Exception = ex,
                    Data = null,
                    Metadata = req.Metadata
                };
            }
        }

        /// <summary>
        /// Build URL from template by substituting tokens with values.
        /// </summary>
        /// <param name="template">URL template with {token} placeholders.</param>
        /// <param name="parameters">Parameter values.</param>
        /// <returns>Final URL.</returns>
        private string BuildUrl(string template, Dictionary<string, string> parameters)
        {
            string result = template;

            foreach (var kvp in parameters)
            {
                string token = "{" + kvp.Key + "}";
                result = result.Replace(token, Uri.EscapeDataString(kvp.Value));
            }

            return result;
        }

        /// <summary>
        /// Map HTTP status code to StepResultTypeEnum.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <returns>Step result type.</returns>
        private StepResultTypeEnum MapStatusCodeToResult(int statusCode)
        {
            if (statusCode >= 200 && statusCode < 300)
            {
                return StepResultTypeEnum.Success;
            }
            else if (statusCode >= 400 && statusCode < 500)
            {
                // Client errors - expected failure path
                return StepResultTypeEnum.Error;
            }
            else if (statusCode >= 500)
            {
                // Server errors - exception path (might warrant retry)
                return StepResultTypeEnum.Exception;
            }
            else
            {
                // 1xx, 3xx or other - treat as error
                return StepResultTypeEnum.Error;
            }
        }

#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
