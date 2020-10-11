using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebShot.Menu.Menus;
using WebShot.Menu.Options;

namespace WebShot.Menu
{
    public sealed class MenuNavigator
    {
        public event EventHandler? Exited;

        public int Count => _stack.Count;
        private readonly Stack<MenuCreator> _stack = new();

        public async Task ExecuteCurrentMenu()
        {
            MenuCreator menuCreator = _stack.Peek();
            IMenu menu = menuCreator();
            CompletionHandler completion = await menu.DisplayAsync();
            await completion(this);
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

        public Task Root()
        {
            while (_stack.Count > 1)
            {
                _stack.Pop();
            }
            return ExecuteCurrentMenu();
        }
    }
}