using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GooglePhotoSync.Google.Api;
using Microsoft.Extensions.Logging;

namespace GooglePhotoSync.Google
{
    public class GoogleSource
    {
        private readonly IGooglePhotosApi m_GooglePhotosApi;
        private readonly ILogger<GoogleSource> m_Logger;

        public List<GoogleAlbum> Albums { get; private set; }

        public GoogleSource(IGooglePhotosApi googlePhotosApi, ILogger<GoogleSource> logger)
        {
            m_GooglePhotosApi = googlePhotosApi;
            m_Logger = logger;
        }

        public async Task Load()
        {
            var albums = new List<GoogleAlbum>();

            GetAlbumsResponse response;
            string pageToken = null;
            do
            {
                var request = new GetAlbumsRequest { pageSize = 50, pageToken = pageToken };
                response = await m_GooglePhotosApi.GetAlbums(request);
                if (response.Albums != null)
                    albums.AddRange(response.Albums);
                pageToken = response.NextPageToken;

            } while (response.NextPageToken != null);

            Albums = albums;
        }
    }

    public static class GoogleAlbumExtensions
    {
        public static async Task<List<MediaItem>> GetAlbumPhotos(this GoogleAlbum album, IGooglePhotosApi googlePhotosApi)
        {
            var mediaItems = new List<MediaItem>();

            SearchMediaItemsResponse response;
            string pageToken = null;
            do
            {
                var request = new SearchMediaItemsRequest { albumId = album.Id, pageSize = 100, pageToken = pageToken };
                response = await googlePhotosApi.SearchMediaItems(request);
                if (response.MediaItems != null)
                    mediaItems.AddRange(response.MediaItems);
                pageToken = response.NextPageToken;

            } while (response.NextPageToken != null);
            
            return mediaItems;
        }
    }
}