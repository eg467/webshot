using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebShot.Menus
{
    public delegate TInput Inputter<TInput>();

    public static class Inputters
    {
        public static Inputter<TInput> FromValue<TInput>(TInput value) =>
            () => value;

        public static Inputter<string> ConsolePrompt(string prompt)
        {
            return () =>
            {
                Console.WriteLine(prompt);
                string? result = null;
                while (result is null)
                    result = Console.ReadLine();
                return result;
            };
        }

        public static Inputter<ListWithSelection<T>> SelectionMenu<T>(IEnumerable<T> items)
        {
            var menu = new ListSelectionInput<T>(items);
            return menu.ChooseOption;
        }
    }
}