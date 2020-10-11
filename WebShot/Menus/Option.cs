using System;
using System.Threading.Tasks;
using WebShot.Menus.ColoredConsole;

namespace WebShot.Menus
{
    internal class Option<TInput> : IMenuOption<TInput>
    {
        private readonly IOutput? _prompt;
        private readonly Func<TInput, bool> _isMatch;
        private readonly Func<TInput, ICompletionHandler, Task> _handler;
        public CompletionHandler CompletionHandler { get; set; }

        public Option(
            IOutput? prompt,
            Func<TInput, bool> isMatch,
            Func<TInput, ICompletionHandler, Task> handler,
            CompletionHandler? completionHandler = null)
        {
            _prompt = prompt;
            _isMatch = isMatch;
            _handler = handler;
            CompletionHandler = completionHandler ?? OptionCompletionHandlers.Back;
        }

        public async Task<bool> Execute(TInput input)
        {
            if (!_isMatch(input))
                return false;
            await _handler(input, this);
            return true;
        }

        public IOutput? Prompt() => _prompt;
    }

    public class OptionPrompt : IOutput
    {
        public ColoredOutput Descriptor { get; set; } = new ColoredOutput("", ConsoleColor.Cyan);
        public ColoredOutput Explanation { get; set; } = new ColoredOutput("", ConsoleColor.Gray);

        public OptionPrompt(ColoredOutput descriptor, ColoredOutput explanation)
        {
            Descriptor = descriptor;
            Explanation = explanation;
        }

        public OptionPrompt(string descriptor, string explanation)
        {
            Descriptor.Text = descriptor;
            Explanation.Text = explanation;
        }

        private IOutput CombinedOutput
        {
            get
            {
                var hasDesc = Descriptor.Length > 0;
                var hasExp = Explanation.Length > 0;

                MixedOutput m = new MixedOutput();
                if (hasDesc)
                    m.Add(Descriptor!);

                if (hasExp)
                    m.Add(hasDesc ? Explanation!.FormatLines(") {0}") : Explanation!);

                return m;
            }
        }

        public void Write() => CombinedOutput.Write();

        public void WriteLine() => CombinedOutput.WriteLine();
    }
}