using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Refit;

namespace GooglePhotoSync.Google.Api
{
    [Headers("Authorization: Bearer X")]
    public interface IGooglePhotosApi
    {
        /* Albums */
        [Get("/v1/albums")]
        Task<GetAlbumsResponse> GetAlbums([Query]GetAlbumsRequest request);
        
        [Post("/v1/albums")]
        Task<GoogleAlbum> CreateAlbum([Body]PostAlbumRequest request);
        
        [Post("/v1/albums/{albumId}:batchAddMediaItems")]
        Task<GoogleAlbum> AddMediaItemsToAlbum(string albumId, [Body]AddMediaItemsToAlbumRequest request);

        /* Uploads */
        
        [Post("/v1/uploads")]
        [Headers("Content-type: application/octet-stream", "X-Goog-Upload-Protocol: raw")]
        Task<string> UploadFile([Body]Stream request, [Header("X-Goog-Upload-Content-Type")]string mimeType);

        /* Media Items */

        [Post("/v1/mediaItems:batchCreate")]
        Task<BatchCreateMediaItemsResponse> BatchCreateMediaItems([Body]BatchCreateMediaItemsRequest request);

        [Post("/v1/mediaItems:search")]
        Task<SearchMediaItemsResponse> SearchMediaItems([Body]SearchMediaItemsRequest request);
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

    public class PostAlbumRequest
    {
        public NewAlbum album { get; set; }

        public class NewAlbum
        {
            public string title { get; set; }
        }
    }

    public class AddMediaItemsToAlbumRequest
    {
        public List<string> mediaItemIds { get; set; } = new List<string>();
    }

    public class BatchCreateMediaItemsRequest
    {
        public string albumId { get; set; }
        public List<BatchCreateMediaItemRequest> newMediaItems { get; set; } = new List<BatchCreateMediaItemRequest>();
    }

    public class BatchCreateMediaItemRequest
    {
        public string description { get; set; }
        public MediaItem simpleMediaItem { get; set; }

        public class MediaItem
        {
            public string fileName { get; set; }
            public string uploadToken { get; set; }
        }
    }

    public class BatchCreateMediaItemsResponse
    {
        public List<BatchCreateMediaItemResponse> newMediaItemResults { get; set; }
    }

    public class BatchCreateMediaItemResponse
    {
        public string uploadToken { get; set; }
        public Status status { get; set; }

        public class Status
        {
            public string message { get; set; }
            public int code { get; set; }
        }
    }

    //{
    //"newMediaItemResults": [
    //{
    //    "uploadToken": "upload-token",
    //    "status": {
    //        "message": "Success"
    //    },
    //    "mediaItem": {
    //        "id": "media-item-id",
    //        "description": "item-description",
    //        "productUrl": "https://photos.google.com/photo/photo-path",
    //        "mimeType": "mime-type",
    //        "mediaMetadata": {
    //            "width": "media-width-in-px",
    //            "height": "media-height-in-px",
    //            "creationTime": "creation-time",
    //            "photo": {}
    //        },
    //        "filename": "filename"
    //    }
    //},
    //{
    //    "uploadToken": "upload-token",
    //    "status": {
    //        "code": 13,
    //        "message": "Internal error"
    //    }
    //}
    //]
    //}


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