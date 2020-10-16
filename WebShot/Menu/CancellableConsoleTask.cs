using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        private void Progress_ProgressChanged(object? sender, TaskProgress e)
        {
            _recentProgress = $"{e.Index}/{e.Count} ({e.CurrentItem})";

            if (_endLine == -1)
                return;

            Console.SetCursorPosition(0, _initialLine);
            Console.WriteLine(_recentProgress);
            Console.SetCursorPosition(0, _endLine);
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
                if (_status == Status.UserCancelled)
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
                var cancelKey = ConsoleKey.Escape;
                while (true)
                {
                    if (_progress is object)
                        Console.WriteLine(_recentProgress);

                    Console.WriteLine($"Press {cancelKey} to cancel operation...");
                    (_, _endLine) = Console.GetCursorPosition();
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

        public void Dispose()
        {
            _progress.ProgressChanged -= Progress_ProgressChanged;
            _cancellationTokenSource.Dispose();
        }
    }
}