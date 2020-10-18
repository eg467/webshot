using OpenQA.Selenium.DevTools.DOM;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using WebshotService.Entities;

namespace WebshotService.ProjectStore
{
    public sealed class FileProjectStore : IProjectStore
    {
        public const string ProjectFilename = "webshots.wsproj";

        private readonly FileStore<Project> _filestore;

        public event EventHandler<ProjectSavedEventArgs>? Saved;

        public string Id => ProjectPath;
        public bool Exists => _filestore.Exists;
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

        /// <summary>
        ///
        /// </summary>
        /// <param name="filePath">The path of the project file <see cref="ProjectFilename"/> or an existing directory that does or should contain it.</param>
        /// <exception cref="ArgumentException"></exception>
        public FileProjectStore(string filePath)
        {
            if (Directory.Exists(filePath))
            {
                filePath = Path.Combine(filePath, ProjectFilename);
            }
            else if (!filePath.EndsWith(ProjectFilename, StringComparison.Ordinal))
            {
                throw new FileNotFoundException("No such directory or project file found.");
            }
            else
            {
                string projectDir = Path.GetDirectoryName(filePath) ?? throw new DirectoryNotFoundException();
                Directory.CreateDirectory(projectDir);
            }

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

        /// <summary>
        /// Loads a project or creates a new one if it doesn't exist.
        /// </summary>
        /// <returns></returns>
        public Project Load()
        {
            Project project = Exists ? _filestore.Load() : new();

            // The actual file path of the current project file takes precedence over whatever the saved path is.
            if (!Id.Equals(project.Id, StringComparison.Ordinal))
            {
                project = project with { Id = Id };
                Save(project);
            }

            return project;
        }

        public void Save(Project project)
        {
            _filestore.Save(project);
            ProjectSavedEventArgs args = new(project);
            Saved?.Invoke(this, args);
        }

        public Image? GetImage(string sessionId, Uri uri, Device device)
        {
            var sessionStore = CreateSessionStore(sessionId);
            var sessionStats = sessionStore.Load();
            var deviceScreenshots = sessionStats.PageScreenshots.FirstOrDefault(s => s.Uri.Equals(uri));
            if (deviceScreenshots is null)
                return null;
            var path = deviceScreenshots.DeviceScreenshots.TryGetValue(device, out var screenshot) ? screenshot.Path : null;
            return path is object ? Image.FromFile(path) : null;
        }

        public List<(string Id, SessionScreenshots Result)> GetResultsBySessionId() =>
            FileSessionStore.FromDirectory(ScreenshotDir)
                .Where(x => x.Exists)
                .Select(x => (x.Id, Result: x.Load()))
                .OrderBy(x => x.Result.Timestamp)
                .ToList();

        public Dictionary<Uri, List<PageScreenshots>> GetResultsByUri()
        {
            var a = FileSessionStore.FromDirectory(ScreenshotDir);
            var a2 = a
                .Where(x => x.Exists)
                .Select(x => x.Load());

            var b = a2
                .SelectMany(x => x.PageScreenshots);

            var c = b
                .GroupBy(x => x.Uri, x => x)
                .ToDictionary(x => x.Key, x => x.ToList());

            return c;
        }

        public string CreateSession() => CreateSessionStore().Id;

        public string GetSessionDirectory(string sessionId) => CreateSessionStore(sessionId).DirectoryPath;

        public void SaveResults(string sessionId, SessionScreenshots results)
        {
            var sessionStore = CreateSessionStore(sessionId);
            sessionStore.Save(results);
        }
    }

    public class FileProjectStoreFactory : IProjectStoreFactory
    {
        public IProjectStore Create(string projectId)
        {
            return new FileProjectStore(projectId);
        }
    }

    public interface IProjectStoreFactory
    {
        IProjectStore Create(string projectId);
    }
}