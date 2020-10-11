using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebShot.Menu
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
    }
}