using System;
using System.Collections.Generic;
using System.Linq;
using WebShot.Menu.ColoredConsole;

namespace WebShot.Menu.Menus
{
    public class SelectionMenuInputter<TItem>
    {
        protected readonly List<TItem> Items;
        public IReadOnlyList<TItem> Options => Items;
        protected readonly MenuOutput _output;
        public readonly Func<TItem, string> _labeler;
        public int SelectedIndex { get; protected set; }

        public event EventHandler<KeyPressedEventArgs<TItem>>? KeyPressed;

        public bool CanCancel { get; set; } = true;
        public int ColumnCount { get; set; } = 1;

        public const string DefaultNullIdentifier = "<NULL>";

        /// <summary>
        /// Creates a user-navigable menu.
        /// </summary>
        /// <param name="items">The options available for selection.</param>
        /// <param name="header">The header/instructions for the menu, since the console will be cleared entirely.</param>
        /// <param name="labeler">A function that converts a list element to a string label, null to use <see cref="TItem.ToString()"/></param>.
        public SelectionMenuInputter(
            IEnumerable<TItem> items,
            MenuOutput output,
            Func<TItem, string>? labeler = null)
        {
            Items = items.ToList();
            _output = output;
            _labeler = x => SafeLabeler(x, labeler);
        }

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

        public ConsoleColor HoverColor { get; set; } = ConsoleColor.Cyan;
        public ConsoleColor NormalColor { get; set; } = ConsoleColor.White;

        public ListWithSelection<TItem> ChooseOption()
        {
            // Adapted From: https://stackoverflow.com/questions/46908148/controlling-menu-with-the-arrow-keys-and-enter

            if (!Items.Any())
            {
                ColoredOutput.WriteLines(_output.Header, "No items are available to select...");
                DefaultMenuLines.PressKeyToContinue();
                return new(Items, -1);
            }

            var startX = 4;
            int startY;
            const int spacingPerLine = 14;
            ConsoleKey key;
            Console.CursorVisible = false;
            var cancelled = false;
            SelectedIndex = 0;
            do
            {
                // Output
                Console.Clear();
                Console.SetCursorPosition(0, 0);
                ColoredOutput.WriteLines(_output.Header);
                _output.Description.WriteLine();

                (_, startY) = Console.GetCursorPosition();
                startY += 2;
                Console.SetCursorPosition(startX, startY);

                for (int i = 0; i < Items.Count; i++)
                {
                    Console.SetCursorPosition(startX + (i % ColumnCount) * spacingPerLine, startY + i / ColumnCount);
                    PrintedLabel(i).Write();
                }

                Console.SetCursorPosition(0, Console.CursorTop + 2);

                // Input

                key = Console.ReadKey(true).Key;

                KeyPressedEventArgs<TItem> args = new(Items[SelectedIndex], key);
                OnKeyPress(args);

                if (args.WasSubmitted)
                    break;

                if (this.CanCancel && (key == ConsoleKey.Escape || args.WasCancelled))
                {
                    cancelled = true;
                    break;
                }

                HandleKey(key);
            } while (key != ConsoleKey.Enter);

            Console.SetCursorPosition(0, startY + Items.Count / ColumnCount + 2);
            Console.CursorVisible = true;

            return new(Items, !cancelled ? SelectedIndex : -1);
        }

        protected void OnKeyPress(KeyPressedEventArgs<TItem> args)
        {
            KeyPressed?.Invoke(this, args);
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
                    if (SelectedIndex % ColumnCount > 0)
                        SelectedIndex--;
                    break;

                case ConsoleKey.RightArrow:
                    if (SelectedIndex % ColumnCount < ColumnCount - 1)
                        SelectedIndex++;
                    break;

                case ConsoleKey.UpArrow:
                    if (SelectedIndex >= ColumnCount)
                        SelectedIndex -= ColumnCount;
                    break;

                case ConsoleKey.DownArrow:
                    var newIndex = Math.Min(Items.Count - 1, SelectedIndex + ColumnCount);
                    if (newIndex / ColumnCount > SelectedIndex / ColumnCount)
                        SelectedIndex = newIndex;
                    break;

                default:
                    return false;
            }
            return true;
        }
    }
}