using System;
using System.Collections.Generic;
using System.Drawing;
using WebshotService.Entities;

namespace WebshotService.ProjectStore
{
    public interface IProjectStore : IObjectStore<Project>
    {
        /// <summary>
        /// A unique identifier for the project (e.g. project file path or DB index).
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Fired when the options, input, or results for the project are persisted.
        /// </summary>
        event EventHandler<ProjectSavedEventArgs> Saved;

        /// <summary>
        /// True if the object exists and can be loaded
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Retrieves a saved screenshot from the project
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        Image GetImage(string sessionId, DeviceScreenshotFile file);

        /// <summary>
        /// Creates a session and its directory.
        /// </summary>
        /// <returns>The Id of the session.</returns>
        string CreateSession();

        string GetSessionDirectory(string sessionId);

        void SaveResults(string sessionId, ScreenshotResults results);

        /// <summary>
        /// Retrieves all screenshots and timings for this project.
        /// </summary>
        /// <returns></returns>
        List<(string Id, ScreenshotResults Result)> GetResultsBySessionId();
    }
}