using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using static Gravity;
using static Motions;
using static Positionings;

public interface IStateMachine <T> where T : Enum
{
  public enum ResetOption
  {
    IgnoreTransitionActions,
    ExecuteTransitionActions
  }

  // @formatter:off
  public delegate Godot.Vector2? VelocityTransitionAction();
  public delegate void NonVelocityTransitionAction ();
  public delegate Godot.Vector2 VelocityFrameAction (Godot.Vector2 velocity, float delta);
  public delegate void NonVelocityFrameAction (float delta);
  public T GetState();
  public bool Is (T state);
  public void OnTransitionTo (T to, NonVelocityTransitionAction nonVelocityAction = null, VelocityTransitionAction velocityAction = null);
  public void OnTransitionToExceptFrom (T to, T exception, NonVelocityTransitionAction nonVelocityAction = null, VelocityTransitionAction velocityAction = null);
  public void OnTransitionToExceptFrom (T to, ImmutableList <T> exceptions, NonVelocityTransitionAction nonVelocityAction = null, VelocityTransitionAction velocityAction = null);
  public void OnTransitionFrom (T from, NonVelocityTransitionAction nonVelocityAction = null, VelocityTransitionAction velocityAction = null);
  public void OnTransitionFromExceptTo (T from, T exception, NonVelocityTransitionAction nonVelocityAction = null, VelocityTransitionAction velocityAction = null);
  public void OnTransitionFromExceptTo (T from, ImmutableList <T> exceptions, NonVelocityTransitionAction nonVelocityAction = null, VelocityTransitionAction velocityAction = null);
  public void OnTransition (T from, T to, NonVelocityTransitionAction nonVelocityAction = null, VelocityTransitionAction velocityAction = null);
  public void AddFrameAction (T state, GravityType gravity = GravityType.AfterApplied, NonVelocityFrameAction nonVelocityAction = null, VelocityFrameAction velocityAction = null);
  public Godot.Vector2? Update (Godot.KinematicBody2D body = null, Func <string, bool> inputFunc = null, Godot.Vector2? velocity = null, float delta = 0.0f);
  public Godot.Vector2? To (T to, Godot.Vector2? velocity = null); public Godot.Vector2? ToIf (T to, bool condition, Godot.Vector2? velocity = null);
  public Godot.Vector2? Push (T to, Godot.Vector2? velocity = null);
  public Godot.Vector2? PushIf (T to, bool condition, Godot.Vector2? velocity = null);
  public Godot.Vector2? Pop (Godot.Vector2? velocity = null);
  public Godot.Vector2? PopIf (bool condition, Godot.Vector2? velocity = null);
  public Godot.Vector2? PopIf (T state, Godot.Vector2? velocity = null);
  public Godot.Vector2? PopIf (T state, bool condition, Godot.Vector2? velocity = null);
  public void Reset (ResetOption resetOption = ResetOption.IgnoreTransitionActions);
  public void SetLogLevel (Log.Level level);
  // @formatter:on

  public void AddTrigger (T from, T to, Inputs.Input? input = null, IEnumerable <IInputWrapper> inputs = null, Motion? motion = null,
    IEnumerable <IMotionWrapper> motions = null, Positioning? positioning = null, Func <bool> condition = null, Func <bool> and = null,
    Func <bool> or = null, IBooleanOperator[] conditions = null);

  public void AddTriggerTo (T to, Inputs.Input? input = null, IEnumerable <IInputWrapper> inputs = null, Motion? motion = null,
    IEnumerable <IMotionWrapper> motions = null, Positioning? positioning = null, Func <bool> condition = null, Func <bool> and = null,
    Func <bool> or = null, IBooleanOperator[] conditions = null);
}