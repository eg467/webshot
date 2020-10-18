using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Immutable;

namespace WebshotService
{
    public static class Utils
    {
        public static string FormattedSerialize(object obj) =>
            JsonConvert.SerializeObject(obj, Formatting.Indented);

        public static string SanitizeFilename(string filename)
        {
            var sanitized = Regex.Replace(filename, "[^-a-zA-Z0-9]+", "_");
            sanitized = Regex.Replace(sanitized, "__+", "_");
            return sanitized;
        }

        public static string CreateTimestampDirectory(
            string parentDir,
            DateTime? creationTimestamp = null)
        {
            Directory.CreateDirectory(parentDir);
            string timestamp = (creationTimestamp ?? DateTime.Now).Timestamp();
            var origPath = Path.Combine(parentDir, timestamp);
            string uniquePath = MakeDirectoryNameUnique(origPath);
            Directory.CreateDirectory(uniquePath);
            return uniquePath;
        }

        public static string MakeDirectoryNameUnique(string dir)
        {
            string uniquePath = dir;
            var i = 1;
            while (Directory.Exists(uniquePath))
            {
                uniquePath = Path.Combine(dir, $"-[{i++}]");
            }
            return uniquePath;
        }

        public static int InRange(int value, int min, int max) =>
            Math.Min(max, Math.Max(min, value));

        public static int CombineHashCodes(int h1, int h2) => (((h1 << 5) + h1) ^ h2);

        public static Action Debounce(Action action, int delay)
        {
            System.Threading.Timer? timer = null;
            void callback(object? o)
            {
                action();
                timer?.Dispose();
                timer = null;
            }

            System.Threading.Timer CreateTimer()
            {
                return new System.Threading.Timer(
                    callback,
                    null,
                    delay,
                    System.Threading.Timeout.Infinite);
            }

            return () =>
            {
                timer?.Dispose();
                timer = CreateTimer();
            };
        }
    }

    public static class Extensions
    {
        public static T FindOrAdd<T>(this List<T> items, Predicate<T> match, Func<T> create)
        {
            int io = items.FindIndex(match);
            if (io >= 0)
                return items[io];

            var newItem = create();
            items.Add(newItem);
            return newItem;
        }

        public static TValue FindOrAdd<TKey, TValue>(
            this Dictionary<TKey, TValue> items,
            TKey key,
            Func<TKey, TValue> create)
        where TKey : notnull
        {
            if (items.TryGetValue(key, out TValue value))
                return value;

            var item = create(key);
            items[key] = item;
            return item;
        }

        public static int Find<T>(this IImmutableList<T> items, Func<T, bool> predicate)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (predicate(items[i])) return i;
            }
            return -1;
        }

        /// <summary>
        /// Transforms all elements in a list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="predicate"></param>
        /// <param name="replacement"></param>
        /// <returns>The number of replacements made.</returns>
        public static int ConditionalMap<T>(this IList<T> items, Func<T, bool> predicate, Func<T, int, T> replacement)
        {
            var replacementCount = 0;
            for (var i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (!predicate(item)) continue;
                items[i] = replacement(item, i);
                replacementCount++;
            }
            return replacementCount;
        }

        /// <summary>
        /// Transforms all elements in a list.
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="items"></param>
        /// <param name="predicate"></param>
        /// <param name="replacement"></param>
        /// <returns>The number of replacements made.</returns>
        //public static TList ImmutableConditionalMap<TList, TItem>(this TList items, Func<TItem, bool> predicate, Func<TItem, int, TItem> replacement) where TList : IImmutableList<TItem>
        //{
        //    for (var i = 0; i < items.Count; i++)
        //    {
        //        TItem item = items[i];
        //        if (!predicate(item)) continue;
        //        items = (TList)items.SetItem(i, replacement(item, i));
        //    }
        //    return items;
        //}

        /// <summary>
        /// Transforms all elements in a list.
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="items"></param>
        /// <param name="predicate"></param>
        /// <param name="replacement"></param>
        /// <returns>The number of replacements made.</returns>
        public static ImmutableArray<TItem> ImmutableConditionalMap<TItem>(this ImmutableArray<TItem> items, Func<TItem, bool> predicate, Func<TItem, TItem> replacement)
        {
            for (var i = 0; i < items.Length; i++)
            {
                TItem item = items[i];
                if (!predicate(item)) continue;
                items = items.SetItem(i, replacement(item));
            }
            return items;
        }

        /// <summary>
        /// Returns or creates a new collection as the value for a dictionary's key.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TVal"></typeparam>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static TVal GetOrInitValue<TKey, TVal>(this Dictionary<TKey, TVal> dict, TKey key) where TVal : new() where TKey : notnull
        {
            return dict.GetOrInitValue(key, () => new TVal());
        }

        /// <summary>
        /// Returns or creates a new collection as the value for a dictionary's key.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TVal"></typeparam>
        ///
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <param name="initializer">A function that creates the object</param>
        /// <returns></returns>
        public static TVal GetOrInitValue<TKey, TVal>(this Dictionary<TKey, TVal> dict, TKey key, Func<TVal> initializer) where TKey : notnull
        {
            if (!dict.TryGetValue(key, out var existingCol))
            {
                existingCol = initializer();
                dict[key] = existingCol;
            }
            return existingCol;
        }

        public static void ForEach<T>(this IEnumerable<T> items, Action<T> fn)
        {
            items.ForEach((x, _) => fn(x));
        }

        public static void ForEach<T>(this IEnumerable<T> items, Action<T, int> fn)
        {
            int i = 0;
            foreach (var item in items)
            {
                fn(item, i++);
            }
        }

        public static bool Unanimous<T>(this IEnumerable<T> items)
        {
            if (!items.Any()) return true;
            var comparer = EqualityComparer<T>.Default;
            var first = items.First();
            bool MatchesFirst(T item) => comparer.Equals(first, item);
            return items.Skip(1).All(MatchesFirst);
        }

        /// <summary>
        /// Returns a new Uri with minor differences (e.g. fragment) removed or null.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static Uri Standardize(this Uri uri)
        {
            // Removes fragment/anchor
            // Adds slashes to path if option is enabled and the url isn't a filename with extension
            string lastSegment = uri.Segments.Last();

            // Add a trailing slash to non-filename path components
            // e.g. http://example.com/test -> http://example.com/test/
            bool appendTrailingSlash = true;
            bool addSlash = appendTrailingSlash
                && !lastSegment.Contains(".")
                && !lastSegment.EndsWith("/");

            string trailingSlash = addSlash ? "/" : "";
            return new Uri($"{uri.Scheme}{Uri.SchemeDelimiter}{uri.Authority}{uri.AbsolutePath}{trailingSlash}{uri.Query}");
        }

        /// <summary>
        /// Standardizes URI if possible or returns the original uri on failure.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static Uri TryStandardize(this Uri uri)
        {
            try
            {
                return uri.Scheme.StartsWith(Uri.UriSchemeHttp)
                    ? uri.Standardize()
                    : uri;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing URI {uri}: {ex.Message}");
                return uri;
            }
        }

        public static string Timestamp(this DateTime dt) =>
            dt.ToString("yyyy-dd-M_HH-mm-ss-fffffff");

        public static T JsonClone<T>(this T obj)
        {
            var serialized = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(serialized);
        }
    }

    /// <summary>
    /// Encryption methods derived from: https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata?redirectedfrom=MSDN&view=dotnet-plat-ext-3.1
    /// </summary>
    public static class Encryption
    {
        private static readonly byte[] s_additionalEntropy = { 7, 2, 56, 72, 2, 2, 23, 3, 3, 4, 1, 67 };

        /// <summary>
        /// Encrypts plain-text string to a base64-encoded, encrypted string.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string Protect(string data)
        {
            var encoding = new System.Text.UTF8Encoding();
            byte[] input = encoding.GetBytes(data);
            var encrypted = Protect(input);
            return Convert.ToBase64String(encrypted); ;
        }

        /// <summary>
        /// Decrypts a base64-encoded, encrypted string.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string Unprotect(string data)
        {
            byte[] input = Convert.FromBase64String(data);
            var decrypted = Unprotect(input);
            var encoding = new System.Text.UTF8Encoding();
            return encoding.GetString(decrypted);
        }

        /// <summary>
        /// Encrypt the data using DataProtectionScope.CurrentUser. The result can be decrypted only by the same current user.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] Protect(byte[] data)
        {
            return ProtectedData.Protect(data, s_additionalEntropy, DataProtectionScope.CurrentUser);
        }

        /// <summary>
        /// Decrypt the data using DataProtectionScope.CurrentUser.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] Unprotect(byte[] data)
        {
            return ProtectedData.Unprotect(data, s_additionalEntropy, DataProtectionScope.CurrentUser);
        }
    }
}