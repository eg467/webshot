using System;
using System.Collections.Generic;
using System.Linq;
using WebShot.Menus.ColoredConsole;

namespace WebShot.Menus
{
    public class ListSelectionInput<TItem>
    {
        protected readonly List<TItem> Items;

        public readonly Func<TItem, string> _labeler;

        /// <summary>
        /// Creates a user-navigable menu.
        /// </summary>
        /// <param name="items">The options available for selection.</param>
        /// <param name="labeler">A function that converts a list element to a string label, null to use <see cref="TItem.ToString()"/></param>.
        public ListSelectionInput(IEnumerable<TItem> items, Func<TItem, string>? labeler = null)
        {
            Items = items.ToList();
            _labeler = x => SafeLabeler(x, labeler);
        }

        public bool CanCancel { get; set; } = true;
        public int ColumnCount { get; set; } = 1;

        protected int SelectedIndex = -1;

        public const string DefaultNullIdentifier = "<NULL>";

        protected static string SafeLabeler<T>(T item, Func<T, string>? labeler) =>
            labeler?.Invoke(item) ?? item?.ToString() ?? DefaultNullIdentifier;

        protected string TextOf(int index) => _labeler(Items[index]);

        protected ConsoleColor ColorOf(int index) =>
            index == SelectedIndex ? HoverColor : NormalColor;

        /// <summary>
        /// Generates the label as it will be printed on the screen.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        protected virtual IOutput PrintedLabel(int index) =>
            new ColoredOutput(TextOf(index), ColorOf(index));

        public ConsoleColor HoverColor { get; set; } = ConsoleColor.Green;
        public ConsoleColor NormalColor { get; set; } = ConsoleColor.White;

        public ListWithSelection<TItem> ChooseOption()
        {
            // Adapted From: https://stackoverflow.com/questions/46908148/controlling-menu-with-the-arrow-keys-and-enter

            if (!Items.Any())
                return NotFound();

            const int startX = 15;
            const int startY = 8;
            const int spacingPerLine = 14;

            ConsoleKey key;

            Console.CursorVisible = false;

            SelectedIndex = 0;
            do
            {
                Console.Clear();

                for (int i = 0; i < Items.Count; i++)
                {
                    Console.SetCursorPosition(startX + (i % ColumnCount) * spacingPerLine, startY + i / ColumnCount);
                    PrintedLabel(i).Write();
                }

                key = Console.ReadKey(true).Key;

                if (CanCancel && key == ConsoleKey.Escape)
                    return NotFound();

                HandleKey(key);
            } while (key != ConsoleKey.Enter);

            Console.CursorVisible = true;
            Console.SetCursorPosition(0, startY + Items.Count / ColumnCount + 2);

            return new ListWithSelection<TItem>(Items, SelectedIndex);

            ListWithSelection<TItem> NotFound() => new(Items, -1);
        }

        /// <summary>
        /// Respond to keypress (Call before or after depending on whether the base handlers should be superceded).
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if the key was handled and processing should cease.</returns>
        protected virtual bool HandleKey(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    {
                        if (SelectedIndex % ColumnCount > 0)
                            SelectedIndex--;
                        break;
                    }
                case ConsoleKey.RightArrow:
                    {
                        if (SelectedIndex % ColumnCount < ColumnCount - 1)
                            SelectedIndex++;
                        break;
                    }
                case ConsoleKey.UpArrow:
                    {
                        if (SelectedIndex >= ColumnCount)
                            SelectedIndex -= ColumnCount;
                        break;
                    }
                case ConsoleKey.DownArrow:
                    {
                        if (SelectedIndex + ColumnCount < Items.Count)
                            SelectedIndex += ColumnCount;
                        break;
                    }
                default:
                    return false;
            }
            return true;
        }
    }

    public record ListWithSelection<T>
    {
        public T Item
        {
            get
            {
                if (!Exists)
                    throw new ArgumentOutOfRangeException(nameof(SelectedIndex));
                return Items[SelectedIndex];
            }
        }

        public bool Exists => SelectedIndex >= 0;

        public int SelectedIndex { get; init; }
        public IReadOnlyList<T> Items { get; init; }

        public ListWithSelection(IReadOnlyList<T> items, int selectedIndex)
        {
            SelectedIndex = selectedIndex;
            Items = items;
        }
    }
}