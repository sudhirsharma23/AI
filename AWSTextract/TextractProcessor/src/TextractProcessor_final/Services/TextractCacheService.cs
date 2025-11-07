using System.Text.Json;
using System.Text.Json.Serialization;
using TextractProcessor.Models;

namespace TextractProcessor.Services
{
    public class TextractCacheService
    {
        private readonly string _cacheDirectory;
        private const string CACHE_FILE_NAME = "textract_cache.json";
        private static readonly SemaphoreSlim _cacheLock = new(1, 1);
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public TextractCacheService(string outputDirectory)
        {
            _cacheDirectory = outputDirectory;
            Directory.CreateDirectory(_cacheDirectory);
            EnsureCacheFileExists().Wait();
        }

        private async Task EnsureCacheFileExists()
        {
            var cacheFile = Path.Combine(_cacheDirectory, CACHE_FILE_NAME);
            if (!File.Exists(cacheFile))
            {
                await File.WriteAllTextAsync(cacheFile, "{}");
            }
        }

        public async Task CacheTextractResponse(string documentKey, TextractResponse response)
        {
            var cacheFile = Path.Combine(_cacheDirectory, CACHE_FILE_NAME);

            try
            {
                await _cacheLock.WaitAsync();
                Dictionary<string, TextractResponse> cache;

                try
                {
                    var existingJson = await File.ReadAllTextAsync(cacheFile);
                    cache = JsonSerializer.Deserialize<Dictionary<string, TextractResponse>>(existingJson, _jsonOptions)
                   ?? new Dictionary<string, TextractResponse>();

                    // Update or add the new response
                    cache[documentKey] = response;

                    // Also save individual file for backup
                    var individualCache = Path.Combine(_cacheDirectory,
                  $"{Path.GetFileNameWithoutExtension(documentKey)}_cache.json");

                    // Save main cache
                    await File.WriteAllTextAsync(cacheFile,
          JsonSerializer.Serialize(cache, _jsonOptions));

                    // Save individual cache
                    await File.WriteAllTextAsync(individualCache,
                       JsonSerializer.Serialize(response, _jsonOptions));
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to cache Textract response for {documentKey}: {ex.Message}");
            }
        }

        public async Task<TextractResponse?> GetCachedResponse(string documentKey)
        {
            var cacheFile = Path.Combine(_cacheDirectory, CACHE_FILE_NAME);
            var individualCache = Path.Combine(_cacheDirectory,
                      $"{Path.GetFileNameWithoutExtension(documentKey)}_cache.json");

            try
            {
                await _cacheLock.WaitAsync();

                try
                {
                    // Try individual cache first
                    if (File.Exists(individualCache))
                    {
                        var json = await File.ReadAllTextAsync(individualCache);
                        return JsonSerializer.Deserialize<TextractResponse>(json, _jsonOptions);
                    }

                    // Fall back to main cache
                    if (File.Exists(cacheFile))
                    {
                        var json = await File.ReadAllTextAsync(cacheFile);
                        var cache = JsonSerializer.Deserialize<Dictionary<string, TextractResponse>>(json, _jsonOptions);
                        return cache?.TryGetValue(documentKey, out var response) == true ? response : null;
                    }

                    return null;
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to retrieve cached Textract response for {documentKey}: {ex.Message}");
                return null;
            }
        }

        public async Task<Dictionary<string, TextractResponse>> GetAllCachedResponses()
        {
            var cacheFile = Path.Combine(_cacheDirectory, CACHE_FILE_NAME);

            try
            {
                await _cacheLock.WaitAsync();

                try
                {
                    if (!File.Exists(cacheFile))
                        return new Dictionary<string, TextractResponse>();

                    var json = await File.ReadAllTextAsync(cacheFile);
                    return JsonSerializer.Deserialize<Dictionary<string, TextractResponse>>(json, _jsonOptions)
                 ?? new Dictionary<string, TextractResponse>();
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to retrieve all cached responses: {ex.Message}");
                return new Dictionary<string, TextractResponse>();
            }
        }
    }
}