using System;
using WebShot.Menu.ColoredConsole;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebShot.Menu.Menus;

namespace WebShot.Menu.Options
{
    public class ConsoleMenuOption : IMenuOption<string>
    {
        /// <summary>
        /// An action to be executed after the option is done processing.
        /// </summary>
        public CompletionHandler CompletionHandler { get; set; }

        /// <summary>
        /// A regex pattern to match this option to input.
        /// </summary>
        protected readonly string? _matchingPattern;

        /// <summary>
        /// A description of the what the user should enter to match this option.
        /// </summary>
        public ColoredOutput Descriptor { get; } =
            new ColoredOutput("", ConsoleColor.Cyan);

        /// <summary>
        /// An explanation of what this option does when selected.
        /// </summary>
        public ColoredOutput Explanation { get; } =
            new ColoredOutput("", ConsoleColor.Gray);

        /// <summary>
        /// True if the pattern matching should be case sensitive.
        /// </summary>
        protected bool _caseSensitivePattern;

        /// <summary>
        /// True if <see cref="_matchingPattern"/> should match the full input, false to only match on a portion of the string.
        /// </summary>
        protected bool _matchFullInput = true;

        private readonly Func<Match, ICompletionHandler, Task>? _handler;

        /// <summary>Constructor</summary>
        /// <param name="descriptor">A user-friendly description of how to select this option.</param>
        /// <param name="explanation">A user-friendly explanation of what the option does.</param>
        /// <param name="matchingPattern">The regex pattern to match. Uses <see cref="descriptor"/> if null.</param>
        /// <param name="handler">What to do when the option is triggered.</param>
        /// <param name="completionHandler">What to do, usually menu navigation, after the option has triggered.</param>
        /// <param name="caseSensitivePattern">Whether <see cref="_matchingPattern"/> must match the case of the input.</param>
        public ConsoleMenuOption(
            string descriptor,
            string explanation,
            string? matchingPattern = null,
            Func<Match, ICompletionHandler, Task>? handler = null,
            CompletionHandler? completionHandler = null,
            bool caseSensitivePattern = false)
        {
            _matchingPattern = matchingPattern ?? descriptor;
            Descriptor.Text = descriptor;
            Explanation.Text = explanation;
            _handler = handler;
            _caseSensitivePattern = caseSensitivePattern;
            CompletionHandler = completionHandler ?? OptionCompletionHandlers.Back;
        }

        /// <summary>Constructor</summary>
        /// <param name="descriptor">A user-friendly description of how to select this option.</param>
        /// <param name="explanation">A user-friendly explanation of what the option does.</param>
        /// <param name="completionHandler">What to do, usually menu navigation, after the option has triggered.</param>
        public ConsoleMenuOption(
            string descriptor,
            string explanation,
            CompletionHandler? completionHandler = null)
        : this(descriptor, explanation, descriptor, null, completionHandler, false)
        {
        }

        private RegexOptions RegexOptions =>
            _caseSensitivePattern ? RegexOptions.None : RegexOptions.IgnoreCase;

        private string FullTextMatchPattern(string originalPattern)
        {
            if (!_matchFullInput)
                return originalPattern;

            if (!originalPattern.StartsWith("^"))
                originalPattern = "^" + originalPattern;
            if (!originalPattern.EndsWith("$"))
                originalPattern += "$";

            return originalPattern;
        }

        /// <summary>
        /// Displays the descriptor and explanation so user knows this option is available.
        /// </summary>
        public IOutput Prompt() =>
            new MixedOutput(Descriptor, Explanation.FormatLines(") {0}"));

        /// <summary>
        /// Executes this option if matches
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<bool> Execute(string input)
        {
            var m = GetMatch(input);
            if (m?.Success != true) return false;
            if (_handler is object)
                await _handler(m, this);
            return true;
        }

        private Match? GetMatch(string input) =>
            input is string && _matchingPattern is string
                ? Regex.Match(
                    input,
                    FullTextMatchPattern(_matchingPattern),
                    RegexOptions)
                : null;
    }
}