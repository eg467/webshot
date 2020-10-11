using Redux;
using System;

namespace WebshotService.State.Reducers
{
    public interface IReducerCombiner
    {
        void SetChanged(bool hasChanged);
    }

    /// <summary>
    /// There is no clean redux.net way to return the original reference when combining reducers.
    /// This returns a reference to the original object (if reference type) if the objects fields were unchanged.
    /// Alternatively, use JSON-serialized comparison.
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public class ReducerCombiner<TState> : IReducerCombiner
    {
        private readonly TState _state;
        private readonly IAction _action;
        private readonly IReducerCombiner? _parent;

        private Reducer<TState>? _mainReducer;
        private Reducer<TState>? _setterReducer;

        public bool WasChanged { get; private set; }

        public ReducerCombiner(TState state, IAction action)
        {
            _state = state;
            _action = action;
        }

        private ReducerCombiner(TState state, IAction action, IReducerCombiner parent) : this(state, action)
        {
            _parent = parent;
        }

        public void SetChanged(bool hasChanged)
        {
            WasChanged = WasChanged || hasChanged;
            _parent?.SetChanged(WasChanged);
        }

        public ReducerCombiner<TSubState> Sub<TSubState>(Func<TState, TSubState> selector) =>
            new ReducerCombiner<TSubState>(selector(_state), _action, this);

        public ReducerCombiner<TSubState> Sub<TSubState>(TSubState subitem) =>
            Sub(_ => subitem);

        /// <summary>
        /// Specify an action that will return an entire TState if it matches the reducer's action.
        /// </summary>
        /// <typeparam name="TSetterAction"></typeparam>
        /// <param name="selector"></param>
        /// <returns></returns>
        public ReducerCombiner<TState> Setter<TSetterAction>(Func<TSetterAction, TState> selector)
        {
            _setterReducer = (s, a) => (a is TSetterAction setterAction) ? selector(setterAction) : s;
            return this;
        }

        public ReducerCombiner<TState> Reducer(Reducer<TState> reducer)
        {
            _mainReducer = reducer;
            return this;
        }

        private Reducer<TState> CreateTrackedReducer(Reducer<TState> reducer) =>
            reducer is object
            ? (Reducer<TState>)((s, a) =>
            {
                var newState = reducer(s, a);
                SetChanged(!Equals(s, newState));
                return newState;
            })
            : (s, a) => s;

        public TState Reduce()
        {
            // Try the setter reducer first, then the main reducer, then return the original state if no changes were made
            return
                TryReducer(_setterReducer, out var setterResult)
                ? setterResult
                : TryReducer(_mainReducer, out var mainResult)
                ? mainResult
                : _state;

            bool TryReducer(Reducer<TState>? reducer, out TState result)
            {
                if (reducer is null)
                {
                    result = _state;
                    return false;
                }
                var trackedReducer = CreateTrackedReducer(reducer);
                result = trackedReducer(_state, _action);
                return WasChanged;
            }
        }
    }
}