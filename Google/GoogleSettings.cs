namespace GooglePhotoSync.Google
{
    public class GoogleSettings
    {
        public string GooglePhotosApiBaseUrl { get; set; }
        public string GoogleClientId { get; set; }
        public string GoogleClientSecret { get; set; }
        public string[] GooglePhotoScopes { get; set; }
        public string GoogleTokenUrl { get; set; }

        /// <summary>Files larger than this use resumable (chunked) upload; smaller files use a single raw upload.</summary>
        public int ResumableUploadThresholdMB { get; set; }
        public long ResumableUploadThresholdBytes => ResumableUploadThresholdMB * 1024L * 1024L;

        /// <summary>Size of each resumable upload chunk. Rounded down to a multiple of Google's chunk granularity (256KB).</summary>
        public int ResumableUploadChunkSizeMB { get; set; }
        public long ResumableUploadChunkSizeBytes => ResumableUploadChunkSizeMB * 1024L * 1024L;
    }
}