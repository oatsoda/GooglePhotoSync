namespace GooglePhotoSync.Local
{
    public class LocalSettings
    {
        public string LocalFolderRoot { get; set; }
        public string[] IgnoreFolderStartingWith { get; set; }
        public int MaxFileSizeMB { get; set; }
        public long MaxFileSizeBytes => MaxFileSizeMB * 1024L * 1024L;
        public string[] ImageExtensions { get; set; }
        public string[] VideoExtensions { get; set; }
    }
}