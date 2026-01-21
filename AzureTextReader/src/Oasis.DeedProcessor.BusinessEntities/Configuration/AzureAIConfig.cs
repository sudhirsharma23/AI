using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace Oasis.DeedProcessor.BusinessEntities.Configuration
{
    public class AzureAIConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string SubscriptionKey { get; set; } = string.Empty;

        public static AzureAIConfig Load()
        {
            var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT");
            var key = Environment.GetEnvironmentVariable("AZURE_AI_KEY");

            if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(key))
            {
                Console.WriteLine("? Loaded Azure AI configuration from environment variables");
                return new AzureAIConfig
                {
                    Endpoint = endpoint,
                    SubscriptionKey = key
                };
            }

            var builder = new ConfigurationBuilder();

            // Use ASPNETCORE_ENVIRONMENT to include appsettings.{Environment}.json when present
            var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
            var envFileName = string.IsNullOrWhiteSpace(aspnetEnv) ? "" : $"appsettings.{aspnetEnv}.json";

            // Add common relative files; these are optional so missing files won't error
            builder.SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            if (!string.IsNullOrWhiteSpace(envFileName))
                builder.AddJsonFile(envFileName, optional: true, reloadOnChange: true);

            // Also add fallback locations (project src folder and AppContext base dir)
            var altCandidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "src", envFileName),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, envFileName),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, "..", "..", "..", "src", envFileName),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "appsettings.json"),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, "appsettings.json")
            }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath)
            .Distinct()
            .ToArray();

            foreach (var candidate in altCandidates)
            {
                try { builder.AddJsonFile(candidate, optional: true, reloadOnChange: true); } catch { }
            }

            builder.AddUserSecrets<AzureAIConfig>(optional: true)
                   .AddEnvironmentVariables();

            var configuration = builder.Build();
            var config = configuration.GetSection("AzureAI").Get<AzureAIConfig>();

            if (config == null || string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.SubscriptionKey))
            {
                // Diagnostic output to help identify why configuration was not found
                try
                {
                    Console.WriteLine("! AzureAI configuration not found - diagnostics:");
                    Console.WriteLine($"  ASPNETCORE_ENVIRONMENT: {aspnetEnv}");
                    Console.WriteLine($"  WorkingDirectory: {Directory.GetCurrentDirectory()}");
                    Console.WriteLine($"  AppContext.BaseDirectory: {AppContext.BaseDirectory}");

                    Console.WriteLine("  Configuration keys found (raw):");
                    foreach (var kv in configuration.AsEnumerable())
                    {
                        Console.WriteLine($"    {kv.Key} = {kv.Value}");
                    }

                    var section = configuration.GetSection("AzureAI");
                    var epVal = section["Endpoint"]; 
                    var skVal = section["SubscriptionKey"];

                    Console.WriteLine($"  AzureAI section present: {section.Exists()}");
                    Console.WriteLine($"    Endpoint present: {!string.IsNullOrEmpty(epVal)}");
                    Console.WriteLine($"    SubscriptionKey present: {!string.IsNullOrEmpty(skVal)}");

                    var envEp = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT");
                    var envKey = Environment.GetEnvironmentVariable("AZURE_AI_KEY");
                    Console.WriteLine($"  Environment AZURE_AI_ENDPOINT set: {!string.IsNullOrEmpty(envEp)}");
                    Console.WriteLine($"  Environment AZURE_AI_KEY set: {!string.IsNullOrEmpty(envKey)}");
                }
                catch
                {
                    // Ignore diagnostics failures
                }

                throw new InvalidOperationException(
                    "Azure AI configuration not found. Please set either:\n" +
                    "1. Environment variables: AZURE_AI_ENDPOINT and AZURE_AI_KEY\n" +
                    "2. User Secrets (recommended for development)\n" +
                    "3. appsettings.Development.json (not committed to git)"
                );
            }

            Console.WriteLine("? Loaded Azure AI configuration from appsettings/user secrets");
            return config;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Endpoint))
                throw new ArgumentException("Azure AI Endpoint is required");

            if (string.IsNullOrWhiteSpace(SubscriptionKey))
                throw new ArgumentException("Azure AI Subscription Key is required");

            if (!Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Azure AI Endpoint must be a valid HTTPS URL");

            Console.WriteLine($"? Azure AI Configuration validated: {Endpoint}");
        }
    }

    public class RedisConfig
    {
        public string ConnectionString { get; set; } = string.Empty;

        public static RedisConfig Load()
        {
            var connectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(connectionString))
                return new RedisConfig { ConnectionString = connectionString };

            var builder = new ConfigurationBuilder();

            var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
            var envFileName = string.IsNullOrWhiteSpace(aspnetEnv) ? "" : $"appsettings.{aspnetEnv}.json";

            builder.SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: true);

            if (!string.IsNullOrWhiteSpace(envFileName))
                builder.AddJsonFile(envFileName, optional: true);

            var altCandidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "src", envFileName),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, envFileName),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "appsettings.json"),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, "appsettings.json")
            }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath)
            .Distinct()
            .ToArray();

            foreach (var candidate in altCandidates)
            {
                try { builder.AddJsonFile(candidate, optional: true); } catch { }
            }

            builder.AddUserSecrets<RedisConfig>(optional: true)
                   .AddEnvironmentVariables();

            var configuration = builder.Build();
            return configuration.GetSection("Redis").Get<RedisConfig>() ?? new RedisConfig();
        }
    }
}
