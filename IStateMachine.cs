using System;

public interface IStateMachine <T> where T : Enum
{
  public enum ResetOption
  {
    IgnoreTransitionActions,
    ExecuteTransitionActions
  }

  public delegate void TransitionAction();
  public delegate bool TransitionTrigger();
  public bool Is (T state);
  public void OnTransitionTo (T to, TransitionAction action);
  public void OnTransitionFrom (T from, TransitionAction action);
  public void OnTransition (T from, T to, TransitionAction action);
  public void AddTrigger (T from, T to, TransitionTrigger trigger);
  public void Update();
  public void To (T to);
  public void ToIf (T to, bool condition);
  public void Push (T to);
  public void PushIf (T to, bool condition);
  public void Pop();
  public void PopIf (bool condition);
  public void PopIf (T state);
  public void PopIf (T state, bool condition);
  public void Reset (ResetOption resetOption = ResetOption.IgnoreTransitionActions);
}