using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;

namespace LaCompta.Services
{
    /// <summary>
    /// Removes legacy single-DB files and prunes per-save DBs whose Stardew save
    /// has been deleted. Stardew has no save-deletion event, so we reconcile at
    /// every GameLaunched and ReturnedToTitle.
    /// </summary>
    public class SaveCleanupService
    {
        private readonly string _modPath;
        private readonly IMonitor _monitor;

        public SaveCleanupService(string modPath, IMonitor monitor)
        {
            _modPath = modPath;
            _monitor = monitor;
        }

        /// <summary>
        /// Delete the legacy <c>Mods/LaCompta/lacompta.db</c> from the v0.1.2 era.
        /// One-shot per launch — once gone, this is a no-op. Mixed-save data in
        /// that file is unrecoverable, so users get a clean slate per the spec.
        /// </summary>
        public void CleanupLegacyDatabase()
        {
            var legacyPath = Path.Combine(_modPath, "lacompta.db");
            if (!File.Exists(legacyPath))
                return;

            try
            {
                File.Delete(legacyPath);
                _monitor.Log("Removed legacy lacompta.db (pre-0.1.3 mono-save layout).", LogLevel.Info);
            }
            catch (IOException ex)
            {
                _monitor.Log($"Could not remove legacy lacompta.db: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>
        /// Delete any <c>data/*.db</c> whose save folder no longer exists under
        /// <see cref="Constants.SavesPath"/>. Safe to call repeatedly.
        /// </summary>
        public void PruneOrphanDatabases()
        {
            var dataDir = Path.Combine(_modPath, "data");
            if (!Directory.Exists(dataDir))
                return;

            if (!Directory.Exists(Constants.SavesPath))
                return;

            HashSet<string> liveSaves;
            try
            {
                liveSaves = Directory.EnumerateDirectories(Constants.SavesPath)
                                     .Select(Path.GetFileName)
                                     .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            }
            catch (System.Exception ex)
            {
                _monitor.Log($"Could not enumerate Stardew saves at '{Constants.SavesPath}': {ex.Message}. Skipping prune.", LogLevel.Warn);
                return;
            }

            foreach (var dbPath in Directory.EnumerateFiles(dataDir, "*.db"))
            {
                var saveName = Path.GetFileNameWithoutExtension(dbPath);
                if (liveSaves.Contains(saveName))
                    continue;

                try
                {
                    File.Delete(dbPath);
                    _monitor.Log($"Pruned orphan DB for deleted save '{saveName}'.", LogLevel.Info);
                }
                catch (IOException ex)
                {
                    _monitor.Log($"Could not prune orphan DB '{saveName}': {ex.Message}", LogLevel.Warn);
                }
            }
        }
    }
}
