using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace WebshotService.Entities
{
    /// <summary>
    /// See: https://developer.mozilla.org/en-US/docs/Web/API/PerformanceNavigationTiming
    /// </summary>
    public record NavigationTiming
    {
        public int DecodedBodySize { get; init; }
        public int FetchStart { get; init; }
        public int LoadEventEnd { get; init; }
        public int LoadEventStart { get; init; }
        public int RedirectEnd { get; init; }
        public int RedirectStart { get; init; }
        public int RequestStart { get; init; }
        public int ResponseEnd { get; init; }
        public int ResponseStart { get; init; }
        public int SecureConnectionStart { get; init; }
        public int StartTime { get; init; }
        public int ConnectEnd { get; init; }
        public int ConnectStart { get; init; }
        public int DomComplete { get; init; }
        public int DomContentLoadedEventEnd { get; init; }
        public int DomContentLoadedEventStart { get; init; }
        public int DomInteractive { get; init; }
        public int DomainLookupEnd { get; init; }
        public int DomainLookupStart { get; init; }
        public int Duration { get; init; }
        public int TransferSize { get; init; }

        private static TimeSpan Ts(int ms) => TimeSpan.FromMilliseconds(ms);

        public int FromFetch(int time) => time - FetchStart;

        public TimeSpan Ttfb => Ts(FromFetch(ResponseStart));

        // See https://www.w3.org/TR/2018/WD-navigation-timing-2-20181130/timestamp-diagram.svg

        [JsonIgnore]
        public TimeSpan Redirect => Ts(RedirectEnd - RedirectStart);

        [JsonIgnore]
        public TimeSpan AppCache => Ts(DomainLookupStart - FetchStart);

        [JsonIgnore]
        public TimeSpan Dns => Ts(DomainLookupEnd - DomainLookupStart);

        [JsonIgnore]
        public TimeSpan Tcp => Ts(ConnectEnd - ConnectStart);

        [JsonIgnore]
        public TimeSpan Request => Ts(ResponseStart - RequestStart);

        [JsonIgnore]
        public TimeSpan Response => Ts(DomInteractive - ResponseStart);

        [JsonIgnore]
        public TimeSpan Processing => Ts(DomComplete - DomInteractive);

        [JsonIgnore]
        public TimeSpan Load => Ts(LoadEventEnd - LoadEventStart);

        [JsonIgnore]
        public TimeSpan BackendResource => Ts(ResponseEnd - RedirectStart);

        [JsonIgnore]
        public TimeSpan FrontendProcessing => Ts(LoadEventEnd - DomInteractive);

        private class JsonIgnoreAttributeIgnorerContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                property.Ignored = false;
                return property;
            }
        }

        private class OrderedContractResolver : JsonIgnoreAttributeIgnorerContractResolver
        {
            protected override IList<Newtonsoft.Json.Serialization.JsonProperty> CreateProperties(System.Type type, Newtonsoft.Json.MemberSerialization memberSerialization)
            {
                var @base = base.CreateProperties(type, memberSerialization);
                var ordered = @base
                    .OrderBy(p => p.Order ?? int.MaxValue)
                    .ThenBy(p => p.PropertyName)
                    .ToList();
                return ordered;
            }
        }

        public string SerializeAll() =>
            JsonConvert.SerializeObject(
                this,
                Formatting.Indented,
                new JsonSerializerSettings()
                {
                    ContractResolver = new OrderedContractResolver()
                });

        public class TimingStats
        {
            public double Min { get; set; }
            public double Max { get; set; }
            public double Avg { get; set; }
            public double Median { get; set; }
            public int Count { get; set; }
        }

        public static Dictionary<string, TimingStats> Stats(IEnumerable<NavigationTiming> timings, bool includeZero = false)
        {
            int[] CountableElements(Func<NavigationTiming, int> selector)
            {
                var all = timings.Select(selector);
                var selectedEls = includeZero ? all : all.Where(x => x != 0);
                var arr = selectedEls.ToArray();
                Array.Sort(arr);
                return arr;
            }

            static double StatOrValue(int[] values, Func<int[], double> operation, double valueIfEmpty)
            {
                return (values.Any()) ? operation(values) : valueIfEmpty;
            }

            TimingStats Stats(int[] values, Func<NavigationTiming, int> selector)
            {
                static double Median(int[] a) =>
                    (a.Length % 2 == 0)
                        ? (a[a.Length / 2] + a[a.Length / 2 + 1]) / 2.0
                        : (a[a.Length / 2]);

                return new TimingStats()
                {
                    Min = StatOrValue(values, a => a.First(), 0.0),
                    Max = StatOrValue(values, a => a.Last(), 0.0),
                    Avg = StatOrValue(values, a => a.Average(), 0.0),
                    Median = StatOrValue(values, Median, 0.0),
                    Count = values.Length
                };
            }

            var props = typeof(NavigationTiming)
                .GetProperties()
                .Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(TimeSpan))
                .ToList();

            Dictionary<string, TimingStats> stats = new();

            foreach (var p in props)
            {
                if (p.PropertyType != typeof(TimeSpan) && p.PropertyType != typeof(int))
                    continue;

                int GetValue(NavigationTiming t)
                {
                    if (p.PropertyType == typeof(TimeSpan))
                        return (int)((TimeSpan)p.GetValue(t)!).TotalMilliseconds;
                    else if (p.PropertyType == typeof(int))
                        return (int)p.GetValue(t)!;
                    else
                        return 0;
                }

                int[] values = CountableElements(GetValue);
                stats[p.Name] = Stats(values, GetValue);
            }
            return stats;
        }
    }
}