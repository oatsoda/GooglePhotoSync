namespace GooglePhotoSync.Local
{
    public class LocalSettings
    {
        public string LocalFolderRoot { get; set; }
        public string[] IgnoreFolderStartingWith { get; set; }
        public string[] ImageExtensions { get; set; }
        public string[] VideoExtensions { get; set; }
    }
}