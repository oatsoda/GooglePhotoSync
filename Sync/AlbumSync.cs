using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace GooglePhotoSync.Sync
{
    public class AlbumSync
    {
        private readonly IGooglePhotosApi m_GooglePhotosApi;
        private readonly ILogger<AlbumSync> m_Logger;

        public AlbumSync(IGooglePhotosApi googlePhotosApi, ILogger<AlbumSync> logger)
        {
            m_GooglePhotosApi = googlePhotosApi;
            m_Logger = logger;
        }

        public async Task SyncAlbum(LocalPhotoAlbum local, GoogleAlbum album)
        {
            if (album == null)
                album = await CreateAlbum(local.Name);

        }

        private async Task<GoogleAlbum> CreateAlbum(string albumName)
        {
            return await Task.FromResult((GoogleAlbum)null);
        }
    }
}