using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;

namespace WebShot.Menu.Options
{
    public abstract class Option<TInput> : IMenuOption<TInput>
    {
        public CompletionHandler CompletionHandler { get; set; }

        public Option(CompletionHandler? completionHandler = null)
        {
            CompletionHandler = completionHandler ?? CompletionHandlers.Back;
        }

        public abstract IOutput? Prompt { get; }

        protected abstract bool IsMatch(TInput input);

        protected abstract Task Handler(TInput input);

        public virtual async Task<bool> Execute(TInput input)
        {
            if (!IsMatch(input))
                return false;
            await Handler(input);
            return true;
        }
    }
}