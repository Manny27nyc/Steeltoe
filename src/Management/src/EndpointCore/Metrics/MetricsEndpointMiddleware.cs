﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Steeltoe.Management.Endpoint.Middleware;
using Steeltoe.Management.EndpointCore.ContentNegotiation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Steeltoe.Management.Endpoint.Metrics
{
    public class MetricsEndpointMiddleware : EndpointMiddleware<IMetricsResponse, MetricsRequest>
    {
        private readonly RequestDelegate _next;

        public MetricsEndpointMiddleware(RequestDelegate next, MetricsEndpoint endpoint, IEnumerable<IManagementOptions> mgmtOptions, ILogger<MetricsEndpointMiddleware> logger = null)
            : base(endpoint, mgmtOptions, null, false, logger)
        {
            _next = next;
        }

        [Obsolete("Use newer constructor that passes in IManagementOptions instead")]
        public MetricsEndpointMiddleware(RequestDelegate next, MetricsEndpoint endpoint, ILogger<MetricsEndpointMiddleware> logger = null)
            : base(endpoint, null, false, logger)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (RequestVerbAndPathMatch(context.Request.Method, context.Request.Path.Value))
            {
                await HandleMetricsRequestAsync(context).ConfigureAwait(false);
            }
            else
            {
                await _next(context).ConfigureAwait(false);
            }
        }

        public override string HandleRequest(MetricsRequest arg)
        {
            var result = _endpoint.Invoke(arg);
            return result == null ? null : Serialize(result);
        }

        protected internal async Task HandleMetricsRequestAsync(HttpContext context)
        {
            var request = context.Request;
            var response = context.Response;

            _logger?.LogDebug("Incoming path: {0}", request.Path.Value);

            var metricName = GetMetricName(request);
            if (!string.IsNullOrEmpty(metricName))
            {
                // GET /metrics/{metricName}?tag=key:value&tag=key:value
                var tags = ParseTags(request.Query);
                var metricRequest = new MetricsRequest(metricName, tags);
                var serialInfo = HandleRequest(metricRequest);

                if (serialInfo != null)
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.WriteAsync(serialInfo).ConfigureAwait(false);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            else
            {
                // GET /metrics
                var serialInfo = HandleRequest(null);
                _logger?.LogDebug("Returning: {0}", serialInfo);

                context.HandleContentNegotiation(_logger);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await context.Response.WriteAsync(serialInfo).ConfigureAwait(false);
            }
        }

        protected internal string GetMetricName(HttpRequest request)
        {
            if (_mgmtOptions == null)
            {
                return GetMetricName(request, _endpoint.Path);
            }

            var paths = new List<string>(_mgmtOptions.Select(opt => $"{opt.Path}/{_endpoint.Id}".Replace("//", "/")));
            foreach (var path in paths)
            {
                var metricName = GetMetricName(request, path);
                if (metricName != null)
                {
                    return metricName;
                }
            }

            return null;
        }

        protected internal List<KeyValuePair<string, string>> ParseTags(IQueryCollection query)
        {
            var results = new List<KeyValuePair<string, string>>();
            if (query == null)
            {
                return results;
            }

            foreach (var q in query)
            {
                if (q.Key.Equals("tag", StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var kvp in q.Value)
                    {
                        var pair = ParseTag(kvp);
                        if (pair != null && !results.Contains(pair.Value))
                        {
                            results.Add(pair.Value);
                        }
                    }
                }
            }

            return results;
        }

        protected internal KeyValuePair<string, string>? ParseTag(string kvp)
        {
            var str = kvp.Split(new char[] { ':' }, 2);
            if (str != null && str.Length == 2)
            {
                return new KeyValuePair<string, string>(str[0], str[1]);
            }

            return null;
        }

        private string GetMetricName(HttpRequest request, string path)
        {
            var epPath = new PathString(path);
            if (request.Path.StartsWithSegments(epPath, out var remaining) && remaining.HasValue)
            {
                return remaining.Value.TrimStart('/');
            }

            return null;
        }
    }
}
