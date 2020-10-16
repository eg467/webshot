using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;
using WebShot.Menu.Menus;

namespace WebShot.Menu.Options
{
    /// <summary>
    /// Creates a console menu option that the user can select to perform a task.
    /// </summary>
    public class ConsoleOption : Option<string>
    {
        private readonly StringValidator _inputValidator;
        private readonly OptionPrompt _prompt;
        private readonly RegexOptionMatcher _matcher;
        private readonly AsyncOptionHandler<Match> _handler;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="prompt">The prompt that informs the user of the option's format and purpose.</param>
        /// <param name="asyncHandler">The action that should be executed if the input matches the option.</param>
        /// <param name="matcher">
        ///     Determines if the input is applicable to this option.
        ///     Defaults to match <see cref="OptionPrompt.CombinedOutput"/> of <paramref name="prompt"/>.</param>
        /// <param name="inputValidator">
        ///     Validates raw user input.
        ///     Defaults to match everthing.</param>
        /// <param name="completionHandler">
        ///     The action to execute after this option's handler has been executed (e.g. navigate to a submenu).
        ///     Defaults to go back in the <see cref="MenuNavigator"/>'s structure.
        /// </param>
        public ConsoleOption(
            OptionPrompt prompt,
            AsyncOptionHandler<Match>? asyncHandler = null,
            RegexOptionMatcher? matcher = null,
            StringValidator? inputValidator = null,
            CompletionHandler? completionHandler = null)
            : base(completionHandler)
        {
            _prompt = prompt;
            _matcher = matcher ?? prompt.Descriptor.Text;
            _handler = asyncHandler ?? ((_, _2) => Task.CompletedTask);
            _inputValidator = inputValidator ?? StringValidator.MatchAll;
        }

        public ConsoleOption(
            OptionPrompt prompt,
            OptionHandler<Match> handler,
            RegexOptionMatcher? matcher = null,
            StringValidator? inputValidator = null,
            CompletionHandler? completionHandler = null)
            : this(prompt, handler.ToAsync(), matcher, inputValidator, completionHandler)
        {
        }

        public override IOutput? Prompt => _prompt;

        protected override Task Handler(string input)
        {
            _inputValidator.EnsureValid(input);
            Match? match = _matcher.GetMatch(input);
            if (match is null)
                throw new InvalidOperationException("You cannot execute the handler of a non-matching option.");
            return _handler(match, this);
        }

        protected override bool IsMatch(string input) => _matcher.IsMatch(input);
    }
}