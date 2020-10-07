using Redux;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebshotService.State
{
    public delegate Task AsyncActionCreator<TState>(Dispatcher dispatcher, Func<TState> getState);

    public delegate void ActionCreator<TState>(Dispatcher dispatcher, Func<TState> getState);

    public static class ReduxExtensions
    {
        /// <summary>
        /// Extension on IStore to dispatch multiple actions via a thunk.
        /// Can be used like https://github.com/gaearon/redux-thunk without the need of middleware.
        /// </summary>
        public static Task Dispatch<TState>(this IStore<TState> store, AsyncActionCreator<TState> actionsCreator)
        {
            return actionsCreator(store.Dispatch, store.GetState);
        }

        public static void Dispatch<TState>(this IStore<TState> store, ActionCreator<TState> actionsCreator)
        {
            actionsCreator(store.Dispatch, store.GetState);
        }
    }
}