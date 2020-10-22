using System;
using System.Collections.Generic;
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

        private int _initialLine = 0;
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
        private int? _lastWriteLine;

        private void Progress_ProgressChanged(object? sender, TaskProgress e)
        {
            // The last progress status was the last thing written
            int numProgressLines = 2; // The progress bar and the "press any key" prompt.
            bool overwritePreviousProgress = _lastWriteLine.HasValue && Console.CursorTop == _lastWriteLine + numProgressLines;

            if (overwritePreviousProgress)
                Console.SetCursorPosition(0, _lastWriteLine!.Value);

            _recentProgress = $"{e.Index}/{e.Count} ({e.CurrentItem})";
            WritePrompt();
        }

        public Task CompleteOrCancel(Task task)
        {
            // Adapted from: https://stackoverflow.com/questions/9479573/how-to-interrupt-console-readline

            if (task.IsCompleted)
                return task;

            (_, _initialLine) = Console.GetCursorPosition();
            _endLine = _initialLine;

            // Start the timeout
            Task t = task.ContinueWith(_ =>
            {
                if (_status != Status.UserCancelled)
                {
                    // Timeout => cancel the console read
                    var handle = GetStdHandle(STD_INPUT_HANDLE);
                    CancelIoEx(handle, IntPtr.Zero);
                }

                Console.SetCursorPosition(0, _endLine);
            });

            try
            {
                // Start reading from the console
                while (true)
                {
                    WritePrompt();

                    ConsoleKeyInfo key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Escape)
                    {
                        _cancellationTokenSource.Cancel();
                        _status = Status.UserCancelled;
                        break;
                    }
                }
            }
            // Handle the exception when the operation is canceled
            catch (InvalidOperationException)
            {
                throw new OperationCanceledException();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation canceled OperationCanceledException");
            }
            finally
            {
                Console.SetCursorPosition(0, _endLine);
            }

            return t;
        }

        private void WritePrompt()
        {
            _lastWriteLine = Console.CursorTop;
            var padding = new string(' ', Console.BufferWidth - _recentProgress.Length);
            new ColoredOutput(_recentProgress + padding, ConsoleColor.Black, ConsoleColor.White).WriteLine();
            Console.WriteLine($"Press {_cancelKey} to cancel operation...");
        }

        public void Dispose()
        {
            _progress.ProgressChanged -= Progress_ProgressChanged;
            _cancellationTokenSource.Dispose();
        }
    }
}