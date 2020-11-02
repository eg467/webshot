using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;
using WebShot.Menu.Menus;

namespace WebShot.Menu.Options
{
    /// <param name="Prompt">The prompt that informs the user of the option's format and purpose.</param>
    /// <param name="CompletionHandler">
    ///     The action to execute after this option's handler has been executed (e.g. navigate to a submenu).
    ///     Defaults to go back in the <see cref="MenuNavigator"/>'s structure.
    /// </param>
    /// <param name="AsyncHandler">The action that should be executed if the input matches the option.</param>
    /// <param name="Matcher">
    ///     Determines if the input is applicable to this option.
    ///     Defaults to match <see cref="OptionPrompt.CombinedOutput"/> of <paramref name="prompt"/>.</param>
    /// <param name="InputValidator">
    ///     Validates raw user input.
    ///     Defaults to match everthing.</param>
    public class InputMenuOptionOptions
    {
        public StringValidator InputValidator { get; set; } = StringValidator.MatchAll;
        public OptionPrompt Prompt { get; set; }
        public RegexOptionMatcher Matcher { get; set; }
        public AsyncOptionHandler<Match> SelectionHandler { get; set; } = (_, _2) => Task.CompletedTask;
        public CompletionHandler CompletionHandler { get; set; }

        public InputMenuOptionOptions(OptionPrompt prompt, CompletionHandler completionHandler)
        {
            Prompt = prompt;
            CompletionHandler = completionHandler;
            Matcher = prompt.Descriptor.Text;
        }
    }

    /// <summary>
    /// Creates a console menu option that the user can select to perform a task.
    /// </summary>
    public class InputMenuOption : Option<string>
    {
        //private readonly StringValidator _inputValidator;
        //private readonly OptionPrompt _prompt;
        //private readonly RegexOptionMatcher _matcher;
        //private readonly AsyncOptionHandler<Match> _handler;

        private readonly InputMenuOptionOptions _options;

        public InputMenuOption(InputMenuOptionOptions options) : base(options.CompletionHandler)
        {
            _options = options;
        }

        //public InputMenuOption(
        //    OptionPrompt prompt,
        //    OptionHandler<Match> handler,
        //    RegexOptionMatcher? matcher = null,
        //    StringValidator? inputValidator = null,
        //    CompletionHandler? completionHandler = null)
        //    : this(prompt, handler.ToAsync(), matcher, inputValidator, completionHandler)
        //{
        //}

        public override IOutput? Prompt => _options.Prompt;

        protected override Task Handler(string input)
        {
            _options.InputValidator.EnsureValid(input);
            Match? match = _options.Matcher.GetMatch(input);
            if (match is null)
                throw new InvalidOperationException("You cannot execute the handler of a non-matching option.");
            return _options.SelectionHandler(match, this);
        }

        protected override bool IsMatch(string input) => _options.Matcher.IsMatch(input);
    }
}