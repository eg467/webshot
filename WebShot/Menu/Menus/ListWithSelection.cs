using System;
using System.Collections.Generic;

namespace WebShot.Menu.Menus
{
    public record ListWithSelection<T>
    {
        public T Item
        {
            get
            {
                if (Cancelled)
                    throw new ArgumentOutOfRangeException(nameof(SelectedIndex));
                return Items[SelectedIndex];
            }
        }

        public bool Cancelled => SelectedIndex == -1;

        public int SelectedIndex { get; init; }
        public List<T> Items { get; init; }

        public ListWithSelection(List<T> items, int selectedIndex)
        {
            SelectedIndex = selectedIndex;
            Items = items;
        }
    }
}