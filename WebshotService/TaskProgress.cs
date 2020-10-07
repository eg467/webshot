using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WebshotService
{
    public record TaskProgress
    {
        public int CurrentIndex { get; init; }
        public int Count { get; init; }
        public string CurrentItem { get; init; }

        public TaskProgress(int currentIndex, int count, string currentItem)
        {
            this.CurrentIndex = currentIndex;
            this.Count = count;
            this.CurrentItem = currentItem;
        }
    }

    public static class IProgressExtension
    {
        public static void Report(this IProgress<TaskProgress> progress, int index, int count, string msg)
        {
            TaskProgress currentProgress = new(index, count, msg);
            progress.Report(currentProgress);
        }
    }
}