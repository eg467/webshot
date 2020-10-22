using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WebshotService.Entities;

namespace WebshotService.State
{
    public static class SelectorExtensions
    {
        public static ScheduledProject? NextScheduledProject(this ApplicationState state) =>
            state.SchedulerState.ScheduledProjects
                .Where(p => p.Enabled)
                .OrderByDescending(p => p.RunImmediately)
                .ThenBy(p => p.ScheduledFor)
                .ThenBy(p => p.ProjectName)
                .FirstOrDefault();
    }
}