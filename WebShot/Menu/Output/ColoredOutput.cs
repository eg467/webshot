using NLog.LayoutRenderers;
using NLog.LayoutRenderers.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using WebshotService;

namespace WebShot.Menu.ColoredConsole
{
    public interface IOutput
    {
        void Write();

        string Text { get; }

        int MaxLineLength { get; }

        void WriteLine();
    }

    public class ColoredOutput : IOutput
    {
        public string Text { get; set; }
        public int MaxLineLength => Text.Length;

        public override string ToString() => Text;

        public int Length => Text.Length;

        public ConsoleColor? Foreground { get; set; }
        public ConsoleColor? Background { get; set; }

        public ColoredOutput(string text, ConsoleColor? foreground = null, ConsoleColor? background = null)
        {
            Text = text;
            Foreground = foreground;
            Background = background;
        }

        public ColoredOutput Clone() =>
            new ColoredOutput(Text, Foreground, Background);

        public static implicit operator ColoredOutput(string line) => new(line);

        public static MixedOutput operator +(ColoredOutput a, ColoredOutput b) => new MixedOutput(a, b);

        public static MixedOutput operator -(MixedOutput a, ColoredOutput b)
        {
            a.Remove(b);
            return a;
        }

        public static List<ColoredOutput> ToList(ConsoleColor? foreground, ConsoleColor? background, params string[] text) =>
            text.Select(t => new ColoredOutput(t, foreground, background)).ToList();

        public static void WriteLines(params ColoredOutput[] messages)
        {
            messages.ForEach(m => m.Write());
            Console.Write(Environment.NewLine);
        }

        public static ColoredOutput Empty => new ColoredOutput("");

        public static ColoredOutput Error(string message) =>
            new ColoredOutput(message, ConsoleColor.White, ConsoleColor.DarkRed);

        public static ColoredOutput Warn(string message) =>
            new ColoredOutput(message, ConsoleColor.DarkMagenta, ConsoleColor.White);

        public void PrintHeader(
            int vBorder = 1,
            int hBorder = 8,
            int vPadding = 1,
            int hPadding = 3,
            char borderChar = '*')
        {
            var headerLength = 2 * hBorder + 2 * hPadding + Text.Length;
            ColoredOutput fullHeaderBar = new string(borderChar, headerLength);
            ColoredOutput padding = new string(' ', hPadding);
            ColoredOutput sideBorder = new string(borderChar, hBorder);
            ColoredOutput vPaddingLine = $"{sideBorder}{padding}{new string(' ', Text.Length)}{padding}{sideBorder}";

            PrintNLines("");
            PrintNLines(fullHeaderBar, vBorder);
            PrintNLines(vPaddingLine, vPadding);
            PrintNLines($"{sideBorder}{padding}{Text}{padding}{sideBorder}");
            PrintNLines(vPaddingLine, vPadding);
            PrintNLines(fullHeaderBar, vBorder);

            void PrintNLines(ColoredOutput line, int count = 1)
            {
                line.Foreground = Foreground;
                line.Background = Background;
                Enumerable
                    .Repeat(line, count)
                    .ForEach(c => c.WriteLine());
            }
        }

        public static Func<string, ColoredOutput> ColoredFactory(ConsoleColor color) =>
            (string s) => new ColoredOutput(s, color);

        private void ApplyColor()
        {
            Console.ForegroundColor = Foreground ?? Console.ForegroundColor;
            Console.BackgroundColor = Background ?? Console.BackgroundColor;
        }

        private static void ClearColor()
        {
            Console.ResetColor();
        }

        public ColoredOutput FormatLines(string formatString) =>
            new ColoredOutput(string.Format(formatString, Text), Foreground, Background);

        public void Write()
        {
            ApplyColor();
            Console.Write(Text);
            ClearColor();
        }

        public void WriteLine()
        {
            ApplyColor();
            Console.WriteLine(Text);
            ClearColor();
        }
    }
}