using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace TransformingProxy
{
    using Ruleset = Dictionary<string, EndpointRuleset>;

    public class TransformingProxyMiddleware : IMiddleware
    {
        private readonly HttpClient _httpClient;
        private readonly TransformingProxyMiddlewareConfiguration _configuration;
        private readonly ILogger _logger;

        public TransformingProxyMiddleware(ILogger logger, HttpClient httpClient, TransformingProxyMiddlewareConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            context.Request.Host = new HostString(_configuration.TargetHost);

            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();

            var requestMessage = GetRequestMessageFromRequest(context.Request);
            var response = await _httpClient.SendAsync(requestMessage);

            var responseBody = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                responseBody = TransformResponseBody(context.Request.Path, responseBody);

            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.Headers.ContentType = response.Content.Headers.ContentType?.ToString();

            var bodyWriter = new StreamWriter(context.Response.Body);
            await bodyWriter.WriteAsync(responseBody);
            await bodyWriter.FlushAsync();

            LogRequestAndResponse(context, requestBody, responseBody);
        }

        private static HttpRequestMessage GetRequestMessageFromRequest(HttpRequest request)
        {
            var result = new HttpRequestMessage
            {
                RequestUri = new Uri(request.GetEncodedUrl()),
                Method = new HttpMethod(request.Method),
            };
            return result;
        }

        private Ruleset GetRuleset()
        {
            var rulesetJson = File.ReadAllText(_configuration.RuleSetsFilePath);

            var ruleset = JsonSerializer.Deserialize<Ruleset>(rulesetJson)!;
            return ruleset;
        }

        private void LogRequestAndResponse(HttpContext context, string? requestBody, string? responseBody)
        {
            var requestPath = context.Request.GetEncodedPathAndQuery();
            requestBody = string.IsNullOrWhiteSpace(requestBody) ? null : JsonSerializer.Deserialize<JsonNode>(requestBody)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            var responseStatusCode = context.Response.StatusCode;
            responseBody = string.IsNullOrWhiteSpace(responseBody) ? null : JsonSerializer.Deserialize<JsonNode>(responseBody)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

            _logger.Information("RequestPath: {requestPath}\nRequestBody: {requestBody}\nResponse Status Code: {responseStatusCode}\nResponseBody: {responseBody}",
                requestPath,
                requestBody,
                responseStatusCode,
                responseBody
            );
        }

        private static void SetJsonValue(JsonNode json, string path, string? value)
        {
            var node = json;
            var steps = path.Split('.');
            foreach (var step in steps[..^1])
            {
                node![step] ??= new JsonObject();
                node = node[step];
            }

            var lastStep = steps[^1];

            node![lastStep] = value != null ? JsonNode.Parse(value) : null;
        }

        private string TransformResponseBody(PathString requestPath, string body)
        {
            var ruleset = GetRuleset();

            if (!ruleset.TryGetValue(requestPath.ToString(), out var endpointRuleset))
                return body;

            return TransformResponseBodyWithRule(body, endpointRuleset);
        }

        private static string TransformResponseBodyWithRule(string body, EndpointRuleset endpointRuleset)
        {
            if (endpointRuleset.Response == null)
                return body;

            var json = JsonNode.Parse(body)!;

            foreach (var (path, rule) in endpointRuleset.Response)
            {
                switch (rule.Action)
                {
                    case RuleAction.Clear:
                        SetJsonValue(json, path, null);
                        break;
                    case RuleAction.Set:
                        SetJsonValue(json, path, JsonSerializer.Serialize(rule.Value));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return json.ToJsonString();
        }
    }
}