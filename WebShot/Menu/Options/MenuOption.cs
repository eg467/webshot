using WebShot.Menu.ColoredConsole;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebShot.Menu.Options
{
    public interface IMenuOption<TInput> : ICompletionHandler
    {
        /// <summary>
        /// Executes the option if it's applicable.
        /// </summary>
        /// <param name="input">The raw input from the menu.</param>
        /// <returns>True if the option handled the input.</returns>
        Task<bool> Execute(TInput input);

        /// <summary>
        /// Creates a user prompt to notify the user about the option and how to select it.
        /// </summary>
        IOutput? Prompt();
    }

    public delegate Task CompletionHandler(MenuNavigator navigator);

    public interface ICompletionHandler
    {
        CompletionHandler CompletionHandler { get; set; }
    }

    /// <summary>
    /// Provides client a means to change the next menu displayed.
    /// </summary>
    public class OptionActionArgs
    {
        public ICompletionHandler OnCompletion { get; }

        public OptionActionArgs(ICompletionHandler completionHandler)
        {
            OnCompletion = completionHandler;
        }
    }
}