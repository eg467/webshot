using System;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;

namespace WebShot.Menu.Options
{
    public class CustomOption<TInput> : Option<TInput>
    {
        private readonly IOutput? _prompt;
        private readonly Predicate<TInput> _matcher;
        private readonly AsyncOptionHandler<TInput> _handler;

        public CustomOption(
            IOutput? prompt,
            Predicate<TInput> isMatch,
            AsyncOptionHandler<TInput> handler,
            CompletionHandler? completionHandler = null)
        {
            _prompt = prompt;
            _matcher = isMatch;
            _handler = handler;
            CompletionHandler = completionHandler ?? CompletionHandlers.Back;
        }

        public override IOutput? Prompt => _prompt;

        protected override Task Handler(TInput input) => _handler(input, this);

        protected override bool IsMatch(TInput input) => _matcher(input);
    }

    public delegate Task AsyncOptionHandler<TInput>(TInput input, ICompletionHandler completionHandler);

    public delegate void OptionHandler<TInput>(TInput input, ICompletionHandler completionHandler);

    public static class OptionHandlerExtensions
    {
        public static AsyncOptionHandler<TInput> ToAsync<TInput>(this OptionHandler<TInput> handler) =>
            (input, completionHandler) => { handler(input, completionHandler); return Task.CompletedTask; };
    }
}