using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GooglePhotoSync.Sync
{
    public class CollectionSync
    {
        private readonly AlbumSync m_Sync;
        private readonly SyncSettings m_Settings;
        private readonly ILogger<CollectionSync> m_Logger;

        private bool m_ContinueForAll;

        public CollectionSync(AlbumSync sync, IOptions<SyncSettings> settings, ILogger<CollectionSync> logger)
        {
            m_Sync = sync;
            m_Settings = settings.Value;
            m_Logger = logger;
        }

        public async Task SyncCollection(CollectionDiff diff)
        {
            foreach (var partial in diff.PartialSyncedAlbums)
            {
                m_Logger.LogInformation($"Syncing partial: '{partial.Local.Name}' unsynced {partial.UnsyncedPhotos.Count} [{partial.UnsyncedPhotoTotalBytes.AsHumanReadableBytes("MB")}] (Local: {partial.Local.TotalFiles}, Google: {partial.Google.MediaItemsCount})");
                
                if (!PromptIfRequired())
                    return;
                
                await m_Sync.SyncPartialAlbum(partial);
            }

            foreach (var local in diff.NeverSyncedAlbums)
            {
                m_Logger.LogInformation($"Syncing never synced: '{local.Name}' {local.TotalFiles} [{local.TotalBytes.AsHumanReadableBytes("MB")}]");

                if (!PromptIfRequired())
                    return;

                await m_Sync.SyncAlbum(local, null);
            }
        }        

        private bool PromptIfRequired()
        {
            if (!m_Settings.PromptBeforeEachAlbumSync || m_ContinueForAll)
                return true;
            
            var waitResult = ConsoleLogic.WaitForConfirmation();
            if (waitResult == ConsoleLogic.WaitResult.Stop)
            {
                Console.WriteLine("Stop requested...");
                return false;
            }
            else if (waitResult == ConsoleLogic.WaitResult.ContinueForAll)
            {
                m_ContinueForAll = true;
            }
            
            return true;
        }
    }     

    public static class ConsoleLogic
    {
        private static readonly char[] _validKeys = new[] { 'n', 'N', 'y', 'Y', 'a', 'A' };
        public static WaitResult WaitForConfirmation()
        {
            ConsoleKeyInfo k;
            do
            {
                Console.Write("Continue? [Y = Yes, N = No, A = Yes for All]: ");
                k = Console.ReadKey();
                Console.WriteLine();
            } while (!_validKeys.Contains(k.KeyChar));

            return k.KeyChar switch
            {
                'n' => WaitResult.Stop,
                'N' => WaitResult.Stop,
                'y' => WaitResult.Continue,
                'Y' => WaitResult.Continue,
                'a' => WaitResult.ContinueForAll,
                'A' => WaitResult.ContinueForAll,
                _ => throw new InvalidOperationException($"Unexpected value: '{k.KeyChar}'")
            };
        }

        public enum WaitResult
        {
            Stop,
            Continue,
            ContinueForAll
        }
    }
}