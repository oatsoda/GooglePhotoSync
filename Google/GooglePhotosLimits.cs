namespace GooglePhotoSync.Google
{
    /// <summary>
    /// Google Photos' maximum accepted file sizes (https://developers.google.com/photos/library/guides/upload-media).
    /// Google states these as "200 MB" and "20 GB"; decimal units are used here as the conservative reading.
    /// </summary>
    public static class GooglePhotosLimits
    {
        public const long MaxImageBytes = 200L * 1000 * 1000;
        public const long MaxVideoBytes = 20L * 1000 * 1000 * 1000;
    }
}
