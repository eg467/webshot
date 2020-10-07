using System;
using WebshotService.Entities;

namespace WebshotService.ProjectStore
{
    public class ProjectSavedEventArgs : EventArgs
    {
        public Project Project { get; }

        public ProjectSavedEventArgs(Project project)
        {
            Project = project;
        }
    }
}