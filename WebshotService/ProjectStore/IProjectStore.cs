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
        /// Retrieves a saved screenshot from the project
        /// </summary>
        /// <returns></returns>
        Image? GetImage(string sessionId, Uri uri, Device device);

        /// <summary>
        /// Creates a session and its directory.
        /// </summary>
        /// <returns>The Id of the session.</returns>
        string CreateSession();

        string GetSessionDirectory(string sessionId);

        void SaveResults(string sessionId, SessionScreenshots results);

        /// <summary>
        /// Retrieves all screenshots and timings for this project.
        /// </summary>
        /// <returns></returns>
        List<(string Id, SessionScreenshots Result)> GetResultsBySessionId();
    }
}