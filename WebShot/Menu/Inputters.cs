using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;

namespace WebShot.Menu
{
    public delegate TInput Inputter<TInput>();

    public static class Inputters
    {
        public static Inputter<TInput> FromValue<TInput>(TInput value) =>
            () => value;

        public static Inputter<string> ConsolePrompt(IOutput? prompt)
        {
            return () =>
            {
                prompt?.WriteLine();
                return Console.ReadLine()?.Trim()
                    ?? throw new InvalidOperationException("No input detected.");
            };
        }
    }
}