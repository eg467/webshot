using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using WebshotService.Entities;

namespace WebshotService.ProjectStore
{
    public sealed class FileProjectStore : IProjectStore
    {
        public const string ProjectFilename = "webshots.wsproj";

        private readonly FileStore<Project> _filestore;

        public event EventHandler<ProjectSavedEventArgs>? Saved;

        public string Id => ProjectPath;
        public bool Exists => _filestore.FileExists;
        public string ProjectPath => _filestore.FilePath;

        /// <summary>
        ///
        /// </summary>
        /// <remarks>Use the directory instead of project file path to ensure only one can be open at a time</remarks>
        public string ProjectDir =>
            Path.GetDirectoryName(ProjectPath) ?? throw new DirectoryNotFoundException();

        public string ScreenshotDir => Path.Combine(ProjectDir, "Screenshots");

        private FileSessionStore CreateSessionStore(string? session = null) =>
            new FileSessionStore(ScreenshotDir, session);

        public FileProjectStore(string projectDir)
        {
            if (projectDir.Contains(ProjectFilename) || File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir) ?? throw new DirectoryNotFoundException();
            }
            Directory.CreateDirectory(projectDir);

            var filePath = Path.Combine(projectDir, ProjectFilename);
            _filestore = new FileStore<Project>(filePath);
        }

        public static string UserAppProjectsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Webshot",
                "Projects");

        /// <summary>
        ///
        /// </summary>
        /// <param name="temporaryDir">
        /// True if the data should be stored in a temp directory,
        /// False to store in the application directory</param>
        /// <returns></returns>
        public static string CreateTempProjectDirectory(bool temporaryDir)
        {
            static string GetTempDir() => Path.Combine(Path.GetTempPath(), "Webshot");
            string basePath = temporaryDir ? GetTempDir() : UserAppProjectsPath;
            var timestamp = DateTime.Now.Timestamp();

            var path = Path.Combine(basePath, $"{timestamp}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private Project CreateProject()
        {
            Project project = new() { Id = Id };
            Save(project);
            return project;
        }

        /// <summary>
        /// Loads a project or creates a new one if it doesn't exist.
        /// </summary>
        /// <returns></returns>
        public Project Load() => Exists ? _filestore.Load() : CreateProject();

        public void Save(Project project)
        {
            _filestore.Save(project);
            ProjectSavedEventArgs args = new(project);
            Saved?.Invoke(this, args);
        }

        public Image GetImage(string sessionId, DeviceScreenshotFile file) =>
            CreateSessionStore(sessionId).GetImage(file);

        public List<(string Id, ScreenshotResults Result)> GetResultsBySessionId() =>
            FileSessionStore.FromDirectory(ScreenshotDir)
                .Where(x => x.HasManifest)
                .Select(x => (x.Id, Result: x.Load()))
                .OrderBy(x => x.Result.Timestamp)
                .ToList();

        public string CreateSession() => CreateSessionStore().Id;

        public string GetSessionDirectory(string sessionId) => CreateSessionStore(sessionId).DirectoryPath;

        public void SaveResults(string sessionId, ScreenshotResults results)
        {
            var sessionStore = CreateSessionStore(sessionId);
            sessionStore.Save(results);
        }
    }

    /// <summary>
    /// Stores the results of screenshot sessions in separate directories
    /// </summary>
    public class FileSessionStore : IObjectStore<ScreenshotResults>
    {
        public const string ScreenshotManifestFilename = "screenshots.manifest";

        public string Id { get; }
        public string DirectoryPath { get; }
        public string ManifestPath => Path.Combine(DirectoryPath, ScreenshotManifestFilename);
        public bool HasManifest => ManifestStore.FileExists;

        private FileStore<ScreenshotResults> ManifestStore => new(ManifestPath);

        public static IEnumerable<FileSessionStore> FromDirectory(string baseDir) =>
            Directory.GetDirectories(baseDir).Select(Load);

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
            if (sessionId?.Length == 0) throw new ArgumentNullException(nameof(sessionId));

            sessionId ??= $"{DateTime.Now.Timestamp()}-{Guid.NewGuid():N}";
            Id = Utils.SanitizeFilename(sessionId);
            DirectoryPath = Path.Combine(baseDir, Id);
            Directory.CreateDirectory(DirectoryPath);
        }

        public Image GetImage(DeviceScreenshotFile file)
        {
            var filename = file.Result.PathsByDevice[file.Device];
            var path = Path.Combine(DirectoryPath, filename);
            return Image.FromFile(path);
        }

        public void Save(ScreenshotResults obj) => ManifestStore.Save(obj);

        public ScreenshotResults Load() => ManifestStore.Load();
    }

    public class FileProjectStoreFactory : IProjectStoreFactory
    {
        public IProjectStore Create(string projectId) =>
            new FileProjectStore(projectId);
    }

    public interface IProjectStoreFactory
    {
        IProjectStore Create(string projectId);
    }
}