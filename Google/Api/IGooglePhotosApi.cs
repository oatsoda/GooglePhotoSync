using System;
using System.Threading.Tasks;
using Refit;

namespace GooglePhotoSync.Google.Api
{
    [Headers("Authorization: Bearer X")]
    public interface IGooglePhotosApi
    {
        [Post("/v1/mediaItems:search")]
        Task<SearchMediaItemsResponse> SearchMediaItems([Body]SearchMediaItemsRequest request);

        [Get("/v1/albums")]
        Task<GetAlbumsResponse> GetAlbums([Query]GetAlbumsRequest request);
    }

    
    public class GetAlbumsRequest
    {
        public int pageSize {  get; set; }
        public string pageToken {  get; set; }
    }

    public class GetAlbumsResponse
    {
        public GoogleAlbum[] Albums { get; set; }
        public string NextPageToken { get; set; }
    }

    public class GoogleAlbum
    {
        public string Id { get; set; }
        public string Title {  get; set; }
        public string ProductUrl {  get; set; }
        public bool IsWriteable { get; set; }
        public int MediaItemsCount { get; set; }
        public string CoverPhotoBaseUrl { get; set; }
        public string CoverPhotoMediaItemId { get; set; }
        // ShareInfo
}

    public class SearchMediaItemsRequest
    {

    }

    public class SearchMediaItemsResponse
    {
        public MediaItem[] MediaItems { get; set; }
        public string NextPageToken { get; set; }
    }

    public class MediaItem
    {
        public string Id {  get; set; }
        public string Description {  get; set;  }
        public string ProductUrl {  get; set; }
        public string BaseUrl { get; set; }
        public string MimeType { get; set; }
        public string Filename { get; set; }
        public MediaMetadata mediaMetadata { get; set; }
        public ContributorInfo contributorInfo { get; set; }
    }

    public class MediaMetadata
    {
        public DateTimeOffset CreationTime { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
        public Photo Photo {  get; set; }
    }

    public class ContributorInfo { }

    public class Photo
    {
        public string CameraMake { get; set; }
        public string CameraModel { get; set; }
        public decimal FocalLength { get; set; }
        public decimal ApertureFNumber { get; set; }
        public decimal IsoEquivalent { get; set; }
        public string ExposureTime { get; set; }
    }
}