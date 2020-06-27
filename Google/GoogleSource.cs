using System.Collections.Generic;
using System.Threading.Tasks;
using GooglePhotoSync.Google.Api;

namespace GooglePhotoSync.Google
{
    public class GoogleSource
    {
        private readonly IGooglePhotosApi m_GooglePhotosApi;

        public List<Album> Albums { get; private set; }

        public GoogleSource(IGooglePhotosApi googlePhotosApi)
        {
            m_GooglePhotosApi = googlePhotosApi;
        }

        public async Task Load()
        {
            var albums = new List<Album>();

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
}