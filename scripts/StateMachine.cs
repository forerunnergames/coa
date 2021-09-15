using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

// TODO Implement per-frame delegate actions that repeat while in a specific state.
// TODO Implement Godot callback triggers or allow the state machine to register / listen for emitted signals.
// 1. Child states can be pushed and popped from a parent state.
// 2. Child state can transition to new parent state.
// 3. Parent states cannot be pushed / popped.
// 3. Any child states are lost when changing to new parent state.
// 4. Initial state is a parent state.
public class StateMachine <T> : IStateMachine <T> where T : struct, Enum
{
  public Log.Level LogLevel { set => _log.CurrentLevel = value; }
  private T _currentState;
  private T _parentState;
  private readonly T _initialState;
  private static readonly T AnyState = (T)(object)-1;
  private readonly Dictionary <T, HashSet <T>> _transitionTable;
  private readonly Stack <T> _childStates = new();
  private readonly Dictionary <T, Dictionary <T, IStateMachine <T>.TransitionAction>> _actions = new();
  private readonly Dictionary <T, Dictionary <T, IStateMachine <T>.TransitionTrigger>> _triggers = new();
  private readonly List <T> _triggeredFromStates = new();
  private readonly List <T> _triggeredToStates = new();
  private readonly List <T> _fromStates = new();
  private readonly List <T> _toStates = new();
  private readonly Log _log;

  // ReSharper disable once ExplicitCallerInfoArgument
  public StateMachine (Dictionary <T, T[]> transitionTable, T initialState, [CallerFilePath] string name = "") : this (
    transitionTable.ToDictionary (kvp => kvp.Key, kvp => kvp.Value.ToHashSet()), initialState, name)
  {
  }

  // ReSharper disable once ExplicitCallerInfoArgument
  private StateMachine (Dictionary <T, HashSet <T>> transitionTable, T initialState, [CallerFilePath] string name = "")
  {
    if (typeof (T).IsEnumDefined (-1))
    {
      throw new ArgumentException (
        $"States cannot contain [{typeof (T).GetEnumName (-1)}] having same value as {ToString (AnyState)}.");
    }

    if (transitionTable.ContainsKey (AnyState))
    {
      throw new ArgumentException (
        $"Transition table key [{transitionTable.GetEntry (AnyState).Key}] cannot have same value as {ToString (AnyState)}.");
    }

    if (transitionTable.Values.Any (x => x.Any (y => Equals (y, AnyState))))
    {
      throw new ArgumentException (
        $"Transition table values [{Tools.ToString (transitionTable.Values.First (x => x.Any (y => Equals (y, AnyState))))}] " +
        $"cannot contain same value as {ToString (AnyState)}.");
    }

    if (initialState.Equals (AnyState))
    {
      throw new ArgumentException ($"Initial state [{initialState}] cannot have same value as {ToString (AnyState)}.");
    }

    if (!transitionTable.ContainsKey (initialState))
    {
      throw new ArgumentException ($"Initial state [{initialState}] not found in transition table.");
    }

    _log = new Log (name);
    _initialState = initialState;
    _currentState = initialState;
    _parentState = initialState;
    _transitionTable = transitionTable;
    _log.Info ($"Initial state: {ToString (_initialState)}");
  }

  public T GetState() => _currentState;
  public bool Is (T state) => Equals (_currentState, state);

  public void OnTransitionTo (T to, IStateMachine <T>.TransitionAction action)
  {
    if (to.Equals (AnyState)) throw new ArgumentException ($"Transition to {ToString (to)} cannot contain {ToString (AnyState)}.");

    AddWildcardableTransition (AnyState, to, action);
  }

  public void OnTransitionFrom (T from, IStateMachine <T>.TransitionAction action)
  {
    if (from.Equals (AnyState))
    {
      throw new ArgumentException ($"Transition from {ToString (from)} cannot contain {ToString (AnyState)}.");
    }

    AddWildcardableTransition (from, AnyState, action);
  }

  public void OnTransition (T from, T to, IStateMachine <T>.TransitionAction action)
  {
    if (from.Equals (AnyState) || to.Equals (AnyState))
    {
      throw new ArgumentException ($"Transition from {ToString (from)} to {ToString (to)} cannot contain {ToString (AnyState)}.");
    }

    AddWildcardableTransition (from, to, action);
  }

  public void AddTriggerTo (T to, IStateMachine <T>.TransitionTrigger trigger)
  {
    if (to.Equals (AnyState)) throw new ArgumentException ($"Trigger to {ToString (to)} cannot contain {ToString (AnyState)}.");

    AddWildcardableTrigger (AnyState, to, trigger);
  }

  public void AddTrigger (T from, T to, IStateMachine <T>.TransitionTrigger trigger)
  {
    if (from.Equals (AnyState) || to.Equals (AnyState))
    {
      throw new ArgumentException ($"Trigger from {ToString (from)} to {ToString (to)} cannot contain {ToString (AnyState)}.");
    }

    AddWildcardableTrigger (from, to, trigger);
  }

  public void Update()
  {
    if (!_triggers.ContainsKey (_currentState) && !_triggers.ContainsKey (AnyState)) return;

    _fromStates.Clear();
    _fromStates.Add (_currentState);
    _fromStates.Add (AnyState);
    _triggeredFromStates.Clear();
    _triggeredToStates.Clear();

    foreach (var fromState in ImmutableList.CreateRange (_fromStates))
    {
      if (!_triggers.ContainsKey (fromState)) continue;

      foreach (var toState in _triggers[fromState].Keys.Where (to => _triggers[fromState][to]()))
      {
        _triggeredFromStates.Add (fromState);
        _triggeredToStates.Add (toState);
      }
    }

    if (_triggeredFromStates.Count == 0 || _triggeredToStates.Count == 0) return;

    if (_triggeredFromStates.Count > 1 || _triggeredToStates.Count > 1)
    {
      _log.Warn ($"Ignoring multiple valid triggers from {ToString (_currentState)} to [{Tools.ToString (_triggeredToStates)}].");

      return;
    }

    TriggerState (_triggeredFromStates.Single(), _triggeredToStates.Single());
  }

  public void Reset (IStateMachine <T>.ResetOption resetOption)
  {
    // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
    switch (resetOption)
    {
      case IStateMachine <T>.ResetOption.ExecuteTransitionActions when !CanTransition (_currentState, _initialState):
        _log.Warn ($"Not executing transition actions during reset because transition {ToString (_currentState)} => " +
                   $"{ToString (_initialState)} is invalid.");

        _log.Debug ($"Ignoring invalid trigger from {ToString (_currentState)} => {ToString (_initialState)}.");

        break;
      case IStateMachine <T>.ResetOption.IgnoreTransitionActions
        when HasTransitionAction (_currentState, _initialState) || HasTransitionAction (AnyState, _initialState):
        _log.Debug ($"Ignoring valid transition actions during reset transition {ToString (_currentState)} => " +
                    $"{ToString (_initialState)}");

        break;
      case IStateMachine <T>.ResetOption.ExecuteTransitionActions when CanTransition (_currentState, _initialState):
        TriggerState (_currentState, _initialState);

        break;
    }

    _currentState = _initialState;
    _parentState = _initialState;
    _childStates.Clear();
    _log.Info ("Reset state machine.");
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
      _log.Warn ($"Cannot push {ToString (to)}: Would not be able to change back to {ToString (_currentState)} " +
                 $"from {ToString (to)}.\nUse {nameof (IStateMachine <T>)}#{nameof (To)} for a one-way transition.");

      return;
    }

    if (!IsCurrentChild (to)) _childStates.Push (to);
    _log.Debug ($"Pushed: {ToString (to)}");
    _log.All (PrintStates());
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
      _log.Warn ($"Cannot pop {ToString (_currentState)}: wasn't pushed.\nUse {nameof (IStateMachine <T>)}#{nameof (To)} instead.");

      return;
    }

    var to = DoublePeek();

    if (!ShouldExecuteChangeState (to)) return;

    _childStates.Pop();
    _log.Debug ($"Popped: {ToString (_currentState)}");
    _log.Debug (PrintStates());
    ExecuteChangeState (to);
  }

  public void PopIf (bool condition)
  {
    if (condition) Pop();
  }

  // @formatter:off
  public void PopIf (T state, bool condition) => PopIf (Is (state) && condition);
  public void PopIf (T state) => PopIf (Is (state));
  private bool IsReversible (T state) => CanTransition (state, _currentState);
  private bool CanPopTo (T to) => CanPop() && Equals (DoublePeek(), to);
  private bool CanPop() => IsCurrentChild (_currentState);
  private bool IsCurrentChild (T state) => _childStates.Count > 0 && Equals (state, _childStates.Peek());
  private bool CanTransitionTo (T to) => CanTransition (_currentState, to);
  private bool CanTransition (T from, T to) => _transitionTable.TryGetValue (from, out var toStates) && toStates.Contains (to);
  private bool HasTransitionAction (T from, T to) => _actions.TryGetValue (from, out var actions) && actions.ContainsKey (to);
  private static bool HasWildcards (params T[] states) => states.Any (state => Equals (state, AnyState));
  private string PrintStates() => $"States:\nChildren:\n{Tools.ToString (_childStates, "\n", "[", "]")}\nParent:\n{ToString (_parentState)}";
  private static string ToString (T t) => Equals (t, AnyState) ? $"internal wildcard state [{nameof (AnyState)} = -1]" : $"[{t.ToString()}]";
  // @formatter:on

  private void AddWildcardableTransition (T from, T to, IStateMachine <T>.TransitionAction action)
  {
    if (!HasWildcards (from, to) && !CanTransition (from, to))
    {
      _log.Warn ($"Not adding transition not found in transition table from {ToString (from)} to {ToString (to)}.");

      return;
    }

    if (_actions.ContainsKey (from) && _actions[from].ContainsKey (to))
    {
      _log.Warn ($"Not adding duplicate transition from {ToString (from)} to {ToString (to)}.");

      return;
    }

    if (!_actions.ContainsKey (from)) _actions.Add (from, new Dictionary <T, IStateMachine <T>.TransitionAction>());
    _actions[from].Add (to, action);
  }

  private void AddWildcardableTrigger (T from, T to, IStateMachine <T>.TransitionTrigger trigger)
  {
    if (!HasWildcards (from, to) && !CanTransition (from, to))
    {
      _log.Warn ($"Not adding trigger with transition not found in transition table from {ToString (from)} to {ToString (to)}.");

      return;
    }

    if (_triggers.ContainsKey (from) && _triggers[from].ContainsKey (to))
    {
      _log.Warn ($"Not adding duplicate trigger from {ToString (from)} to {ToString (to)}.");

      return;
    }

    if (!_triggers.ContainsKey (from)) _triggers.Add (from, new Dictionary <T, IStateMachine <T>.TransitionTrigger>());
    _triggers[from].Add (to, trigger);
  }

  private T DoublePeek()
  {
    if (_childStates.Count == 0)
    {
      throw new InvalidOperationException ($"Cannot double peek: current state {ToString (_currentState)} wasn't pushed.");
    }

    return _childStates.Count > 1 ? _childStates.Skip (1).First() : _parentState;
  }

  private void TriggerState (T from, T to)
  {
    _log.Debug ($"Executing {(from.Equals (AnyState) ? "wildcard" : "specific")} " +
                $"trigger {ToString (_currentState)} {(from.Equals (AnyState) ? "(any)" : "(specific)")} " +
                $"=> {ToString (to)} (specific).");

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
  }

  private bool ShouldExecuteChangeState (T to)
  {
    if (Equals (to, _currentState))
    {
      _log.All ($"Ignoring duplicate {ToString (to)} state transition.");

      return false;
    }

    if (CanTransitionTo (to)) return true;

    _log.Warn ($"Ignoring invalid transition from {ToString (_currentState)} to {ToString (to)}.");

    return false;
  }

  private void ExecuteChangeState (T to)
  {
    ExecuteTransitionActionsTo (to);
    _log.Info ($"State transition: {ToString (_currentState)} => {ToString (to)}");
    _currentState = to;
  }

  private void ExecuteTransitionActionsTo (T to)
  {
    _fromStates.Clear();
    _fromStates.Add (_currentState);
    _fromStates.Add (AnyState);
    _toStates.Clear();
    _toStates.Add (to);
    _toStates.Add (AnyState);

    foreach (var fromState in ImmutableList.CreateRange (_fromStates))
    {
      foreach (var toState in ImmutableList.CreateRange (_toStates).Where (toState => HasTransitionAction (fromState, toState)))
      {
        _log.Debug ($"Executing {(fromState.Equals (AnyState) || toState.Equals (AnyState) ? "wildcard" : "specific")} " +
                    $"transition action {ToString (_currentState)} {(fromState.Equals (AnyState) ? "(any)" : "(specific)")} " +
                    $"=> {ToString (to)} {(toState.Equals (AnyState) ? "(any)" : "(specific)")}.");

        _actions[fromState][toState]();
      }
    }
  }
}