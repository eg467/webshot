using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;
using WebshotService;

namespace WebShot.Menu
{
    /// <summary>
    /// Allows user an option to cancel a task using console input.
    /// </summary>
    internal class CancellableConsoleTask : IDisposable
    {
        private enum Status { Running, UserCancelled, Suspended }

        private const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIoEx(IntPtr handle, IntPtr lpOverlapped);

        private int _initialLine;
        private int _endLine = -1;

        private readonly Progress<TaskProgress> _progress = new();
        public IProgress<TaskProgress> Progress => _progress;

        private Status _status = Status.Running;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        public CancellationToken Token => _cancellationTokenSource.Token;

        public CancellableConsoleTask()
        {
            _progress.ProgressChanged += Progress_ProgressChanged;
        }

        private string _recentProgress = "Progress...";
        private readonly ConsoleKey _cancelKey = ConsoleKey.Escape;
        private int _lastWriteLine = -1;

        private void Progress_ProgressChanged(object? sender, TaskProgress e)
        {
            // The last progress status was the last thing written
            int numProgressLines = 2; // The progress bar and the "press any key" prompt.

            // Overwrite only if nothing has been written elsewhere since the last update.
            bool overwritePreviousProgress = _lastWriteLine != -1 && Console.CursorTop == _lastWriteLine + numProgressLines;

            Debug.WriteLine($"Progress_ProgressChanged({e.CurrentItem}): lastwriteline = {_lastWriteLine}, Console.CursorTop = {Console.CursorTop}, overwrite = {overwritePreviousProgress}");

            if (overwritePreviousProgress)
                Console.SetCursorPosition(0, Math.Max(0, _lastWriteLine));

            _recentProgress = $"{e.Index}/{e.Count} ({e.CurrentItem})";
            WritePrompt();
        }

        public void MonitorKeypress()
        {
            ConsoleKeyInfo cki = new();
            do
            {
                WritePrompt();
                try
                {
                    cki = Console.ReadKey(true);
                }
                catch (InvalidOperationException)
                {
                    Debug.WriteLine("User cancel");
                    return;
                }
            } while (cki.Key != ConsoleKey.Escape);
            _cancellationTokenSource.Cancel();
            _status = Status.UserCancelled;
        }

        public Task CompleteOrCancel(Task task)
        {
            // Adapted from: https://stackoverflow.com/questions/9479573/how-to-interrupt-console-readline
            // Also: https://darchuk.net/2019/02/08/waiting-for-a-keypress-asynchronously-in-a-c-console-app/

            if (task.IsCompleted)
                return task;

            _endLine = Console.CursorTop;

            Task t = task.ContinueWith(_ =>
            {
                if (_status != Status.UserCancelled)
                {
                    var handle = GetStdHandle(STD_INPUT_HANDLE);
                    CancelIoEx(handle, IntPtr.Zero);
                }

                try
                {
                    if (_lastWriteLine >= 0)
                        Console.SetCursorPosition(0, _lastWriteLine);
                }
                catch (Exception ex)
                {

                    throw;
                }
                
            });

            return Task.Run(() => MonitorKeypress());
        }

        private void WritePrompt()
        {
            _lastWriteLine = Console.CursorTop;
            var padding = new string(' ', Console.BufferWidth - _recentProgress.Length);
            var message = _recentProgress + padding;
            Debug.WriteLine($"WritePrompt({message}): lastwriteline = {_lastWriteLine}, Console.CursorTop = {Console.CursorTop}");
            new ColoredOutput(message, ConsoleColor.Black, ConsoleColor.White).WriteLine();
            Console.WriteLine($"Press {_cancelKey} to cancel operation...");
        }

        public void Dispose()
        {
            _progress.ProgressChanged -= Progress_ProgressChanged;
            _cancellationTokenSource.Dispose();
        }
    }
}