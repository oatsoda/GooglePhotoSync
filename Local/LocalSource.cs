using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;

namespace GooglePhotoSync.Local
{
    public class LocalSource
    {
        private readonly LocalSettings m_LocalSettings;
        private readonly DirectoryInfo m_RootDir;

        public List<LocalPhotoAlbum> PhotoAlbums { get; private set; }

        private int? m_TotalFiles;
        public int TotalFiles => m_TotalFiles ?? (m_TotalFiles = PhotoAlbums.Sum(a => a.TotalFiles)).Value;
        
        private long? m_TotalBytes;
        public long TotalBytes => m_TotalBytes ?? (m_TotalBytes = PhotoAlbums.Sum(a => a.TotalBytes)).Value;

        public LocalSource(IOptions<LocalSettings> localSettings)
        {
            m_LocalSettings = localSettings.Value;
            m_RootDir = new DirectoryInfo(localSettings.Value.LocalFolderRoot);
        }

        public void Load()
        {
            PhotoAlbums = m_RootDir.EnumerateDirectories()
                                   .OrderBy(f => f.Name)
                                   .Where(IsNotIgnored)
                                   .Select(d => new LocalPhotoAlbum(d, m_LocalSettings))
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

        public string Name => m_Dir.Name;
        public List<LocalFile> Files { get; }
        
        public int TotalFiles => Files.Count;

        private long? m_TotalBytes;
        public long TotalBytes => m_TotalBytes ?? (m_TotalBytes = Files.Sum(f => f.Bytes)).Value;

        public LocalPhotoAlbum(DirectoryInfo dir, LocalSettings localSettings)
        {
            m_Dir = dir;
            m_LocalSettings = localSettings;

            Files = m_Dir.EnumerateFiles()
                         .Where(IsNotIgnored)
                         .Select(f => new LocalFile(f, localSettings))
                         .ToList();
        }

        private bool IsNotIgnored(FileInfo file)
        {
            return m_LocalSettings.ImageExtensions.Any(e => string.Compare(e, file.Extension, StringComparison.CurrentCultureIgnoreCase) == 0) ||
                   m_LocalSettings.VideoExtensions.Any(e => string.Compare(e, file.Extension, StringComparison.CurrentCultureIgnoreCase) == 0);
        }

    }

    public class LocalFile
    {
        private readonly FileInfo m_File;
        private readonly LocalSettings m_LocalSettings;

        public long Bytes => m_File.Length;

        private string m_MimeType;
        public string MimeType => m_MimeType ??= MimeTypes.GetMimeType(m_File.Extension);

        public bool IsImage => m_LocalSettings.ImageExtensions.Any(e => string.Compare(e, m_File.Extension, StringComparison.CurrentCultureIgnoreCase) == 0);
        public bool IsVideo => m_LocalSettings.VideoExtensions.Any(e => string.Compare(e, m_File.Extension, StringComparison.CurrentCultureIgnoreCase) == 0);

        public string FilePath => m_File.FullName;
        public string FileName => m_File.Name;

        public LocalFile(FileInfo file, LocalSettings localSettings)
        {
            m_File = file;
            m_LocalSettings = localSettings;
        }

        public Stream OpenStream()
        {
            return m_File.OpenRead();
        }
    }
}