using NLog.LayoutRenderers;
using NLog.LayoutRenderers.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using WebshotService;

namespace WebShot.Menus.ColoredConsole
{
    public interface IOutput
    {
        void Write();

        void WriteLine();
    }

    public enum ContentDirection
    {
        Horizontal, Vertical
    }

    public class MixedOutput : IOutput
    {
        private readonly List<IOutput> _items;

        private Action _writer;

        public ContentDirection ContentDirection
        {
            get => _writer == HorizontalWrite ? ContentDirection.Horizontal : ContentDirection.Vertical;
            set => _writer = value == ContentDirection.Horizontal ? HorizontalWrite : VerticalWrite;
        }

        public MixedOutput()
        {
            _items = new();
            _writer = HorizontalWrite;
        }

        public MixedOutput(ContentDirection direction, IEnumerable<IOutput> items)
        {
            _items = items.ToList();
            _writer = HorizontalWrite;
        }

        public MixedOutput(IEnumerable<IOutput> items)
        : this(ContentDirection.Horizontal, items)
        {
        }

        public MixedOutput(params IOutput[] items) : this((IEnumerable<IOutput>)items)
        {
        }

        public MixedOutput(ContentDirection direction, params IOutput[] items) : this(direction, (IEnumerable<IOutput>)items)
        {
        }

        public MixedOutput(IEnumerable<(string Content, ConsoleColor Foreground)> coloredText)
            : this(coloredText.Select(ct => new ColoredOutput(ct.Content, ct.Foreground)))
        {
        }

        public MixedOutput(params (string Content, ConsoleColor Foreground)[] coloredText)
            : this((IEnumerable<(string, ConsoleColor)>)coloredText)
        {
        }

        public static MixedOutput operator +(MixedOutput a, MixedOutput b) =>
            new MixedOutput(a._items.Concat(b._items));

        public static implicit operator MixedOutput(List<IOutput> items) => new(items);

        public static implicit operator MixedOutput(IOutput[] items) => new(items);

        public void Add(IOutput item) => _items.Add(item);

        public bool Remove(IOutput item) => _items.Remove(item);

        public void Write()
        {
            if (_items.Any())
                _writer();
        }

        public void HorizontalWrite()
        {
            _items.ForEach(i => i.Write());
        }

        public void VerticalWrite()
        {
            for (int i = 0; i < _items.Count - 1; i++)
                _items[i].WriteLine();

            _items[^1].Write();
        }

        public void WriteLine()
        {
            Write();
            Console.Write(Environment.NewLine);
        }
    }

    public class ColoredOutput : IOutput
    {
        public string Text { get; set; }
        public int Length => Text.Length;

        public override string ToString() => Text;

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

        public static void WriteLine(params ColoredOutput[] messages)
        {
            messages.ForEach(m => m.Write());
            Console.Write(Environment.NewLine);
        }

        public static ColoredOutput Error(string message) =>
            new ColoredOutput(message, ConsoleColor.White, ConsoleColor.DarkRed);

        public static ColoredOutput Warn(string message) =>
            new ColoredOutput(message, ConsoleColor.DarkMagenta, ConsoleColor.White);

        public static Func<string, ColoredOutput> WithForeground(ConsoleColor color) =>
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

        public static MixedOutput Multiline(
                ConsoleColor? foreground,
                ConsoleColor? background,
                params string[] lines) => Multiline(foreground, background, (IEnumerable<string>)lines);

        public static MixedOutput Multiline(
                ConsoleColor? foreground,
                ConsoleColor? background,
                IEnumerable<string> lines) =>
            new MixedOutput(
                ContentDirection.Vertical,
                lines.Select(l => new ColoredOutput(l, foreground, background)));
    }
}