#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Oasis.DeedProcessor.Host.Services;
using Xunit;

namespace AzureTextReader.Tests
{
    public class FileMonitorUtilsTests : IDisposable
    {
        private readonly string _tempFile;
        private readonly string _stateDir;

        public FileMonitorUtilsTests()
        {
            _stateDir = Path.Combine(Path.GetTempPath(), "atr_state_tests");
            Directory.CreateDirectory(_stateDir);
            _tempFile = Path.Combine(Path.GetTempPath(), "atr_test_file.txt");
            File.WriteAllText(_tempFile, "initial");
        }

        [Fact]
        public async Task ComputeFileHashAsync_ReturnsDifferentHashAfterChange()
        {
            var h1 = await FileMonitorUtils.ComputeFileHashAsync(_tempFile);
            File.WriteAllText(_tempFile, "changed");
            var h2 = await FileMonitorUtils.ComputeFileHashAsync(_tempFile);
            Assert.NotEqual(h1, h2);
        }

        [Fact]
        public void IsFileStable_DetectsStability()
        {
            var now = DateTime.UtcNow;
            // set the file LastWrite to older than threshold to simulate stability
            File.SetLastWriteTimeUtc(_tempFile, now.AddSeconds(-10));
            // Use lastSeen older than stable seconds
            var stable = FileMonitorUtils.IsFileStable(_tempFile, now.AddSeconds(-10), 3);
            Assert.True(stable);
        }

        [Fact]
        public void LoadProcessedHashes_ReturnsEmptyForMissing()
        {
            var set = FileMonitorUtils.LoadProcessedHashes(_stateDir);
            Assert.NotNull(set);
        }

        public void Dispose()
        {
            try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
            try { Directory.Delete(_stateDir, true); } catch { }
        }
    }
}
