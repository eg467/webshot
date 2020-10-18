using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using WebshotService.Entities;

namespace WebshotService.ProjectStore
{
    /// <summary>
    /// Stores the results of screenshot sessions in separate directories
    /// </summary>
    public class FileSessionStore : IObjectStore<SessionScreenshots>
    {
        public const string ScreenshotManifestFilename = "screenshots.manifest";

        public string Id { get; }
        public string DirectoryPath { get; }
        public string ManifestPath => Path.Combine(DirectoryPath, ScreenshotManifestFilename);
        public bool Exists => ManifestStore.Exists;

        private FileStore<SessionScreenshots> ManifestStore => new(ManifestPath);

        public static IEnumerable<FileSessionStore> FromDirectory(string baseDir) =>
            Directory.Exists(baseDir)
                ? Directory.GetDirectories(baseDir).Select(Load)
                : Enumerable.Empty<FileSessionStore>();

        /// <summary>
        /// Loads a store from an existing session.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static FileSessionStore Load(string dir)
        {
            var basedir = Path.GetDirectoryName(dir) ?? throw new ArgumentException("Invalid session base directory.");
            var sessionId = Path.GetFileName(dir) ?? throw new ArgumentException("Invalid session id.");
            return new FileSessionStore(basedir, sessionId);
        }

        /// <summary>
        /// Creates or loads a store.
        /// </summary>
        /// <param name="baseDir">The directory that contains all session subdirectories.</param>
        /// <param name="Id">The subdirectory that contains the current session results. Null to autogenerate a directory.</param>
        public FileSessionStore(string baseDir, string? sessionId = null)
        {
            if (sessionId?.Length == 0)
                throw new ArgumentException("Parameter can by null but not empty.", nameof(sessionId));

            sessionId ??= $"{DateTime.Now.Timestamp()}-{Guid.NewGuid():N}";
            Id = Utils.SanitizeFilename(sessionId);
            DirectoryPath = Path.Combine(baseDir, Id);
            Directory.CreateDirectory(DirectoryPath);
        }

        public void Save(SessionScreenshots obj) => ManifestStore.Save(obj);

        public SessionScreenshots Load() => ManifestStore.Load();
    }
}