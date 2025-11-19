using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace AzureTextReader.Configuration
{
    /// <summary>
    /// Secure configuration helper that loads Azure AI Services credentials from environment variables or appsettings
    /// NEVER hardcode keys in source code!
    /// </summary>
    public class AzureAIConfig
    {
        public string Endpoint { get; set; }
        public string SubscriptionKey { get; set; }

        /// <summary>
        /// Loads configuration from environment variables first, then appsettings.json
        /// Priority: Environment Variables > User Secrets > appsettings.json
        /// </summary>
        public static AzureAIConfig Load()
        {
            // Try environment variables first (highest priority)
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

            // Fall back to configuration file
            var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
.AddUserSecrets<AzureAIConfig>(optional: true) // Loads from user secrets if available
         .AddEnvironmentVariables(); // Also check env vars again

            var configuration = builder.Build();
            var config = configuration.GetSection("AzureAI").Get<AzureAIConfig>();

            if (config == null || string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.SubscriptionKey))
            {
                throw new InvalidOperationException(
"Azure AI configuration not found. Please set either:\n" +
   "1. Environment variables: AZURE_AI_ENDPOINT and AZURE_AI_KEY\n" +
   "2. User Secrets (recommended for development):\n" +
   "   dotnet user-secrets set \"AzureAI:Endpoint\" \"your-endpoint\"\n" +
     "   dotnet user-secrets set \"AzureAI:SubscriptionKey\" \"your-key\"\n" +
 "3. appsettings.Development.json (not committed to git)"
    );
            }

            Console.WriteLine("? Loaded Azure AI configuration from appsettings/user secrets");
            return config;
        }

        /// <summary>
        /// Validates that the configuration is properly loaded
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                throw new ArgumentException("Azure AI Endpoint is required");
            }

            if (string.IsNullOrWhiteSpace(SubscriptionKey))
            {
                throw new ArgumentException("Azure AI Subscription Key is required");
            }

            if (!Endpoint.StartsWith("https://"))
            {
                throw new ArgumentException("Azure AI Endpoint must be a valid HTTPS URL");
            }

            Console.WriteLine($"? Azure AI Configuration validated: {Endpoint}");
        }
    }

    /// <summary>
    /// Redis configuration (if needed)
    /// </summary>
    public class RedisConfig
    {
        public string ConnectionString { get; set; }

        public static RedisConfig Load()
        {
            var connectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");

            if (!string.IsNullOrEmpty(connectionString))
            {
                return new RedisConfig { ConnectionString = connectionString };
            }

            var builder = new ConfigurationBuilder()
         .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                  .AddJsonFile("appsettings.Development.json", optional: true)
               .AddUserSecrets<RedisConfig>(optional: true)
          .AddEnvironmentVariables();

            var configuration = builder.Build();
            return configuration.GetSection("Redis").Get<RedisConfig>();
        }
    }
}
