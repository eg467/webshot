using System;

namespace WebShot.Menu
{
    /// <summary>
    /// Throw this before processing begins if a command matches an option pattern, but is somehow invalid.
    /// User should be safe to continue and retry input.
    /// </summary>
    [Serializable]
    public class MenuInputException : Exception
    {
        public MenuInputException()
        {
        }

        public MenuInputException(string message) : base(message)
        {
        }

        public MenuInputException(string message, Exception inner) : base(message, inner)
        {
        }

        protected MenuInputException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}