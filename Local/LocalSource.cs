using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GooglePhotoSync.Local
{
    public class LocalSource
    {
        private readonly LocalSettings m_LocalSettings;
        private readonly ILogger<LocalSource> m_Logger;

        private readonly DirectoryInfo m_RootDir;

        public List<LocalPhotoAlbum> PhotoAlbums { get; private set; }

        private int? m_TotalFiles;
        public int TotalFiles => m_TotalFiles ?? (m_TotalFiles = PhotoAlbums.Sum(a => a.TotalFiles)).Value;
        
        private long? m_TotalBytes;
        public long TotalBytes => m_TotalBytes ?? (m_TotalBytes = PhotoAlbums.Sum(a => a.TotalBytes)).Value;

        public LocalSource(IOptions<LocalSettings> localSettings, ILogger<LocalSource> logger)
        {
            m_Logger = logger;
            m_LocalSettings = localSettings.Value;
            m_RootDir = new DirectoryInfo(localSettings.Value.LocalFolderRoot);
        }

        public void Load()
        {
            PhotoAlbums = m_RootDir.EnumerateDirectories()
                                   .OrderBy(f => f.Name)
                                   .Where(IsNotIgnored)
                                   .Select(d => new LocalPhotoAlbum(d, m_LocalSettings, m_Logger))
                                   .ToList();
        }

        private bool IsNotIgnored(DirectoryInfo dir)
        {
            var name = dir.Name;
            return !m_LocalSettings.IgnoreFolderStartingWith.Any(s => name.StartsWith(s));
        }
    }

    public class LocalPhotoAlbum
    {
        private readonly DirectoryInfo m_Dir;
        private readonly LocalSettings m_LocalSettings;
        private readonly ILogger m_Logger;

        public string Name => m_Dir.Name;
        public List<LocalFile> Files { get; }
        
        public int TotalFiles => Files.Count;

        private long? m_TotalBytes;
        public long TotalBytes => m_TotalBytes ?? (m_TotalBytes = Files.Sum(f => f.Bytes)).Value;

        public LocalPhotoAlbum(DirectoryInfo dir, LocalSettings localSettings, ILogger logger)
        {
            m_Dir = dir;
            m_LocalSettings = localSettings;
            m_Logger = logger;

            Files = m_Dir.EnumerateFiles()
                         .Where(f => IsSupportedFileType(f) && IsWithinFileSize(f)) 
                         .Select(f => new LocalFile(f, localSettings, this))
                         .ToList();
        }

        private bool IsSupportedFileType(FileInfo file)
        {
            var isSupported = m_LocalSettings.ImageExtensions.Any(e => string.Compare(e, file.Extension, StringComparison.CurrentCultureIgnoreCase) == 0) ||
                   m_LocalSettings.VideoExtensions.Any(e => string.Compare(e, file.Extension, StringComparison.CurrentCultureIgnoreCase) == 0);
            
            if (!isSupported)
                m_Logger.LogInformation("Unsupported File {ext} Skipped: {name} [{size}]", file.Extension, file.FullName, file.Length.AsHumanReadableBytes("MB"));
            
            return isSupported;
        }

        private bool IsWithinFileSize(FileInfo file)
        {
            // TODO: Temp size filter - think large files need splitting
            var isUnderMax = file.Length <= m_LocalSettings.MaxFileSizeBytes;

            if (!isUnderMax)
                m_Logger.LogInformation("Large file Skipped: {name} [{size}]", file.FullName, file.Length.AsHumanReadableBytes("MB"));

            return isUnderMax;
        }

    }

    public class LocalFile
    {
        private readonly FileInfo m_File;
        private readonly LocalSettings m_LocalSettings;
        private readonly LocalPhotoAlbum m_Parent;

        public long Bytes => m_File.Length;

        private string m_MimeType;
        public string MimeType => m_MimeType ??= MimeTypes.GetMimeType(m_File.Extension);

        public bool IsImage => m_LocalSettings.ImageExtensions.Any(e => string.Compare(e, m_File.Extension, StringComparison.CurrentCultureIgnoreCase) == 0);
        public bool IsVideo => m_LocalSettings.VideoExtensions.Any(e => string.Compare(e, m_File.Extension, StringComparison.CurrentCultureIgnoreCase) == 0);

        public string FilePath => m_File.FullName;
        public string FileName => m_File.Name;
        public string ShortFilePath => $"{m_Parent.Name}\\{FileName}";

        public LocalFile(FileInfo file, LocalSettings localSettings, LocalPhotoAlbum parent)
        {
            m_File = file;
            m_LocalSettings = localSettings;
            m_Parent = parent;
        }

        public Stream OpenStream()
        {
            return m_File.OpenRead();
        }
    }
}
