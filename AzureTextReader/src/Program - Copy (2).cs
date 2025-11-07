//using Azure.AI.OpenAI;
//using OpenAI;
//using OpenAI.Chat;
//using Microsoft.Extensions.Caching.Memory;
//using System.ClientModel;
//using System.ClientModel.Primitives;
//using System.Net;
//using System.Reflection.PortableExecutable;
//using System.Security.Cryptography;
//using System.Text;
//using System.Text.Json;
//using System.Text.Json.Nodes;
//using System.Reflection;

//// Create a MemoryCache instance and run
//using var memoryCache = new MemoryCache(new MemoryCacheOptions());
//await RunAsync(memoryCache);

//// Asynchronous method to process all image URLs (local function)
//async Task RunAsync(IMemoryCache memoryCache)
//{
//    //Azure endpoint and credentials(replace with your actual values)
//    var endpoint = "https://sudhir-ai-test.openai.azure.com/contentunderstanding/analyzers/prebuilt-documentAnalyzer:analyze?api-version=2025-05-01-preview";
//    var subscriptionKey = "5j4M8CypX3SHzhuzdsddwhGrHyDMlnhIKtve7cqMwgHtjURmiA1DJQQJ99BJAC4f1cMXJ3w3AAAAACOGbrak";
//    var imageUrls = new[] { "https://github.com/sudhirsharma23/azure-ai-foundry-image-text-extractor/blob/main/src/images/2025000065660-1.tif?raw=true",
//                            "https://github.com/sudhirsharma23/azure-ai-foundry-image-text-extractor/blob/main/src/images/2025000065660.tif?raw=true"
// };

//    // Use a single HttpClient instance for all requests
//    using var client = new HttpClient();
//    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

//    foreach (var imageUrl in imageUrls)
//    {
//        // Prepare the request body for the POST request
//        var requestBody = $"{{\"url\":\"{imageUrl}\"}}";
//        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

//        // Send POST request to start extraction
//        var postResponse = await client.PostAsync(endpoint, content);
//        postResponse.EnsureSuccessStatusCode();

//        // Retrieve the Operation-Location header for polling
//        if (!postResponse.Headers.TryGetValues("Operation-Location", out var values))
//        {
//            Console.WriteLine("Operation-Location header not found. Skipping this image.");
//            continue;
//        }
//        var operationLocation = values.First();
//        Console.WriteLine($"Operation-Location: {operationLocation}");

//        ExtractionOperation? extractionOperation = null;
//        // Poll the operation status until it is 'Succeeded'
//        do
//        {
//            var getRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
//            getRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
//            var getResponse = await client.SendAsync(getRequest);
//            getResponse.EnsureSuccessStatusCode();
//            var result = await getResponse.Content.ReadAsStringAsync();

//            extractionOperation = JsonSerializer.Deserialize<ExtractionOperation>(result);
//            if (extractionOperation == null)
//            {
//                Console.WriteLine("Failed to parse ExtractionOperation. Skipping this image.");
//                break;
//            }
//            if (extractionOperation.status != "Succeeded")
//            {
//                Console.WriteLine($"Status: {extractionOperation.status}. Waiting before retry...");
//                await Task.Delay(3000); // Wait3 seconds before retrying
//            }
//        } while (extractionOperation.status != "Succeeded");

//        // Output the extracted content if available
//        if (extractionOperation?.result?.contents != null && extractionOperation.result.contents.Count >0)
//        {
//            Console.WriteLine($"Parsed ExtractionOperation: status={extractionOperation.status}, contents={extractionOperation.result.contents[0].markdown}");

//            // Initialize the AzureOpenAIClient
//            var credential = new ApiKeyCredential(subscriptionKey);
//            var azureClient = new AzureOpenAIClient(new Uri("https://sudhir-ai-test.openai.azure.com/"), credential);

//            string schemaText = File.ReadAllText("E:\\Sudhir\\Prj\\files\\zip\\src\\invoice_schema - Copy.json");
//            JsonNode jsonSchema = JsonNode.Parse(schemaText);


//            // Initialize the ChatClient with the specified deployment name
//            ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini");

//            // Create a list of chat messages
//            var messages = new List<OpenAI.Chat.ChatMessage>
//                                     {
//                                     new SystemChatMessage(@" You are an OCR-like data extraction tool that extracts deed data from tif.

//                                    1. Extract all values from the attached Deed PDF. Identify and return the following details for each property transaction:

//                                    2. Grantor (seller/transferor) name

//                                    3. Grantee (buyer/transferee) name

//                                    4. Property address

//                                    5. Legal description

//                                    6. Sale price or transaction amount

//                                    7. Tax information (if present)

//                                    8. Recording date

//                                    9. Document number or reference

//                                    10. Notary information and date

//                                     Any other relevant fields or notes mentioned in the deed

//                                     You are a data transformation tool that takes in JSON data and a reference JSON schema, and outputs JSON data according to the schema.
//                                     Not all of the data in the input JSON will fit the schema, so you may need to omit some data or add null values to the output JSON.
//                                     Translate all data into English if not already in English.
//                                     Ensure values are formatted as specified in the schema (e.g. dates as YYYY-MM-DD).
//                                     Here is the schema: " + $"{jsonSchema}"),
//                                     new UserChatMessage($"Please extract the data, grouping data according to theme/sub groups, and then output into JSON and provide the clean json data {extractionOperation.result.contents[0].markdown}")
//                                     };

//            // Create chat completion options
//            var options = new ChatCompletionOptions
//            {
//                Temperature = (float)0.7,
//                MaxOutputTokenCount =13107,

//                TopP = (float)0.95,
//                FrequencyPenalty = (float)0,
//                PresencePenalty = (float)0,
//            };

//            try
//            {
//                // Compute a cache key for this combination of messages + options
//                var cacheKey = ComputeCacheKey(messages, options);

//                // Try to pull cached response
//                if (memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
//                {
//                    Console.WriteLine("Cache hit - using cached chat completion.");
//                    Console.WriteLine(cachedJson);
//                }
//                else
//                {
//                    // Create the chat completion request
//                    ChatCompletion completion = chatClient.CompleteChat(messages, options);

//                    // Serialize the response to JSON for consistent caching / logging
//                    var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions() { WriteIndented = true });

//                    // Cache the serialized completion with reasonable expiration
//                    var cacheEntryOptions = new MemoryCacheEntryOptions
//                    {
//                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
//                        SlidingExpiration = TimeSpan.FromHours(24)
//                    };

//                    memoryCache.Set(cacheKey, completionJson, cacheEntryOptions);

//                    // Print the response
//                    if (completion != null)
//                    {
//                        Console.WriteLine(completionJson);
//                    }
//                    else
//                    {
//                        Console.WriteLine("No response received.");
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"An error occurred: {ex.Message}");
//            }
//        }
//        else
//        {
//            Console.WriteLine("No contents found in extraction result.");
//        }
//    }
//}

//// Helper: compute stable cache key from messages + options (local function)
//string ComputeCacheKey(IList<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options)
//{
//    var sb = new StringBuilder();
//    // include options that affect the output
//    sb.Append($"temp:{options.Temperature};maxTokens:{options.MaxOutputTokenCount};topP:{options.TopP};freq:{options.FrequencyPenalty};pres:{options.PresencePenalty};");
//    foreach (var m in messages)
//    {
//        // Role and content via reflection to avoid compile-time dependency on SDK internal shape
//        var t = m.GetType();
//        string role = "";
//        string content = "";
//        var roleProp = t.GetProperty("Role") ?? t.GetProperty("Author") ?? t.GetProperty("Name");
//        if (roleProp != null)
//        {
//            try { role = roleProp.GetValue(m)?.ToString() ?? ""; } catch { role = ""; }
//        }
//        var contentProp = t.GetProperty("Content") ?? t.GetProperty("Text") ?? t.GetProperty("Message");
//        if (contentProp != null)
//        {
//            try { content = contentProp.GetValue(m)?.ToString() ?? ""; } catch { content = ""; }
//        }
//        else
//        {
//            // fallback to ToString()
//            content = m.ToString() ?? "";
//        }

//        sb.Append($"role:{role};content:{content};");
//    }

//    // Hash the combined string for compact key
//    using var sha = SHA256.Create();
//    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
//    var hash = sha.ComputeHash(bytes);
//    return Convert.ToHexString(hash);
//}

//// Model for the extraction operation response
//class ExtractionOperation
//{
//    public string? id { get; set; }
//    public string? status { get; set; }
//    public ExtractionResult? result { get; set; }
//}

//// Model for the result property in the extraction operation
//class ExtractionResult
//{
//    public List<ContentResult>? contents { get; set; }
//}

//// Model for each content item in the extraction result
//class ContentResult
//{
//    public string? markdown { get; set; }
//    public string? kind { get; set; }
//}

