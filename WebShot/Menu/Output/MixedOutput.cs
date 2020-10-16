using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WebShot.Menu.ColoredConsole
{
    public class MixedOutput : IOutput, IEnumerable<IOutput>
    {
        private readonly List<IOutput> _items;

        private Action _writer;

        public string Text
        {
            get
            {
                string combiner = ContentDirection == ContentDirection.Vertical
                        ? Environment.NewLine
                        : "";
                return string.Join(combiner, _items.Select(i => i.Text));
            }
        }

        public override string ToString() => Text;

        public ContentDirection ContentDirection
        {
            get => _writer == HorizontalWrite
                ? ContentDirection.Horizontal
                : ContentDirection.Vertical;
            set => _writer = value == ContentDirection.Horizontal
                ? HorizontalWrite
                : VerticalWrite;
        }

        public int MaxLineLength => ContentDirection == ContentDirection.Vertical
            ? _items.Max(i => i.MaxLineLength) : Text.Length;

        public MixedOutput(ContentDirection direction, IEnumerable<IOutput> items)
        {
            // For compiler null warning
            _writer = HorizontalWrite;

            ContentDirection = direction;
            _items = items.ToList();
        }

        public MixedOutput() : this(ContentDirection.Horizontal, Enumerable.Empty<IOutput>())
        {
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

        public static MixedOutput BlankLines(int count) =>
            Vertical(Enumerable.Repeat((ColoredOutput)"", count));

        public static MixedOutput Vertical(IEnumerable<IOutput> items) =>
            new MixedOutput(ContentDirection.Vertical, items);

        public static MixedOutput Horizontal(IEnumerable<IOutput> items) =>
            new MixedOutput(ContentDirection.Horizontal, items);

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

        private void HorizontalWrite()
        {
            _items.ForEach(i => i.Write());
        }

        private void VerticalWrite()
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

        public IEnumerator<IOutput> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public enum ContentDirection
    {
        Horizontal, Vertical
    }
}