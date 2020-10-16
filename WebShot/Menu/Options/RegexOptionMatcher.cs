using System;
using WebShot.Menu.ColoredConsole;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

namespace WebShot.Menu.Options
{
    public class RegexOptionMatcher
    {
        private readonly Regex _regex;

        public bool IsMatch(string input) => _regex.IsMatch(input);

        public Match? GetMatch(string input) => _regex.Match(input);

        public RegexOptionMatcher(string pattern, bool matchCase = false, bool matchFullInput = true)
        {
            if (matchFullInput)
                pattern = GetFullMatch(pattern);
            var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            var timeout = TimeSpan.FromSeconds(10);
            _regex = new Regex(pattern, options, timeout);
        }

        public static implicit operator RegexOptionMatcher(string pattern) =>
            new RegexOptionMatcher(pattern);

        private static string GetFullMatch(string pattern)
        {
            if (!pattern.StartsWith('^'))
                pattern = "^" + pattern;

            // if the pattern doesn't end in a non-escaped '$'...
            if (!pattern.EndsWith("$") || pattern.EndsWith(@"\$"))
                pattern += "$";

            return pattern;
        }
    }
}