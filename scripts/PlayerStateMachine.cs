using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static PlayerStateMachine;

// Climbing terminology:
//   Cliff arresting: Using pickaxe to slow a cliff fall
//   Cliffhanging: Attached to a cliff above the ground, not moving
//   Traversing: Climbing horizontally
//   Free falling: Moving down, without cliff arresting, can also be coming down from a jump.
public class PlayerStateMachine : StateMachine <State>
{
  public enum State
  {
    Idle,
    ReadingSign,
    OpeningDoor,
    Walking,
    Running,
    Jumping,
    AscendingStairs,
    DescendingStairs,
    ClimbingPrep,
    ClimbingUp,
    ClimbingDown,
    CliffHanging,
    Traversing,
    CliffArresting,
    FreeFalling,
  }

  // @formatter:off

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.Idle, new[] { State.Walking, State.Running, State.Jumping, State.ClimbingPrep, State.ClimbingUp, State.ClimbingDown, State.FreeFalling, State.ReadingSign, State.OpeningDoor, State.AscendingStairs, State.DescendingStairs }},
    { State.Walking, new[] { State.Idle, State.Running, State.Jumping, State.FreeFalling, State.ClimbingPrep, State.ReadingSign, State.OpeningDoor, State.AscendingStairs, State.DescendingStairs }},
    { State.Running, new[] { State.Idle, State.Walking, State.Jumping, State.FreeFalling, State.ClimbingPrep, State.ReadingSign, State.OpeningDoor, State.AscendingStairs, State.DescendingStairs }},
    { State.Jumping, new[] { State.Idle, State.FreeFalling, State.Walking, State.AscendingStairs, State.DescendingStairs }},
    { State.AscendingStairs, new[] { State.Idle, State.Walking, State.Running, State.Jumping, State.OpeningDoor, State.DescendingStairs, State.FreeFalling }},
    { State.DescendingStairs, new[] { State.Idle, State.Walking, State.Running, State.Jumping, State.OpeningDoor, State.AscendingStairs, State.FreeFalling }},
    { State.ClimbingPrep, new[] { State.ClimbingUp, State.Idle, State.Walking, State.Jumping }},
    { State.ClimbingUp, new[] { State.ClimbingDown, State.CliffHanging, State.FreeFalling, State.Idle }},
    { State.ClimbingDown, new[] { State.ClimbingUp, State.CliffHanging, State.FreeFalling, State.Idle }},
    { State.CliffHanging, new[] { State.ClimbingUp, State.ClimbingDown, State.Traversing, State.FreeFalling }},
    { State.Traversing, new[] { State.CliffHanging, State.FreeFalling }},
    { State.CliffArresting, new[] { State.CliffHanging, State.FreeFalling, State.Idle }},
    { State.FreeFalling, new[] { State.CliffArresting, State.CliffHanging, State.Idle, State.Walking, State.Running, State.Jumping, State.AscendingStairs, State.DescendingStairs }},
    { State.ReadingSign, new[] { State.Idle, State.Walking, State.Running }},
    { State.OpeningDoor, new[] { State.Idle, State.Walking, State.Running, State.AscendingStairs, State.DescendingStairs }}
  };

  // ReSharper disable once ExplicitCallerInfoArgument
  public PlayerStateMachine (State initialState, Log.Level logLevel, [CallerFilePath] string name = "") : base (TransitionTable,
    initialState, name) => LogLevel = logLevel;

  // @formatter:on
}