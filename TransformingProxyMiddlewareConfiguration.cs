using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace TransformingProxy
{
    public class TransformingProxyMiddlewareConfiguration
    {
        private readonly IConfiguration _configuration;
        public string RuleSetsFilePath { get; private set; } = "";
        public string TargetHost { get; private set; } = "";
        
        public TransformingProxyMiddlewareConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Init()
        {
            TargetHost = _configuration["TargetHost"] ?? throw new Exception("Target host is not specified");
            
            Console.WriteLine($"Redirecting all requests to {TargetHost}");
            
            var filename = $"{_configuration["RuleSet"] ?? "default"}.json";
            RuleSetsFilePath = Path.Combine("RuleSets", filename);
            if (!File.Exists(RuleSetsFilePath))
                throw new Exception($"Rule sets file {RuleSetsFilePath} is not found");
        }
    }
}