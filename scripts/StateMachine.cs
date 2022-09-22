using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using static System.Collections.Immutable.ImmutableList;
using static Gravity;
using static Inputs;
using static Motions;
using static Positionings;

// TODO Implement Godot callback triggers or allow the state machine to register / listen for emitted signals.
// 1. Child states can be pushed and popped from a parent state.
// 2. Child state can transition to new parent state.
// 3. Parent states cannot be pushed / popped.
// 3. Any child states are lost when changing to new parent state.
// 4. Initial state is a parent state.
// ReSharper disable ExplicitCallerInfoArgument
public class StateMachine <T> : IStateMachine <T> where T : struct, Enum
{
  private T _currentState;
  private T _parentState;
  private readonly T _initialState;
  private static readonly T AnyState = (T)(object)-1;
  private readonly Dictionary <T, HashSet <T>> _transitionTable;
  private readonly Stack <T> _childStates = new();
  private readonly Dictionary <T, Dictionary <T, TransitionAction>> _transitionActions = new();
  private readonly Dictionary <T, Dictionary <T, TransitionTrigger>> _triggers = new();
  private readonly Dictionary <T, FrameAction> _frameActions = new();
  private readonly Dictionary <T, HashSet <T>> _fromExceptions = new();
  private readonly Dictionary <T, HashSet <T>> _toExceptions = new();
  private readonly List <T> _triggeredFromStates = new();
  private readonly List <T> _triggeredToStates = new();
  private readonly string _name;
  private readonly Log _log;

  private class TransitionTrigger
  {
    public Log.Level LogLevel
    {
      set
      {
        _logLevel = value;
        if (_log != null) _log.CurrentLevel = _logLevel;
      }
    }

    private readonly IInputWrapper _inputs;
    private readonly IMotionWrapper _motions;
    private readonly Positioning? _positioning;
    private readonly Func <bool> _condition;
    private readonly Func <bool> _and;
    private readonly Func <bool> _or;
    private readonly IBooleanOperator _operators;
    private Log.Level _logLevel;
    private readonly Log _log;

    public TransitionTrigger (Input? input = null, IEnumerable <IInputWrapper> inputs = null, Motion? motion = null,
      IEnumerable <IMotionWrapper> motions = null, Positioning? positioning = null, Func <bool> condition = null,
      Func <bool> and = null, Func <bool> or = null, IBooleanOperator[] others = null, Log.Level logLevel = Log.Level.Info,
      [CallerFilePath] string name = "")
    {
      var inputWrapper = input.HasValue ? _ (Required (input.Value)) : null;
      var inputsList = inputs?.ToList();
      if (inputWrapper != null) inputsList?.AddRange (inputWrapper);

      _inputs = inputsList != null ? new CompositeInputWrapper (inputsList.ToArray()) :
        inputWrapper != null ? new CompositeInputWrapper (inputWrapper) : null;

      var motionWrapper = motion.HasValue ? _ (Required (motion.Value)) : null;
      var motionsList = motions?.ToList();
      if (motionWrapper != null) motionsList?.AddRange (motionWrapper);

      _motions = motionsList != null ? new CompositeMotionWrapper (motionsList.ToArray()) :
        motionWrapper != null ? new CompositeMotionWrapper (motionWrapper) : null;

      _positioning = positioning;
      _condition = condition;
      _and = and;
      _or = or;
      _operators = others != null ? new CompositeBooleanOperator (others) : null;
      _logLevel = logLevel;
      _log = new Log (name) { CurrentLevel = logLevel };
    }

    public bool CanTransition (T from, T to, Godot.KinematicBody2D body = null, IsInputActive isInputActive = null,
      Godot.Vector2? velocity = null, GravityType? gravity = null)
    {
      // @formatter:off
      var input = isInputActive == null || (_inputs?.Compose (x => x.IsActive (isInputActive)) ?? true);
      var physicsBodyData = new PhysicsBodyData (velocity, gravity, _positioning);
      var motion = !velocity.HasValue || (_motions?.ComposeFromRequired (input, x => x.IsActive (ref physicsBodyData)) ?? input);
      var positioning = body == null || !_positioning.HasValue || _positioning.Value.IsActive (body);
      var conditions = (_condition?.Invoke() ?? true) && (_and?.Invoke() ?? true) || (_or?.Invoke() ?? false);
      var result = input && motion && positioning && conditions;
      var canTransition = _operators?.Compose (result) ?? result;

      _log.Debug ($"{nameof (TransitionTrigger)}: {from} => {to}: " +
                 $"inputs: {input} ({(_inputs != null ? _inputs : "null")}), " +
                 $"motions: {motion} ({(_motions != null ? _motions : "null")}), " +
                 $"gravity type: {(gravity != null ? gravity : "null")}, " +
                 $"velocity: {StateMachine <T>.ToString (velocity)}, " +
                 $"positioning: {positioning} ({(_positioning != null ? _positioning : "null")}, " +
                 $"body: {(body != null ? body.Name : "null")}), " +
                 $"conditions: {conditions} (condition: {(_condition != null ? _condition() : "null")}, " +
                 $"and: {(_and != null ? _and() : "null")}, or: {(_or != null ? _or() : "null")}), " +
                 $"operators: {_operators?.Compose (true) ?? true} ({(_operators != null ? _operators : "null")}), " +
                 $"can transition: {canTransition}");
      // @formatter:on

      return canTransition;
    }
  }

  private class TransitionAction
  {
    private readonly IStateMachine <T>.NonVelocityTransitionAction _nonVelocityAction;
    private readonly IStateMachine <T>.VelocityTransitionAction _velocityAction;
    private readonly Log _log;

    public TransitionAction (IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
      IStateMachine <T>.VelocityTransitionAction velocityAction = null, Log.Level logLevel = Log.Level.Info,
      [CallerFilePath] string name = "")
    {
      _nonVelocityAction = nonVelocityAction;
      _velocityAction = velocityAction;
      _log = new Log (name) { CurrentLevel = logLevel };
    }

    public Godot.Vector2? Run (T fromState, T toState, Godot.Vector2? velocity)
    {
      _log.Debug ($"BEFORE: Executing {(fromState.Equals (AnyState) || toState.Equals (AnyState) ? "wildcard" : "specific")} " +
                  ToString (_nonVelocityAction, _velocityAction) +
                  $" {StateMachine <T>.ToString (fromState)} {(fromState.Equals (AnyState) ? "(any)" : "(specific)")} =>" +
                  $" {StateMachine <T>.ToString (toState)} {(toState.Equals (AnyState) ? "(any)" : "(specific)")}." +
                  $" Velocity: {StateMachine <T>.ToString (velocity)}");

      if (_velocityAction != null) velocity = _velocityAction();
      _nonVelocityAction?.Invoke();

      _log.Debug ($"AFTER: Executing {(fromState.Equals (AnyState) || toState.Equals (AnyState) ? "wildcard" : "specific")} " +
                  ToString (_nonVelocityAction, _velocityAction) +
                  $" {StateMachine <T>.ToString (fromState)} {(fromState.Equals (AnyState) ? "(any)" : "(specific)")} =>" +
                  $" {StateMachine <T>.ToString (toState)} {(toState.Equals (AnyState) ? "(any)" : "(specific)")}." +
                  $" Velocity: {StateMachine <T>.ToString (velocity)}");

      return velocity;
    }

    private static string ToString (IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
      IStateMachine <T>.VelocityTransitionAction velocityAction = null) =>
      $"{(nonVelocityAction != null ? nameof (IStateMachine <T>.NonVelocityTransitionAction) : velocityAction != null ? nameof (IStateMachine <T>.VelocityTransitionAction) : $"null {nameof (TransitionAction)}")}";
  }

  private class FrameAction
  {
    public GravityType Gravity { get; }
    private readonly IStateMachine <T>.NonVelocityFrameAction _nonVelocityAction;
    private readonly IStateMachine <T>.VelocityFrameAction _velocityAction;
    private readonly Log _log;

    public FrameAction (GravityType gravity, IStateMachine <T>.NonVelocityFrameAction nonVelocityAction = null,
      IStateMachine <T>.VelocityFrameAction velocityAction = null, Log.Level logLevel = Log.Level.Info,
      [CallerFilePath] string name = "")
    {
      Gravity = gravity;
      _nonVelocityAction = nonVelocityAction;
      _velocityAction = velocityAction;
      _log = new Log (name) { CurrentLevel = logLevel };
    }

    public Godot.Vector2? Run (Godot.Vector2? velocity, float delta, T state)
    {
      velocity = Gravity.ApplyBefore (velocity);

      if (velocity.HasValue || _velocityAction == null)
      {
        _log.All ($"BEFORE: Executing {ToString (_nonVelocityAction, _velocityAction)} for state {state}. " +
                  $"Velocity (before gravity): {StateMachine <T>.ToString (velocity)}");
      }
      else
      {
        _log.Warn ($"Not executing {nameof (IStateMachine <T>.VelocityFrameAction)} " +
                   $"because velocity is null. Did you forget to pass velocity into {nameof (IStateMachine <T>)}.Update?");
      }

      velocity = velocity.HasValue ? _velocityAction?.Invoke (velocity.Value, delta) ?? velocity : null;
      _nonVelocityAction?.Invoke (delta);
      velocity = Gravity.ApplyAfter (velocity);

      if (velocity.HasValue || _velocityAction == null)
      {
        _log.All ($"AFTER: Executing {ToString (_nonVelocityAction, _velocityAction)} for state {state}. " +
                  $"Velocity (after gravity): {StateMachine <T>.ToString (velocity)}");
      }

      return velocity;
    }

    private static string ToString (IStateMachine <T>.NonVelocityFrameAction nonVelocityAction = null,
      IStateMachine <T>.VelocityFrameAction velocityAction = null) =>
      $"{(nonVelocityAction != null ? nameof (IStateMachine <T>.NonVelocityFrameAction) : velocityAction != null ? nameof (IStateMachine <T>.VelocityFrameAction) : $"null {nameof (FrameAction)}")}";
  }

  public StateMachine (Dictionary <T, T[]> transitionTable, T initialState, Log.Level logLevel = Log.Level.Info,
    [CallerFilePath] string name = "") : this (transitionTable.ToDictionary (kvp => kvp.Key, kvp => kvp.Value.ToHashSet()),
    initialState, logLevel, name)
  {
  }

  private StateMachine (Dictionary <T, HashSet <T>> transitionTable, T initialState, Log.Level logLevel = Log.Level.Info,
    [CallerFilePath] string name = "")
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

    _log = new Log (name) { CurrentLevel = logLevel };
    _name = name;
    _initialState = initialState;
    _currentState = initialState;
    _parentState = initialState;
    _transitionTable = transitionTable;
    _log.Info ($"Initial state: {ToString (_initialState)}");
  }

  public T GetState() => _currentState;
  public bool Is (T state) => Equals (_currentState, state);

  public void OnTransitionTo (T to, IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
    IStateMachine <T>.VelocityTransitionAction velocityAction = null) =>
    OnTransitionTo (to, new TransitionAction (nonVelocityAction, velocityAction, _log.CurrentLevel, _name));

  public void OnTransitionToExceptFrom (T to, T exception, IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
    IStateMachine <T>.VelocityTransitionAction velocityAction = null) =>
    OnTransitionToExceptFrom (to, Create (exception),
      new TransitionAction (nonVelocityAction, velocityAction, _log.CurrentLevel, _name));

  public void OnTransitionToExceptFrom (T to, ImmutableList <T> exceptions,
    IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
    IStateMachine <T>.VelocityTransitionAction velocityAction = null) =>
    OnTransitionToExceptFrom (to, exceptions, new TransitionAction (nonVelocityAction, velocityAction, _log.CurrentLevel, _name));

  public void OnTransitionFrom (T from, IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
    IStateMachine <T>.VelocityTransitionAction velocityAction = null) =>
    OnTransitionFrom (from, new TransitionAction (nonVelocityAction, velocityAction, _log.CurrentLevel, _name));

  public void OnTransitionFromExceptTo (T from, T exception, IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
    IStateMachine <T>.VelocityTransitionAction velocityAction = null) =>
    OnTransitionFromExceptTo (from, Create (exception),
      new TransitionAction (nonVelocityAction, velocityAction, _log.CurrentLevel, _name));

  public void OnTransitionFromExceptTo (T from, ImmutableList <T> exceptions,
    IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
    IStateMachine <T>.VelocityTransitionAction velocityAction = null) =>
    OnTransitionFromExceptTo (from, exceptions, new TransitionAction (nonVelocityAction, velocityAction, _log.CurrentLevel, _name));

  public void OnTransition (T from, T to, IStateMachine <T>.NonVelocityTransitionAction nonVelocityAction = null,
    IStateMachine <T>.VelocityTransitionAction velocityAction = null) =>
    OnTransition (from, to, new TransitionAction (nonVelocityAction, velocityAction, _log.CurrentLevel, _name));

  public void SetLogLevel (Log.Level level)
  {
    _log.CurrentLevel = level;
    foreach (var trigger in _triggers.Values.SelectMany (x => x.Values)) trigger.LogLevel = level;
  }

  public void AddFrameAction (T state, GravityType gravity = GravityType.AfterApplied,
    IStateMachine <T>.NonVelocityFrameAction nonVelocityAction = null, IStateMachine <T>.VelocityFrameAction velocityAction = null)
  {
    if (state.Equals (AnyState))
      throw new ArgumentException ($"Frame action state {ToString (state)} cannot contain {ToString (AnyState)}.");

    if (_frameActions.ContainsKey (state)) throw new ArgumentException ($"State {ToString (state)} already contains a frame action.");

    _frameActions[state] = new FrameAction (gravity, nonVelocityAction, velocityAction, _log.CurrentLevel, _name);
  }

  public void AddTrigger (T from, T to, Input? input = null, IEnumerable <IInputWrapper> inputs = null, Motion? motion = null,
    IEnumerable <IMotionWrapper> motions = null, Positioning? positioning = null, Func <bool> condition = null, Func <bool> and = null,
    Func <bool> or = null, IBooleanOperator[] conditions = null) =>
    AddTrigger (from, to,
      new TransitionTrigger (input, inputs, motion, motions, positioning, condition, and, or, conditions, _log.CurrentLevel, _name));

  public void AddTriggerTo (T to, Input? input = null, IEnumerable <IInputWrapper> inputs = null, Motion? motion = null,
    IEnumerable <IMotionWrapper> motions = null, Positioning? positioning = null, Func <bool> condition = null, Func <bool> and = null,
    Func <bool> or = null, IBooleanOperator[] conditions = null)
  {
    if (to.Equals (AnyState)) throw new ArgumentException ($"Trigger to {ToString (to)} cannot contain {ToString (AnyState)}.");

    AddWildcardableTrigger (AnyState, to,
      new TransitionTrigger (input, inputs, motion, motions, positioning, condition, and, or, conditions, _log.CurrentLevel, _name));
  }

  public Godot.Vector2? Update (Godot.KinematicBody2D body = null, IsInputActive input = null, Godot.Vector2? velocity = null,
    float delta = 0.0f) =>
    ExecuteTriggers (body, input, ExecuteFrameAction (velocity, delta));

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

  public Godot.Vector2? To (T to, Godot.Vector2? velocity = null)
  {
    if (!ShouldExecuteChangeState (to)) return null;

    velocity = ExecuteChangeState (to, velocity);
    _childStates.Clear();
    _parentState = to;

    return velocity;
  }

  public Godot.Vector2? Push (T to, Godot.Vector2? velocity = null)
  {
    if (!ShouldExecuteChangeState (to)) return velocity;

    if (!IsReversible (to))
    {
      _log.Warn ($"Cannot push {ToString (to)}: Would not be able to change back to {ToString (_currentState)} " +
                 $"from {ToString (to)}.\nUse {nameof (IStateMachine <T>)}#{nameof (To)} for a one-way transition.");

      return velocity;
    }

    if (!IsCurrentChild (to)) _childStates.Push (to);
    _log.Debug ($"Pushed: {ToString (to)}");
    _log.All (PrintStates());

    return ExecuteChangeState (to, velocity);
  }

  public Godot.Vector2? Pop (Godot.Vector2? velocity)
  {
    if (!CanPop())
    {
      _log.Warn ($"Cannot pop {ToString (_currentState)}: wasn't pushed.\nUse {nameof (IStateMachine <T>)}#{nameof (To)} instead.");

      return velocity;
    }

    var to = DoublePeek();

    if (!ShouldExecuteChangeState (to)) return velocity;

    _childStates.Pop();
    _log.Debug ($"Popped: {ToString (_currentState)}");
    _log.Debug (PrintStates());

    return ExecuteChangeState (to, velocity);
  }

  // @formatter:off
  public Godot.Vector2? ToIf (T to, bool condition, Godot.Vector2? velocity = null) => condition ? To (to, velocity) : velocity;
  public Godot.Vector2? PushIf (T to, bool condition, Godot.Vector2? velocity = null) => condition ? Push (to, velocity) : velocity;
  public Godot.Vector2? PopIf (bool condition, Godot.Vector2? velocity = null) => condition ? Pop (velocity) : velocity;
  public Godot.Vector2? PopIf (T state, bool condition, Godot.Vector2? velocity = null) => PopIf (Is (state) && condition, velocity);
  public Godot.Vector2? PopIf (T state, Godot.Vector2? velocity = null) => PopIf (Is (state), velocity);
  private bool IsReversible (T state) => CanTransition (state, _currentState);
  private bool CanPopTo (T to) => CanPop() && Equals (DoublePeek(), to);
  private bool CanPop() => IsCurrentChild (_currentState);
  private bool IsCurrentChild (T state) => _childStates.Count > 0 && Equals (state, _childStates.Peek());
  private bool CanTransitionTo (T to) => CanTransition (_currentState, to);
  private bool CanTransition (T from, T to) => _transitionTable.TryGetValue (from, out var toStates) && toStates.Contains (to);
  private static bool HasWildcards (params T[] states) => states.Any (state => Equals (state, AnyState));
  private string PrintStates() => $"States:\nChildren:\n{Tools.ToString (_childStates, "\n", "[", "]")}\nParent:\n{ToString (_parentState)}";
  private static string ToString (T t) => Equals (t, AnyState) ? $"internal wildcard state [{nameof (AnyState)} = -1]" : $"[{t.ToString()}]";
  private static string ToString (Godot.Vector2? velocity) => $"{(velocity.HasValue ? velocity.Value : "null")}";
  // @formatter:on

  private void OnTransitionTo (T to, TransitionAction action)
  {
    if (to.Equals (AnyState)) throw new ArgumentException ($"Transition to {ToString (to)} cannot contain {ToString (AnyState)}.");

    AddWildcardableTransition (AnyState, to, action);
  }

  private void OnTransitionToExceptFrom (T to, ImmutableList <T> exceptions, TransitionAction action)
  {
    if (exceptions.Any (x => x.Equals (AnyState)))
    {
      throw new ArgumentException (
        $"Transition to {ToString (to)} with exceptions {Tools.ToString (exceptions)} cannot contain {ToString (AnyState)}.");
    }

    var duplicates = _toExceptions.ContainsKey (to)
      ? _toExceptions[to].Intersect (exceptions).ToList()
      : ImmutableList <T>.Empty.ToList();

    foreach (var duplicate in duplicates)
    {
      _log.Warn ($"Not adding duplicate transition to {ToString (to)} except from {ToString (duplicate)}.");
    }

    var nonDuplicates = exceptions.Except (duplicates).ToList();

    if (!nonDuplicates.Any()) return;

    if (!_toExceptions.ContainsKey (to)) _toExceptions.Add (to, new HashSet <T>());

    foreach (var nonDuplicate in nonDuplicates)
    {
      _toExceptions[to].Add (nonDuplicate);
    }

    OnTransitionTo (to, action);
  }

  private void OnTransitionFrom (T from, TransitionAction action)
  {
    if (from.Equals (AnyState))
    {
      throw new ArgumentException ($"Transition from {ToString (from)} cannot contain {ToString (AnyState)}.");
    }

    AddWildcardableTransition (from, AnyState, action);
  }

  private void OnTransitionFromExceptTo (T from, ImmutableList <T> exceptions, TransitionAction action)
  {
    if (exceptions.Any (x => x.Equals (AnyState)))
    {
      throw new ArgumentException (
        $"Transition from {ToString (from)} with exceptions {Tools.ToString (exceptions)} cannot contain {ToString (AnyState)}.");
    }

    var duplicates = _fromExceptions.ContainsKey (from)
      ? _fromExceptions[from].Intersect (exceptions).ToList()
      : ImmutableList <T>.Empty.ToList();

    foreach (var duplicate in duplicates)
    {
      _log.Warn ($"Not adding duplicate transition from {ToString (from)} except to {ToString (duplicate)}.");
    }

    var nonDuplicates = exceptions.Except (duplicates).ToList();

    if (!nonDuplicates.Any()) return;

    if (!_fromExceptions.ContainsKey (from)) _fromExceptions.Add (from, new HashSet <T>());

    foreach (var nonDuplicate in nonDuplicates)
    {
      _fromExceptions[from].Add (nonDuplicate);
    }

    OnTransitionFrom (from, action);
  }

  private void OnTransition (T from, T to, TransitionAction action)
  {
    if (from.Equals (AnyState) || to.Equals (AnyState))
    {
      throw new ArgumentException ($"Transition from {ToString (from)} to {ToString (to)} cannot contain {ToString (AnyState)}.");
    }

    AddWildcardableTransition (from, to, action);
  }

  private void AddTrigger (T from, T to, TransitionTrigger trigger)
  {
    if (from.Equals (AnyState) || to.Equals (AnyState))
    {
      throw new ArgumentException ($"Trigger from {ToString (from)} to {ToString (to)} cannot contain {ToString (AnyState)}.");
    }

    AddWildcardableTrigger (from, to, trigger);
  }

  private bool HasTransitionAction (T from, T to) =>
    _transitionActions.TryGetValue (from, out var actions) && actions.ContainsKey (to) &&
    !(to.Equals (AnyState) && !from.Equals (AnyState) && _fromExceptions.ContainsKey (from)) &&
    !(from.Equals (AnyState) && !to.Equals (AnyState) && _toExceptions.ContainsKey (to));

  private Godot.Vector2? ExecuteTriggers (Godot.KinematicBody2D body = null, IsInputActive input = null,
    Godot.Vector2? velocity = null)
  {
    if (!_triggers.ContainsKey (_currentState) && !_triggers.ContainsKey (AnyState)) return velocity;

    _triggeredFromStates.Clear();
    _triggeredToStates.Clear();

    // @formatter:off
    GravityType? gravity = _frameActions.ContainsKey (_currentState) ? _frameActions[_currentState].Gravity : null;

    // Because frame actions, where gravity would be applied, always occur before transition triggers,
    // by this point (executing transition triggers), gravity has already been applied regardless.
    // Therefore the only valid gravity type for motion calculations is either None or AfterApplied.
    gravity = gravity == GravityType.BeforeApplied ? GravityType.AfterApplied : gravity;

    foreach (var from in Create (_currentState, AnyState).Where (x => _triggers.ContainsKey (x)))
    {
      foreach (var to in _triggers[from].Keys.Where (x => _triggers[from][x].CanTransition (from, x, body, input, velocity, gravity)))
      {
        _triggeredFromStates.Add (from);
        _triggeredToStates.Add (to);
      }
    }
    // @formatter:on

    if (_triggeredFromStates.Count == 0 || _triggeredToStates.Count == 0) return velocity;

    // ReSharper disable once InvertIf
    if (_triggeredFromStates.Count > 1 || _triggeredToStates.Count > 1)
    {
      _log.Warn ($"Ignoring multiple valid triggers from {ToString (_currentState)} to [{Tools.ToString (_triggeredToStates)}].");

      return velocity;
    }

    return TriggerState (_triggeredFromStates.Single(), _triggeredToStates.Single(), velocity);
  }

  private Godot.Vector2? ExecuteFrameAction (Godot.Vector2? velocity, float delta) =>
    !_frameActions.ContainsKey (_currentState) ? null : _frameActions[_currentState].Run (velocity, delta, _currentState);

  private void AddWildcardableTransition (T from, T to, TransitionAction action)
  {
    if (!HasWildcards (from, to) && !CanTransition (from, to))
    {
      _log.Warn ($"Not adding transition not found in transition table from {ToString (from)} to {ToString (to)}.");

      return;
    }

    if (_transitionActions.ContainsKey (from) && _transitionActions[from].ContainsKey (to))
    {
      _log.Warn ($"Not adding duplicate transition from {ToString (from)} to {ToString (to)}.");

      return;
    }

    if (!_transitionActions.ContainsKey (from)) _transitionActions.Add (from, new Dictionary <T, TransitionAction>());
    _transitionActions[from].Add (to, action);
  }

  private void AddWildcardableTrigger (T from, T to, TransitionTrigger trigger)
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

    if (!_triggers.ContainsKey (from)) _triggers.Add (from, new Dictionary <T, TransitionTrigger>());
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

  private Godot.Vector2? TriggerState (T from, T to, Godot.Vector2? velocity = null)
  {
    _log.Debug ($"Executing {(from.Equals (AnyState) ? "wildcard" : "specific")} " +
                $"trigger {ToString (_currentState)} {(from.Equals (AnyState) ? "(any)" : "(specific)")} " +
                $"=> {ToString (to)} (specific).");

    return CanPopTo (to) ? Pop (velocity) : IsReversible (to) ? Push (to, velocity) : To (to, velocity);
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

  private Godot.Vector2? ExecuteChangeState (T to, Godot.Vector2? velocity)
  {
    velocity = ExecuteTransitionActionsTo (to, velocity);
    _log.Info ($"State transition: {ToString (_currentState)} => {ToString (to)}");
    _currentState = to;

    return velocity;
  }

  private Godot.Vector2? ExecuteTransitionActionsTo (T to, Godot.Vector2? velocity1) =>
    Create (_currentState, AnyState).Aggregate (velocity1,
      (velocity2, fromState) => Create (to, AnyState).Where (toState => HasTransitionAction (fromState, toState)).Aggregate (velocity2,
        (velocity3, toState) => _transitionActions[fromState][toState].Run (fromState, toState, velocity3)));
}