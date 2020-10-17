using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebShot.Menu.ColoredConsole;
using WebShot.Menu.Menus;
using WebShot.Menu.Options;

namespace WebShot.Menu
{
    public sealed class MenuNavigator
    {
        public event EventHandler? Exited;

        private ILogger<MenuNavigator> _logger;

        public MenuNavigator(ILogger<MenuNavigator> logger)
        {
            _logger = logger;
        }

        public int Count => _stack.Count;
        public bool IsRoot => Count == 1;
        private readonly Stack<MenuCreator> _stack = new();

        public async Task ExecuteCurrentMenu()
        {
            do
            {
                try
                {
                    MenuCreator menuCreator = _stack.Peek();
                    IMenu menu = menuCreator();
                    CompletionHandler completion = await menu.DisplayAsync();
                    await completion(this);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,  ex.Message);
                    if (IsRoot)
                        await ToRoot();
                    else
                        await Back();
                }
            } while (true);
        }

        public Task DisplayNew(MenuCreator m)
        {
            _stack.Push(m);
            return ExecuteCurrentMenu();
        }

        public Task Replace(MenuCreator m)
        {
            _stack.Pop();
            _stack.Push(m);
            return ExecuteCurrentMenu();
        }

        public void Exit()
        {
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public Task Back()
        {
            _stack.Pop();
            if (_stack.Count == 0)
            {
                Exit();
                return Task.CompletedTask;
            }

            return ExecuteCurrentMenu();
        }

        public Task ToRoot()
        {
            while (_stack.Count > 1)
                _stack.Pop();
            return ExecuteCurrentMenu();
        }
    }
}