using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TextractUploader
{
    class Program
    {
        // --- CONFIGURATION ---
        // TODO: Replace with your S3 bucket name. This bucket should be configured to trigger the TextractProcessor Lambda.
        private const string BucketName = "testbucket-sudhir-bsi1";

        private const string UploadFolder = "uploads";
        // TODO: Replace with the full path to the .tif file you want to upload.
        private static readonly string[] FilesToUpload = new[]
        {
            "2025000065660.tif",
            "2025000065660-1.tif"
        };

        // TODO: Replace with the AWS region of your S3 bucket.
        private static readonly RegionEndpoint BucketRegion = RegionEndpoint.USWest2;

        private static IAmazonS3 _s3Client;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== TextractUploader with Ollama gemma3:latest Demo ===");
            Console.WriteLine();

            // Test Ollama gemma3:latest model first
            //await TestOllamaModel();

            Console.WriteLine();
            Console.WriteLine("=== Starting S3 Upload ===");
            Console.WriteLine();

            _s3Client = new AmazonS3Client(BucketRegion);

            // Get current date in YYYY-MM-DD format
            var uploadDate = DateTime.Now.ToString("yyyy-MM-dd");
            Console.WriteLine($"Upload date folder: {uploadDate}");
            Console.WriteLine();

            foreach (var fileName in FilesToUpload)
            {
                // Check if file exists locally
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Error: File {fileName} not found in {Directory.GetCurrentDirectory()}");
                    continue;
                }

                // Get file name without extension for folder name
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                
                // Create the S3 key with structure: uploads/<date>/<filename>/<actualfile>
                var key = $"{UploadFolder}/{uploadDate}/{fileNameWithoutExt}/{fileName}";
     
                var request = new PutObjectRequest
                {
                    BucketName = BucketName,
                    Key = key,
                    FilePath = filePath,
                    ContentType = "image/tiff" // Or "image/jpeg" for JPG files
                };

                try
                {
                    Console.WriteLine($"Uploading {fileName}...");
                    Console.WriteLine($"  S3 Path: {key}");
                    var response = await _s3Client.PutObjectAsync(request);

                    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    {
                        Console.WriteLine($"  SUCCESS: Uploaded to s3://{BucketName}/{key}");
                    }
                    else
                    {
                        Console.WriteLine($"  ERROR: Upload failed with status {response.HttpStatusCode}");
                    }
                }
                catch (AmazonS3Exception e)
                {
                    Console.WriteLine($"  S3 Error: {e.Message}");
                    Console.WriteLine($"  Error Code: {e.ErrorCode}");
                    Console.WriteLine($"  Request ID: {e.RequestId}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"  General Error: {e.Message}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("Upload completed!");
            Console.WriteLine();
            Console.WriteLine("Folder structure created:");
      Console.WriteLine($"  {UploadFolder}/");
         Console.WriteLine($"    {uploadDate}/");
            foreach (var fileName in FilesToUpload)
            {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
           Console.WriteLine($"      {fileNameWithoutExt}/");
     Console.WriteLine($"        {fileName}");
            }
            Console.WriteLine();
        Console.WriteLine("Press any key to exit.");
      Console.ReadKey();
        }

        private static async Task TestOllamaModel()
        {
            try
            {
                Console.WriteLine("Testing Ollama gemma3:latest Model");
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine();

                var ollamaService = new OllamaService();

                // Step 1: Check if Ollama is running
                Console.WriteLine("[1] Checking Ollama availability...");
                var isAvailable = await ollamaService.IsAvailableAsync();

                if (!isAvailable)
                {
                    Console.WriteLine("ERROR: Ollama service is NOT available at http://localhost:11434");
                    Console.WriteLine();
                    Console.WriteLine("To start Ollama with gemma3:latest:");
                    Console.WriteLine("  1. docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama");
                    Console.WriteLine("  2. docker exec -it ollama ollama pull gemma3:latest");
                    Console.WriteLine("  3. docker exec -it ollama ollama run gemma3:latest");
                    Console.WriteLine();
                    return;
                }

                Console.WriteLine("SUCCESS: Ollama service is available");
                Console.WriteLine();

                // Step 2: List available models
                Console.WriteLine("[2] Fetching available models...");
                try
                {
                    var modelsResponse = await ollamaService.GetModelsAsync();
                    Console.WriteLine($"Found {modelsResponse.Models.Count} model(s):");
                    foreach (var model in modelsResponse.Models)
                    {
                        var sizeInMB = model.Size / (1024.0 * 1024.0);
                        Console.WriteLine($"  - {model.Name} ({sizeInMB:F1} MB)");
                    }
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not fetch models - {ex.Message}");
                    Console.WriteLine();
                }

                // Step 3: Test simple prompt
                Console.WriteLine("[3] Testing simple prompt...");
                Console.WriteLine("Prompt: 'Explain what AWS Textract does in one sentence.'");
                Console.WriteLine();

                var startTime = DateTime.Now;
                var response = await ollamaService.GenerateAsync(
                    "Explain what AWS Textract does in one sentence."
                );
                var duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine("Response from gemma3:latest:");
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine(response.Response);
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine();
                Console.WriteLine("Statistics:");
                Console.WriteLine($"  Model: {response.Model}");
                Console.WriteLine($"  Response time: {duration:F2} seconds");
                Console.WriteLine($"  Tokens generated: {response.EvalCount}");
                if (response.EvalDuration > 0)
                {
                    var tokensPerSec = response.EvalCount / (response.EvalDuration / 1_000_000_000.0);
                    Console.WriteLine($"  Generation speed: {tokensPerSec:F2} tokens/sec");
                }
                Console.WriteLine();

                // Step 4: Test chat endpoint
                Console.WriteLine("[4] Testing chat endpoint with system prompt...");
                Console.WriteLine("System: 'You are an expert in AWS services.'");
                Console.WriteLine("User: 'What is Amazon S3 used for?'");
                Console.WriteLine();

                startTime = DateTime.Now;
                var chatResponse = await ollamaService.ChatAsync(
                    "What is Amazon S3 used for?",
                    "You are an expert in AWS services."
                );
                duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine("Chat Response:");
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine(chatResponse.Message.Content);
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine();
                Console.WriteLine("Statistics:");
                Console.WriteLine($"  Response time: {duration:F2} seconds");
                Console.WriteLine($"  Tokens: {chatResponse.EvalCount}");
                Console.WriteLine();

                // Step 5: Test document processing
                Console.WriteLine("[5] Testing document extraction...");
                var documentPrompt = @"Extract key information from this invoice:
                                        Invoice Number: INV-2024-001
                                        Date: 2024-01-15
                                        Vendor: ABC Company
                                        Total: $1,250.00

                                        Return as simple text with the 4 fields listed.";

                Console.WriteLine("Prompt: Document extraction");
                Console.WriteLine();

                startTime = DateTime.Now;
                var docResponse = await ollamaService.GenerateAsync(documentPrompt);
                duration = (DateTime.Now - startTime).TotalSeconds;

                Console.WriteLine("Document Extraction Response:");
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine(docResponse.Response);
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine();
                Console.WriteLine("Statistics:");
                Console.WriteLine($"  Response time: {duration:F2} seconds");
                Console.WriteLine($"  Tokens: {docResponse.EvalCount}");
                Console.WriteLine();

                Console.WriteLine("All Ollama tests completed successfully!");

            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR: HTTP connection failed - {ex.Message}");
                Console.WriteLine("Ensure Ollama Docker container is running.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
    }

    // Ollama Service Class
    public class OllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _modelName;

        public OllamaService(string baseUrl = "http://localhost:11434", string modelName = "gemma3:latest")
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _baseUrl = baseUrl;
            _modelName = modelName;
        }

        public async Task<OllamaResponse> GenerateAsync(string prompt)
        {
            var request = new OllamaGenerateRequest
            {
                Model = _modelName,
                Prompt = prompt,
                Stream = false
            };

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var httpResponse = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);
            httpResponse.EnsureSuccessStatusCode();

            var responseBody = await httpResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OllamaResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        public async Task<OllamaChatResponse> ChatAsync(string message, string systemPrompt = null)
        {
            var messages = new List<OllamaChatMessage>();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new OllamaChatMessage { Role = "system", Content = systemPrompt });
            }

            messages.Add(new OllamaChatMessage { Role = "user", Content = message });

            var request = new OllamaChatRequest
            {
                Model = _modelName,
                Messages = messages,
                Stream = false
            };

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var httpResponse = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content);
            httpResponse.EnsureSuccessStatusCode();

            var responseBody = await httpResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OllamaChatResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<OllamaModelsResponse> GetModelsAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OllamaModelsResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }

    // Request Models
    public class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    public class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    public class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    // Response Models
    public class OllamaResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("response")]
        public string Response { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("total_duration")]
        public long TotalDuration { get; set; }

        [JsonPropertyName("load_duration")]
        public long LoadDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }

        [JsonPropertyName("eval_duration")]
        public long EvalDuration { get; set; }
    }

    public class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("message")]
        public OllamaChatMessage Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("total_duration")]
        public long TotalDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }

        [JsonPropertyName("eval_duration")]
        public long EvalDuration { get; set; }
    }

    public class OllamaModelsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo> Models { get; set; }
    }

    public class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}