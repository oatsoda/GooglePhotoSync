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
            album ??= await CreateAlbum(local.Name);

            await SyncFiles(local, album);
        }

        private async Task<GoogleAlbum> CreateAlbum(string albumName)
        {
            m_Logger.LogDebug($"Creating album '{albumName}'");
            return await m_GooglePhotosApi.CreateAlbum(new PostAlbumRequest
                                                       {
                                                           album = new PostAlbumRequest.NewAlbum
                                                                   {
                                                                       title = albumName
                                                                   }
                                                       });
        }

        private async Task SyncFiles(LocalPhotoAlbum local, GoogleAlbum album)
        {

        }
    }
}