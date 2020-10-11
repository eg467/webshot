using System;
using System.Threading.Tasks;
using WebShot.Menu.Menus;

namespace WebShot.Menu.Options
{
    public static class OptionCompletionHandlers
    {
        public static Task Back(MenuNavigator nav) => nav.Back();

        public static CompletionHandler FromMenuCreator(MenuCreator menuCreator) =>
            (MenuNavigator nav) => nav.DisplayNew(menuCreator);

        public static CompletionHandler FromMenu<TInput>(Menu<TInput> menu) =>
            FromMenuCreator(() => menu);

        public static Task Repeat(MenuNavigator nav) => nav.ExecuteCurrentMenu();

        public static Task Root(MenuNavigator nav) => nav.Root();

        public static Task Exit(MenuNavigator _)
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }
    }
}