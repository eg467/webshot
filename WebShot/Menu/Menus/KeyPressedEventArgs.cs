using System;

namespace WebShot.Menu.Menus
{
    public class KeyPressedEventArgs<T> : EventArgs
    {
        public T Item { get; }
        public ConsoleKey Key { get; }

        public bool WasCancelled => KeyPressResponse == KeyPressResponse.Cancel;
        public bool WasSubmitted => KeyPressResponse == KeyPressResponse.SubmitMenu;

        public KeyPressResponse KeyPressResponse { get; set; } = KeyPressResponse.Continue;

        public KeyPressedEventArgs(T item, ConsoleKey key)
        {
            Item = item;
            Key = key;
        }
    }

    public enum KeyPressResponse { Continue, SubmitMenu, Cancel }
}