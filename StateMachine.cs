using System;
using System.Collections.Generic;
using System.Linq;

// TODO Implement State Conditions for entering / exiting
// TODO Implement per-frame delegate actions that repeat while in a specific state.
// TODO Automatically transition states if exit condition is true and only one transition has true enter condition
// TODO if (MustTransitionFrom (State.CliffHanging) &&
// TODO AllTransitionsInvalidExcept (State.FreeFalling)) // then transition to FreeFall
// 1. Child states can be pushed and popped from a parent state.
// 2. Child state can transition to new parent state.
// 3. Parent states cannot be pushed / popped.
// 3. Any child states are lost when changing to new parent state.
// 4. Initial state is a parent state.
public class StateMachine <T> : IStateMachine <T> where T : struct, Enum
{
  private T _currentState;
  private T _parentState;
  private readonly T _initialState;
  private static readonly T AnyState = (T) (object) -1;
  private readonly Dictionary <T, HashSet <T>> _transitionTable;
  private readonly Stack <T> _childStates = new();
  private readonly Dictionary <T, Dictionary <T, IStateMachine <T>.TransitionAction>> _actions = new();
  private readonly Dictionary <T, Dictionary <T, IStateMachine <T>.TransitionTrigger>> _triggers = new();

  public StateMachine (Dictionary <T, T[]> transitionTable, T initialState) : this (
    transitionTable.ToDictionary (kvp => kvp.Key, kvp => kvp.Value.ToHashSet()), initialState)
  {
  }

  private StateMachine (Dictionary <T, HashSet <T>> transitionTable, T initialState)
  {
    if (transitionTable.ContainsKey (AnyState) || transitionTable.Values.Any (x => x.Any (y => Equals (y, AnyState))))
    {
      throw new ArgumentException ($"{nameof (transitionTable)} cannot contain wildcard {ToString (AnyState)}");
    }

    _initialState = initialState;
    _currentState = initialState;
    _parentState = initialState;
    _transitionTable = transitionTable;
  }

  public bool Is (T state) => Equals (_currentState, state);
  public void OnTransitionTo (T to, IStateMachine <T>.TransitionAction action) => OnTransition (AnyState, to, action);
  public void OnTransitionFrom (T from, IStateMachine <T>.TransitionAction action) => OnTransition (from, AnyState, action);

  public void OnTransition (T from, T to, IStateMachine <T>.TransitionAction action)
  {
    if (!HasWildcards (from, to) && !CanTransition (from, to))
    {
      Log.Warn ($"Ignoring invalid transition from {ToString (from)} to {ToString (to)}.");

      return;
    }

    if (!_actions.ContainsKey (from)) _actions.Add (from, new Dictionary <T, IStateMachine <T>.TransitionAction>());

    if (_actions[from].ContainsKey (to))
    {
      throw new ArgumentException ($"Transition action from {ToString (from)} to {ToString (to)} already exists.");
    }

    _actions[from].Add (to, action);
  }

  public void AddTrigger (T from, T to, IStateMachine <T>.TransitionTrigger trigger)
  {
    if (HasWildcards (to))
    {
      throw new ArgumentException (
        $"Trigger from {ToString (from)} to {ToString (to)} cannot contain a destination wildcard {ToString (AnyState)}.");
    }

    if (!HasWildcards (from, to) && !CanTransition (from, to))
    {
      Log.Warn ($"Ignoring invalid trigger from {ToString (from)} to {ToString (to)}.");

      return;
    }

    if (!_triggers.ContainsKey (from)) _triggers.Add (from, new Dictionary <T, IStateMachine <T>.TransitionTrigger>());

    if (_triggers[from].ContainsKey (to))
    {
      throw new ArgumentException ($"Trigger from {ToString (from)} to {ToString (to)} already exists.");
    }

    _triggers[from].Add (to, trigger);
  }

  public void Update()
  {
    if (!_triggers.ContainsKey (_currentState)) return;

    var triggeredStates = _triggers[_currentState].Keys.Where (to => _triggers[_currentState][to]()).ToList();

    switch (triggeredStates.Count)
    {
      case 0:
        break;
      case 1:
        var to = triggeredStates.Single();

        if (CanPopTo (to))
        {
          Pop();
        }
        else if (IsReversible (to))
        {
          Push (to);
        }
        else
        {
          To (to);
        }

        break;
      default:
        Log.Warn (
          $"Ignoring multiple valid triggers from {ToString (_currentState)} to {Tools.ToString (triggeredStates, f: ToString)}.");

        break;
    }
  }

  public void To (T to)
  {
    if (!ShouldExecuteChangeState (to)) return;

    ExecuteChangeState (to);
    _childStates.Clear();
    _parentState = to;
  }

  public void ToIf (T to, bool condition)
  {
    if (condition) To (to);
  }

  public void Push (T to)
  {
    if (!ShouldExecuteChangeState (to)) return;

    if (!IsReversible (to))
    {
      Log.Warn ($"Cannot push {ToString (to)}: Would not be able to change back to {ToString (_currentState)} " +
                $"from {ToString (to)}.\nUse {nameof (IStateMachine <T>)}#{nameof (To)} for a one-way transition.");

      return;
    }

    if (!IsCurrentChild (to)) _childStates.Push (to);
    Log.Debug ($"Pushed: {ToString (to)}");
    Log.All (PrintStates());
    ExecuteChangeState (to);
  }

  public void PushIf (T to, bool condition)
  {
    if (condition) Push (to);
  }

  public void Pop()
  {
    if (!CanPop())
    {
      Log.Warn ($"Cannot pop {ToString (_currentState)}: wasn't pushed.\nUse {nameof (IStateMachine <T>)}#{nameof (To)} instead.");

      return;
    }

    var to = DoublePeek();

    if (!ShouldExecuteChangeState (to)) return;

    _childStates.Pop();
    Log.Debug ($"Popped: {ToString (_currentState)}");
    Log.Debug (PrintStates());
    ExecuteChangeState (to);
  }

  public void Reset()
  {
    _currentState = _initialState;
    _parentState = _initialState;
    _childStates.Clear();
  }

  public void PopIf (bool condition)
  {
    if (condition) Pop();
  }

  public void PopIf (T state, bool condition) => PopIf (Is (state) && condition);
  public void PopIf (T state) => PopIf (Is (state));
  private bool IsReversible (T state) => CanTransition (state, _currentState);
  private bool CanPopTo (T to) => CanPop() && Equals (DoublePeek(), to);
  private bool CanPop() => IsCurrentChild (_currentState);
  private bool IsCurrentChild (T state) => _childStates.Count > 0 && Equals (state, _childStates.Peek());
  private bool CanTransitionTo (T to) => CanTransition (_currentState, to);
  private bool CanTransition (T from, T to) { return _transitionTable.TryGetValue (from, out var toStates) && toStates.Contains (to); }
  private bool HasTransitionAction (T from, T to) { return _actions.TryGetValue (from, out var actions) && actions.ContainsKey (to); }
  private string PrintStates() => $"States:\nChildren:\n{Tools.ToString (_childStates, "\n")}\nParent:\n{_parentState}";
  private static bool HasWildcards (params T[] states) => states.Any (state => Equals (state, AnyState));
  private static string ToString (T t) => Equals (t, AnyState) ? nameof (AnyState) : t.ToString();

  private T DoublePeek()
  {
    if (_childStates.Count == 0)
    {
      throw new InvalidOperationException ($"Cannot double peek: current state {ToString (_currentState)} wasn't pushed.");
    }

    return _childStates.Count > 1 ? _childStates.Skip (1).First() : _parentState;
  }

  private bool ShouldExecuteChangeState (T to)
  {
    if (Equals (to, _currentState))
    {
      Log.All ($"Ignoring duplicate {ToString (to)} state transition.");

      return false;
    }

    // ReSharper disable once InvertIf
    if (!CanTransitionTo (to))
    {
      Log.Warn ($"Ignoring invalid transition from {ToString (_currentState)} to {ToString (to)}.");

      return false;
    }

    return true;
  }

  private void ExecuteChangeState (T to)
  {
    ExecuteTransitionActionsTo (to);
    Log.Info ($"State transition: {ToString (_currentState)} => {ToString (to)}");
    _currentState = to;
  }

  private void ExecuteTransitionActionsTo (T to)
  {
    foreach (var fromState in new[] { _currentState, AnyState })
    {
      foreach (var toState in new[] { to, AnyState })
      {
        if (HasTransitionAction (fromState, toState)) _actions[fromState][toState]();
      }
    }
  }
}