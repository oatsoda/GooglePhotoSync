using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GooglePhotoSync.Local;
using Microsoft.Extensions.Options;

namespace GooglePhotoSync.Sync
{
    public class SyncStateFile
    {
        private const string _FILE_NAME = "google.sync";

        private readonly string m_FilePath;


        public SyncStateFile(IOptions<LocalSettings> localSettings)
        {
            m_FilePath = Path.Combine(localSettings.Value.LocalFolderRoot, _FILE_NAME);
        }

        public async Task<SyncState> Load()
        {
            if (!File.Exists(m_FilePath))
                return new SyncState();

            var bytes = await File.ReadAllBytesAsync(m_FilePath);
            var value = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<SyncState>(value, new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task Save(SyncState syncState)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(syncState, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllBytesAsync(m_FilePath, bytes);
        }
    }
}