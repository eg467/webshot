using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;
using WebShot.Menu.Options;

namespace WebShot.Menu.Menus
{
    public partial class SelectionMenu<TItem> : Menu<ListWithSelection<TItem>>
    {
        private readonly CustomOption<ListWithSelection<TItem>> _option;

        public SelectionMenu(
            string header,
            IOutput? description,
            IEnumerable<TItem> items,
            Func<TItem, string>? labeler,
            AsyncOptionHandler<ListWithSelection<TItem>> handler,
            CompletionHandler? completionHandler,
            int columns = 1,
            bool canCancel = true)
        : base(
              new(),
              header,
              description,
              GetInputter(items, header, description ?? ColoredOutput.Empty, labeler, columns, canCancel))
        {
            _option = new CustomOption<ListWithSelection<TItem>>(
                null,
                x => true,
                handler.IfSelected(),
                completionHandler);
            AddOption(_option);
        }

        private static Inputter<ListWithSelection<TItem>> GetInputter(
            IEnumerable<TItem> items,
            string header,
            IOutput? description,
            Func<TItem, string>? labeler,
            int columns = 1,
            bool canCancel = true)
        {
            SelectionMenuInputter<TItem> inputter = new SelectionMenuInputter<TItem>(items, header, description, labeler)
            {
                ColumnCount = columns,
                CanCancel = canCancel,
            };

            return inputter.ChooseOption;
        }
    }

    public class SelectionMenuInputter<TItem>
    {
        protected readonly List<TItem> Items;
        protected readonly string _header;
        protected readonly IOutput? _description;
        public readonly Func<TItem, string> _labeler;

        /// <summary>
        /// Creates a user-navigable menu.
        /// </summary>
        /// <param name="items">The options available for selection.</param>
        /// <param name="header">The header/instructions for the menu, since the console will be cleared entirely.</param>
        /// <param name="labeler">A function that converts a list element to a string label, null to use <see cref="TItem.ToString()"/></param>.
        public SelectionMenuInputter(
            IEnumerable<TItem> items,
            string? header,
            IOutput? description,
            Func<TItem, string>? labeler = null)
        {
            Items = items.ToList();
            _header = header ?? "Menu";
            _description = description;
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

        public ConsoleColor HoverColor { get; set; } = ConsoleColor.Cyan;
        public ConsoleColor NormalColor { get; set; } = ConsoleColor.White;

        public ListWithSelection<TItem> ChooseOption()
        {
            // Adapted From: https://stackoverflow.com/questions/46908148/controlling-menu-with-the-arrow-keys-and-enter

            if (!Items.Any())
            {
                ColoredOutput.WriteLines(_header, "No items are available to select...");
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
                Console.Clear();
                Console.SetCursorPosition(0, 0);
                ColoredOutput.WriteLines(_header);
                _description?.WriteLine();

                (_, startY) = Console.GetCursorPosition();
                startY += 2;
                Console.SetCursorPosition(startX, startY);

                for (int i = 0; i < Items.Count; i++)
                {
                    Console.SetCursorPosition(startX + (i % ColumnCount) * spacingPerLine, startY + i / ColumnCount);
                    PrintedLabel(i).Write();
                }

                key = Console.ReadKey(true).Key;

                if (CanCancel && key == ConsoleKey.Escape)
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
                        var newIndex = Math.Min(Items.Count - 1, SelectedIndex + ColumnCount);
                        if (newIndex / ColumnCount > SelectedIndex / ColumnCount)
                            SelectedIndex = newIndex;
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
        public List<T> Items { get; init; }

        public ListWithSelection(List<T> items, int selectedIndex)
        {
            SelectedIndex = selectedIndex;
            Items = items;
        }
    }

    public static class OptionHandlerExtension
    {
        /// <summary>
        /// Only completes the handler if an option was selected and the menu wasn't cancelled.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static AsyncOptionHandler<ListWithSelection<T>> IfSelected<T>(this AsyncOptionHandler<ListWithSelection<T>> handler) =>
            (x, c) =>
            {
                if (x.SelectedIndex == -1)
                {
                    c.CompletionHandler = CompletionHandlers.Back;
                    return Task.CompletedTask;
                }
                return handler(x, c);
            };
    }
}