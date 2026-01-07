using System.Security.Cryptography;
using System.Text.Json;

namespace AzureTextReader.Services.Ocr
{
    public static class FileMonitorUtils
    {
        public static async Task<string> ComputeFileHashAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }

        public static bool IsFileStable(string path, DateTime lastSeenUtc, int stableSeconds)
        {
            if (!File.Exists(path)) return false;
            var fi = new FileInfo(path);
            var lastWrite = fi.LastWriteTimeUtc;
            var now = DateTime.UtcNow;
            // stable if last seen is older than stableSeconds and last write hasn't changed since
            return (now - lastSeenUtc).TotalSeconds >= stableSeconds && (now - lastWrite).TotalSeconds >= stableSeconds;
        }

        public static HashSet<string> LoadProcessedHashes(string stateFolder)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var idx = Path.Combine(stateFolder ?? string.Empty, "processed_hashes.json");
                if (File.Exists(idx))
                {
                    var txt = File.ReadAllText(idx);
                    var arr = JsonSerializer.Deserialize<List<string>>(txt) ?? new List<string>();
                    foreach (var h in arr) set.Add(h);
                }
            }
            catch
            {
                // ignore
            }
            return set;
        }
    }
}
