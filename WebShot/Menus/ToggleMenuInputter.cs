using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebShot.Menus.ColoredConsole;

namespace WebShot.Menus
{
    public partial class ToggleMenu<TItem> : Menu<ListWithSelection<(TItem, bool)>>
    {
        public int Columns { get; set; }

        private readonly Option<ListWithSelection<(TItem, bool)>> _option;

        public ToggleMenu(
            ColoredOutput header,
            IEnumerable<TItem> items,
            Func<TItem, string>? labeler,
            Func<ListWithSelection<(TItem, bool)>, ICompletionHandler, Task> handler,
            CompletionHandler? completionHandler,
            int columns = 1,
            bool canCancel = true)
        : this(header, SelectAll(items), labeler, handler, completionHandler, columns, canCancel)
        {
        }

        public ToggleMenu(
            ColoredOutput header,
            IEnumerable<(TItem, bool)> items,
            Func<TItem, string>? labeler,
            Func<ListWithSelection<(TItem, bool)>, ICompletionHandler, Task> handler,
            CompletionHandler? completionHandler,
            int columns = 1,
            bool canCancel = true)
        : base(new(), header, GetInputter(items, header, labeler))
        {
            _option = new Option<ListWithSelection<(TItem, bool)>>(
                null,
                x => true,
                (x, c) =>
                {
                    if (x.SelectedIndex == -1)
                    {
                        c.CompletionHandler = OptionCompletionHandlers.Back;
                        return Task.CompletedTask;
                    }
                    return handler(x, c);
                },
                completionHandler);
            AddOption(_option);
        }

        private static IEnumerable<(TItem, bool)> SelectAll(IEnumerable<TItem> items) =>
            items.Select(i => (i, true));

        private static Inputter<ListWithSelection<(TItem, bool)>> GetInputter(
            IEnumerable<(TItem, bool)> items,
            IOutput header,
            Func<TItem, string>? labeler,
            int columns = 1,
            bool canCancel = true)
        {
            ToggleMenuInputter<TItem> inputter = new ToggleMenuInputter<TItem>(
                items,
                header,
                labeler)
            {
                ColumnCount = columns,
                CanCancel = canCancel,
            };

            return inputter.ChooseOption;
        }
    }

    public class ToggleMenuInputter<TItem> : SelectionMenuInputter<(TItem Item, bool Enabled)>
    {
        public ConsoleColor EnabledColor { get; set; } = ConsoleColor.Green;
        public ConsoleColor DisabledColor { get; set; } = ConsoleColor.Red;

        /// <summary>
        /// Creates a user-navigable menu.
        /// </summary>
        /// <param name="items">The options available for selection.</param>
        /// <param name="labeler">A function that converts a list element to a string label, null to use <see cref="TItem.ToString()"/></param>.
        public ToggleMenuInputter(
            IEnumerable<(TItem, bool)> items,
            IOutput header,
            Func<TItem, string>? labeler = null)
        : base(
            items,
            header,
            labeler is object ? (x => labeler(x.Item)) : null)
        {
        }

        protected override IOutput PrintedLabel(int index)
        {
            ColoredOutput checkLabel = Items[index].Enabled
                    ? new("[\u2713] ", EnabledColor)
                    : new("[ ] ", DisabledColor);

            IOutput baseLabel = base.PrintedLabel(index);
            return MixedOutput.Horizontal(new[] { checkLabel, baseLabel });
        }

        /// <summary>
        /// Respond to a user keypress.
        /// Call base at beginning or end of override depending on if the base handlers should be superceded.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if the key was handled and processing should cease.</returns>
        protected override bool HandleKey(ConsoleKey key)
        {
            if (base.HandleKey(key))
                return true;

            switch (key)
            {
                case ConsoleKey.Spacebar:
                    {
                        (TItem item, bool enabled) = Items[SelectedIndex];
                        Items[SelectedIndex] = (item, !enabled);
                        break;
                    }
                default:
                    return false;
            }
            return true;
        }
    }
}