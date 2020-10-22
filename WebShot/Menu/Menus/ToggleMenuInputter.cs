using System;
using System.Collections.Generic;
using WebShot.Menu.ColoredConsole;

namespace WebShot.Menu.Menus
{
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
            MenuOutput output,
            Func<(TItem, bool), string>? labeler = null)
        : base(items, output, labeler)
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