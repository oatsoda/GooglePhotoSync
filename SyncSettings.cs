namespace GooglePhotoSync
{
    public class SyncSettings
    {
        public bool PromptBeforeEachAlbumSync { get; set; }
        public int BatchSize { get; set; }
        public int ParallelUploads { get; set; }
    }
}