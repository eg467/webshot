using ImageProcessor.Common.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WebshotService.Lighthouse
{
    public enum EmulatedDevice { Mobile, Desktop }

    public enum ThrottleMode { DevTools, Provided, Simulate }

    [Flags]
    public enum OutputType : uint { None = 0, Csv = 1 << 0, Json = 1 << 1, Html = 1 << 2 }

    /// <summary>
    /// Provides access to the locally installed lighthouse tool for performance benhmarking.
    /// </summary>
    public class Lighthouse
    {
        private const string LighthouseFilename = "lighthouse";

        public static class Categories
        {
            public const string Performance = "performance";
            public const string BestPractices = "best-practices";
            public const string Accessibility = "accessibility";
            public const string Pwa = "pwa";
            public const string Seo = "seo";

            public static string[] GetAll() =>
                typeof(Categories).GetAllPublicConstantValues<string>().ToArray()!;
        }

        private string[] _categories = new string[] { Categories.Performance, Categories.Seo, Categories.BestPractices };

        public string[] TestCategories
        {
            get => _categories;
            set
            {
                var availableCategories = Categories.GetAll();
                if (value.Length == 0 || value.Except(availableCategories).Any())
                {
                    string categoryList = string.Join(", ", availableCategories);
                    throw new ArgumentException($"You must select at least one of: {categoryList}");
                }
                _categories = value;
            }
        }

        private string CategoriesFlag => $"--only-categories={string.Join(",", _categories).ToLower()}";

        public EmulatedDevice Device { get; set; } = EmulatedDevice.Mobile;
        private string DeviceFlag => $"--emulated-form-factor=\"{Device.ToString().ToLower()}\"";

        public bool Quiet { get; set; } = true;
        private string QuietFlag => Quiet ? "--quiet  --chrome-flags=\"--headless\"" : "";

        public ThrottleMode ThrottleMode { get; set; } = ThrottleMode.Simulate;
        private string ThrottleFlag => $"--throttling-method={ThrottleMode.ToString().ToLower()}";

        /// <summary>
        /// Set individually with each request.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string OutputPathFlag(string path)
        {
            static bool IsPowerOfTwo(uint x) => (x & (x - 1)) == 0;
            var oneSelected = OutputType != OutputType.None && IsPowerOfTwo((uint)OutputType);

            // Lighthouse automatically appends extensions when multiple output types are specified.
            // If only one is requested, ensure the output path has an appropriate extension.

            if (oneSelected)
            {
                var extension = OutputType.ToString().ToLower();
                if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    path = $"{path}.{extension}";
                }
            }

            return !string.IsNullOrEmpty(path) ? $"--output-path=\"{path}\"" : "";
        }

        public OutputType OutputType { get; set; } = OutputType.Json | OutputType.Html;

        private string OutputFlag
        {
            get
            {
                var outputs = Enum.GetValues(typeof(OutputType))
                    .Cast<OutputType>()
                    .Where(t => t != OutputType.None && OutputType.HasFlag(t))
                    .Select(t => t.ToString().ToLower());
                return $"--output={string.Join(",", outputs)}";
            }
        }

        private static string WhichCommand =>
            Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => "where",
                PlatformID.Unix => "which",
                _ => throw new PlatformNotSupportedException("Lighthouse will only work on Windows or Unix.")
            };

        public static bool IsInstalled
        {
            get
            {
                try
                {
                    return !string.IsNullOrEmpty(GetLighthousePath());
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private static string GetLighthousePath()
        {
            var info = new ProcessStartInfo(WhichCommand, LighthouseFilename)
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };

            using var p = Process.Start(info);
            if (p is null)
            {
                throw new Exception("Lighthouse path could not be found.");
            }

            string? file = null;
            do
            {
                file = p.StandardOutput.ReadLine();
            } while (file is not null && !file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase));

            if (file is null || !File.Exists(file))
            {
                throw new InvalidOperationException("Lighthouse, Google Chrome, and Node.js must be installed to use this feature.");
            }
            return file;
        }

        private readonly string _lighthousePath;

        public Lighthouse(string[]? categories = null)
        {
            _lighthousePath = GetLighthousePath();
            if (categories is not null)
            {
                TestCategories = categories;
            }
        }

        public async Task AnalyzeUrlAsync(Uri url, string outputPath, TimeSpan? timeout = null)
        {
            timeout ??= Timeout.InfiniteTimeSpan;
            var cts = new CancellationTokenSource(timeout.Value);
            await AnalyzeUrlHelperAsync(url, outputPath, cts.Token);
        }

        private string CommonFlags =>
            string.Join(" ", new string[] { DeviceFlag, QuietFlag, ThrottleFlag, OutputFlag, CategoriesFlag });

        private async Task AnalyzeUrlHelperAsync(Uri url, string outputPath, CancellationToken token = default)
        {
            var info = new ProcessStartInfo(_lighthousePath, $"{url} {CommonFlags} {OutputPathFlag(outputPath)}")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };

            using var p = Process.Start(info);
            if (p is not null)
            {
                await p.WaitForExitAsync(token);
            }
        }

        public async Task AnalyzeUrlsAsync(
            IEnumerable<Uri> urls,
            Func<Uri, string> urlToFilePath,
            CancellationToken? token = null,
            IProgress<TaskProgress>? progress = null,
            TimeSpan? timeout = null)
        {
            timeout ??= Timeout.InfiniteTimeSpan;
            var timeoutTokenSource = new CancellationTokenSource(timeout.Value);
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, token ?? default);

            var urlList = urls.ToArray();
            for (var i = 0; i < urlList.Length; i++)
            {
                var url = urlList[i];
                progress?.Report(new(i + 1, urlList.Length, url.AbsoluteUri));
                var filename = urlToFilePath(url);
                await AnalyzeUrlHelperAsync(url, filename, combinedCts.Token);
            }
        }
    }
}