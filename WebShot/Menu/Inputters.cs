using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;
using WebShot.Menu.Menus;

namespace WebShot.Menu
{
    public delegate TInput Inputter<TInput>();

    public static class Inputters
    {
        public static Inputter<TInput> FromValue<TInput>(TInput value) =>
            () => value;

        public static Inputter<string> ConsolePrompt(IOutput? prompt, StringValidator? validator = null, bool trim = true)
        {
            return () =>
            {
                validator ??= new();
                string input = "";
                while (true)
                {
                    prompt?.WriteLine();
                    input = Console.ReadLine() ?? "";
                    if (trim)
                        input = input.Trim();

                    try
                    {
                        validator.EnsureValid(input);
                        return input;
                    }
                    catch (Exception ex)
                    {
                        ColoredOutput.Error(ex.Message);
                    }
                }
            };
        }
    }
}