using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using WAT;

[Pre (nameof (RunBeforeTestMethod))]
// ReSharper disable UnusedMember.Local
// ReSharper disable ObjectCreationAsStatement
public class StateMachineTest : Test
{
  private enum State
  {
    State1,
    State2,
    State3,
    State4,
    State5,
    State6,
    State7
  }

  private enum StatesWithWildcard
  {
    Wildcard = -1,
    Valid
  }

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.State1, new[] { State.State2, State.State3, State.State4, State.State6, State.State7 } },
    { State.State2, new[] { State.State3, State.State5 } },
    { State.State3, new[] { State.State1 } },
    { State.State4, new[] { State.State1, State.State5 } },
    { State.State5, new[] { State.State1, State.State2, State.State3, State.State6 } },
    { State.State6, new[] { State.State1, State.State5 } },
    { State.State7, new[] { State.State1, State.State2 } }
  };

  // @formatter:off
  private static readonly State AnyState = (State)(object)-1;
  private StateMachine <State> _sm;
  private const State InitialState = State.State5;

  public void RunBeforeTestMethod() => _sm = new StateMachine <State> (TransitionTable, InitialState);

  // @formatter:on

  [Test]
  public void TestInitialState()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestGetState()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    Assert.IsEqual (_sm.GetState(), InitialState);
  }

  [Test]
  public void TestGetStateAfterStateChange()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    const State state = State.State1;
    _sm.To (state);
    Assert.IsEqual (_sm.GetState(), state);
  }

  [Test]
  public void TestToInvalidState()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.To (State.State4);
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestPush()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    Assert.IsTrue (_sm.Is (State.State7));
  }

  [Test]
  public void TestInvalidPushOneWayTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State1);
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestPop()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.Pop();
    _sm.Pop();
    _sm.Pop();
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestInvalidPopWasNotPushed()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    const State state = State.State1;
    _sm.To (state);
    _sm.Pop();
    Assert.IsTrue (_sm.Is (state));
  }

  [Test]
  public void TestToSameStateInvalid()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    const State state = State.State1;
    _sm.LogLevel = Log.Level.All;
    _sm.To (state);
    Assert.DoesNotThrow (() => _sm.To (state));
  }

  [Test]
  public void TestToIfFalse()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.ToIf (State.State1, false);
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestToIfTrue()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    const State state = State.State1;
    _sm.ToIf (state, true);
    Assert.IsTrue (_sm.Is (state));
  }

  [Test]
  public void TestPushIfTrue()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.PushIf (State.State6, true);
    _sm.PushIf (State.State1, true);
    _sm.PushIf (State.State7, true);
    Assert.IsTrue (_sm.Is (State.State7));
  }

  [Test]
  public void TestPushIfFalse()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.PushIf (State.State6, false);
    _sm.PushIf (State.State1, false);
    _sm.PushIf (State.State7, false);
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestPopIfIsStateTrue()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.PopIf (State.State7);
    _sm.PopIf (State.State1);
    _sm.PopIf (State.State6);
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestPopIfIsStateFalse()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.PopIf (State.State1);
    _sm.PopIf (State.State6);
    Assert.IsTrue (_sm.Is (State.State7));
  }

  [Test]
  public void TestPopIfConditionTrue()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.PopIf (true);
    _sm.PopIf (true);
    _sm.PopIf (true);
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestPopIfConditionFalse()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.PopIf (false);
    _sm.PopIf (false);
    _sm.PopIf (false);
    Assert.IsTrue (_sm.Is (State.State7));
  }

  [Test]
  public void TestPopIfStateTrueConditionTrue()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.PopIf (State.State7, true);
    _sm.PopIf (State.State1, true);
    _sm.PopIf (State.State6, true);
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestPopIfStateTrueConditionFalse()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.PopIf (State.State7, false);
    _sm.PopIf (State.State7, false);
    _sm.PopIf (State.State7, false);
    Assert.IsTrue (_sm.Is (State.State7));
  }

  [Test]
  public void TestPopIfStateFalseConditionTrue()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.PopIf (State.State1, true);
    _sm.PopIf (State.State6, true);
    _sm.PopIf (InitialState, true);
    Assert.IsTrue (_sm.Is (State.State7));
  }

  [Test]
  public void TestPopIfStateFalseConditionFalse()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.PopIf (State.State6, false);
    _sm.PopIf (State.State1, false);
    _sm.PopIf (InitialState, false);
    Assert.IsTrue (_sm.Is (State.State7));
  }

  [Test]
  public void TestTransitionTableWithKeyWildcardStateThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        new StateMachine <State> (new Dictionary <State, State[]> { { AnyState, new[] { InitialState } } }, InitialState);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestTransitionTableWithValueWildcardStateThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        new StateMachine <State> (
          new Dictionary <State, State[]> { { InitialState, new[] { AnyState, State.State1, State.State2, State.State3 } } },
          InitialState);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestInitialWildcardStateThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        new StateMachine <State> (new Dictionary <State, State[]>(), AnyState);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestInitialStateNotTransitionTableKeyThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        new StateMachine <State> (new Dictionary <State, State[]>(), InitialState);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestStatesEnumContainWildcardThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        new StateMachine <StatesWithWildcard> (new Dictionary <StatesWithWildcard, StatesWithWildcard[]>(), StatesWithWildcard.Valid);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestOnTransitionToWithPush()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionTo (State.State6, () => { ++counter; });
    _sm.OnTransitionTo (State.State1, () => { ++counter; });
    _sm.OnTransitionTo (State.State7, () => { ++counter; });
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    Assert.IsEqual (counter, 3);
  }

  [Test]
  public void TestOnTransitionToWithPop()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionTo (InitialState, () => { ++counter; });
    _sm.OnTransitionTo (State.State6, () => { ++counter; });
    _sm.OnTransitionTo (State.State1, () => { ++counter; });
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.Pop();
    _sm.Pop();
    _sm.Pop();
    Assert.IsEqual (counter, 5);
  }

  [Test]
  public void TestOnTransitionToWithTo()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionTo (InitialState, () => { ++counter; });
    _sm.OnTransitionTo (State.State6, () => { ++counter; });
    _sm.OnTransitionTo (State.State1, () => { ++counter; });
    _sm.To (State.State6);
    _sm.To (State.State1);
    _sm.To (State.State7);
    Assert.IsEqual (counter, 2);
  }

  [Test]
  public void TestDuplicateOnTransitionToNotAdded()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionTo (State.State6, () => { ++counter; });
    _sm.OnTransitionTo (State.State6, () => { ++counter; });
    _sm.To (State.State6);
    Assert.IsEqual (counter, 1);
  }

  [Test]
  public void TestOnTransitionToNotTriggeredForInitialState()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var flag = false;
    _sm.OnTransitionTo (InitialState, () => { flag = true; });
    Assert.IsFalse (flag);
  }

  [Test]
  public void TestValidOnTransitionToWithInvalidTo()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.OnTransitionTo (State.State7, () => { ++counter; });
    _sm.To (State.State7);
    Assert.IsEqual (counter, 0);
  }

  [Test]
  public void TestOnTransitionFromWithPush()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionFrom (InitialState, () => { ++counter; });
    _sm.OnTransitionFrom (State.State1, () => { ++counter; });
    _sm.OnTransitionFrom (State.State7, () => { ++counter; });
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    Assert.IsEqual (counter, 2);
  }

  [Test]
  public void TestOnTransitionFromWithPop()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionFrom (InitialState, () => { ++counter; });
    _sm.OnTransitionFrom (State.State1, () => { ++counter; });
    _sm.OnTransitionFrom (State.State6, () => { ++counter; });
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.Pop();
    _sm.Pop();
    _sm.Pop();
    Assert.IsEqual (counter, 5);
  }

  [Test]
  public void TestOnTransitionFromWithTo()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionFrom (InitialState, () => { ++counter; });
    _sm.OnTransitionFrom (State.State1, () => { ++counter; });
    _sm.OnTransitionFrom (State.State6, () => { ++counter; });
    _sm.To (State.State6);
    _sm.To (State.State1);
    _sm.To (State.State7);
    Assert.IsEqual (counter, 3);
  }

  [Test]
  public void TestValidOnTransitionFromWithInvalidTo()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.OnTransitionFrom (InitialState, () => { ++counter; });
    _sm.To (State.State7);
    Assert.IsEqual (counter, 0);
  }

  [Test]
  public void TestOnTransitionWithPush()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransition (InitialState, State.State6, () => { ++counter; });
    _sm.OnTransition (State.State1, State.State7, () => { ++counter; });
    _sm.OnTransition (State.State6, InitialState, () => { ++counter; });
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    Assert.IsEqual (counter, 2);
  }

  [Test]
  public void TestOnTransitionWithPop()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransition (InitialState, State.State6, () => { ++counter; });
    _sm.OnTransition (State.State1, State.State7, () => { ++counter; });
    _sm.OnTransition (State.State6, InitialState, () => { ++counter; });
    _sm.Push (State.State6);
    _sm.Push (State.State1);
    _sm.Push (State.State7);
    _sm.Pop();
    _sm.Pop();
    _sm.Pop();
    Assert.IsEqual (counter, 3);
  }

  [Test]
  public void TestOnTransitionWithTo()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransition (InitialState, State.State6, () => { ++counter; });
    _sm.OnTransition (State.State1, State.State7, () => { ++counter; });
    _sm.OnTransition (State.State6, InitialState, () => { ++counter; });
    _sm.To (State.State6);
    _sm.To (State.State1);
    _sm.To (State.State7);
    Assert.IsEqual (counter, 2);
  }

  [Test]
  public void TestOnTransitionInvalid()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    _sm.OnTransition (InitialState, State.State7, () => { ++counter; });
    _sm.To (State.State7);
    Assert.IsEqual (counter, 0);
  }

  [Test]
  public void TestAddWildcardFromTransitionThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        _sm.OnTransition (AnyState, State.State6, () => { });
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestAddWildcardToTransitionThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        _sm.OnTransition (State.State6, AnyState, () => { });
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestAddWildcardToAndFromTransitionThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        _sm.OnTransition (AnyState, State.State6, () => { });
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestTrueTriggerInAddTriggerCausesTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    const State state = State.State6;
    _sm.AddTrigger (InitialState, state, trigger);
    trigger();
    _sm.Update();
    Assert.IsTrue (_sm.Is (state));
  }

  [Test]
  public void TestFalseTriggerInAddTriggerDoesNotCauseTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var trigger = new IStateMachine <State>.TransitionTrigger (() => false);
    const State state = State.State6;
    _sm.AddTrigger (InitialState, state, trigger);
    trigger();
    _sm.Update();
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestTrueInvalidTriggerInAddTriggerDoesNotCauseTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    const State state = State.State7;
    _sm.AddTrigger (InitialState, state, trigger);
    trigger();
    _sm.Update();
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestTrueTriggerInAddTriggerToCausesTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    const State state = State.State6;
    _sm.LogLevel = Log.Level.All;
    _sm.AddTriggerTo (state, trigger);
    trigger();
    _sm.Update();
    Assert.IsTrue (_sm.Is (state));
  }

  [Test]
  public void TestFalseTriggerInAddTriggerToDoesNotCauseTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var trigger = new IStateMachine <State>.TransitionTrigger (() => false);
    const State state = State.State6;
    _sm.AddTriggerTo (state, trigger);
    trigger();
    _sm.Update();
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestTrueInvalidTriggerInAddTriggerToDoesNotCauseTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    const State state = State.State7;
    _sm.AddTriggerTo (state, trigger);
    trigger();
    _sm.Update();
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestFromWildcardInAddTriggerThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        _sm.AddTrigger (AnyState, State.State6, () => true);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestWildcardInAddTriggerToThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        _sm.AddTriggerTo (AnyState, () => true);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestToWildcardInAddTriggerThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        _sm.AddTrigger (InitialState, AnyState, () => true);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestToFromWildcardsInAddTriggerThrows()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    Assert.Throws (() =>
    {
      try
      {
        _sm.AddTrigger (AnyState, AnyState, () => true);
      }
      catch (Exception e)
      {
        GD.Print (e);

        throw;
      }
    });
  }

  [Test]
  public void TestDuplicateTriggerDoesNotCauseDuplicateTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    var trigger2 = new IStateMachine <State>.TransitionTrigger (() => true);
    const State state = State.State6;
    _sm.OnTransition (InitialState, state, () => { ++counter; });
    _sm.AddTrigger (InitialState, state, trigger);
    _sm.AddTrigger (InitialState, state, trigger2);
    trigger();
    trigger2();
    _sm.Update();
    Assert.IsEqual (counter, 1);
  }

  [Test]
  public void TestMultipleValidTriggersDoNotCauseTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    var trigger2 = new IStateMachine <State>.TransitionTrigger (() => true);
    const State stateA = State.State6;
    const State stateB = State.State3;
    _sm.OnTransition (InitialState, stateA, () => { ++counter; });
    _sm.OnTransition (InitialState, stateB, () => { ++counter; });
    _sm.AddTrigger (InitialState, stateA, trigger);
    _sm.AddTrigger (InitialState, stateB, trigger2);
    trigger();
    trigger2();
    _sm.Update();
    Assert.IsEqual (counter, 0);
  }

  [Test]
  public void TestMultipleSequentialTriggersCauseMultipleTransitions()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    _sm.LogLevel = Log.Level.All;
    _sm.AddTrigger (InitialState, State.State1, trigger);
    _sm.AddTrigger (State.State1, State.State2, trigger);
    _sm.AddTrigger (State.State2, State.State3, trigger);
    trigger();
    _sm.Update();
    trigger();
    _sm.Update();
    trigger();
    _sm.Update();
    Assert.IsTrue (_sm.Is (State.State3));
  }

  [Test]
  public void TestMultipleSequentialTriggersCauseMultipleTransitionActions()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    var action = new IStateMachine <State>.TransitionAction (() => ++counter);
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransition (InitialState, State.State1, action);
    _sm.OnTransitionTo (State.State1, action);
    _sm.OnTransitionFrom (State.State2, action);
    _sm.AddTrigger (InitialState, State.State1, trigger);
    _sm.AddTrigger (State.State1, State.State2, trigger);
    _sm.AddTrigger (State.State2, State.State3, trigger);
    trigger();
    _sm.Update();
    trigger();
    _sm.Update();
    trigger();
    _sm.Update();
    Assert.IsEqual (counter, 3);
  }

  [Test]
  public void TestDifferentFromSameToTriggersCauseTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    _sm.LogLevel = Log.Level.All;
    _sm.AddTrigger (InitialState, State.State1, trigger);
    _sm.AddTrigger (State.State3, State.State1, trigger);
    _sm.AddTrigger (State.State4, State.State1, trigger);
    trigger();
    _sm.Update();
    _sm.To (State.State3);
    trigger();
    _sm.Update();
    Assert.IsTrue (_sm.Is (State.State1));
  }

  [Test]
  public void TestMultipleTransitionActionsExecutedForSameTransition()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    var action = new IStateMachine <State>.TransitionAction (() => ++counter);
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionTo (State.State1, action);
    _sm.OnTransitionFrom (InitialState, action);
    _sm.OnTransition (InitialState, State.State1, action);
    _sm.To (State.State1);
    Assert.IsEqual (counter, 3);
  }

  [Test]
  public void TestResetGoesToInitialStateEvenIfTransitionIsInvalid()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    _sm.To (State.State1);
    _sm.To (State.State7);
    _sm.To (State.State2);
    _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    Assert.IsTrue (_sm.Is (InitialState));
  }

  [Test]
  public void TestResetToSameStateDoesNotThrow()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    Assert.DoesNotThrow (() => _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions));
    Assert.DoesNotThrow (() => _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions));
    Assert.DoesNotThrow (() => _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions));
  }

  [Test]
  public void TestResetWithInvalidTransitionDoesNotExecuteTransitionActions()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    var action = new IStateMachine <State>.TransitionAction (() => ++counter);
    var action2 = new IStateMachine <State>.TransitionAction (() => ++counter);
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionFrom (InitialState, action);
    _sm.OnTransitionFrom (State.State1, action);
    _sm.OnTransitionTo (InitialState, action2);
    _sm.AddTrigger (InitialState, State.State1, trigger);
    trigger();
    _sm.Update();
    _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    Assert.IsEqual (counter, 1);
  }

  [Test]
  public void TestResetWithValidTransitionExecutesTransitionActionsWhenExecuteOptionSet()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    var action = new IStateMachine <State>.TransitionAction (() => ++counter);
    var action2 = new IStateMachine <State>.TransitionAction (() => ++counter);
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionFrom (InitialState, action);
    _sm.OnTransitionFrom (State.State1, action);
    _sm.OnTransitionTo (InitialState, action2);
    _sm.AddTrigger (InitialState, State.State1, trigger);
    trigger();
    _sm.Update();
    _sm.To (State.State6);
    _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    Assert.IsEqual (counter, 3);
  }

  [Test]
  public void TestResetWithValidTransitionIgnoresTransitionActionsWhenIgnoreOptionSet()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;
    var action = new IStateMachine <State>.TransitionAction (() => ++counter);
    var action2 = new IStateMachine <State>.TransitionAction (() => ++counter);
    var trigger = new IStateMachine <State>.TransitionTrigger (() => true);
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionFrom (InitialState, action);
    _sm.OnTransitionFrom (State.State1, action);
    _sm.OnTransitionTo (InitialState, action2);
    _sm.AddTrigger (InitialState, State.State1, trigger);
    trigger();
    _sm.Update();
    _sm.To (State.State6);
    _sm.Reset (IStateMachine <State>.ResetOption.IgnoreTransitionActions);
    Assert.IsEqual (counter, 2);
  }

  [Test]
  public void IntegrationTest()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var counter = 0;

    var action = new IStateMachine <State>.TransitionAction (() =>
    {
      ++counter;
      GD.Print ($"Incremented counter from {counter - 1} to {counter}.");
    });

    var action2 = new IStateMachine <State>.TransitionAction (() =>
    {
      --counter;
      GD.Print ($"Decremented counter from {counter + 1} to {counter}.");
    });

    var trigger1 = new IStateMachine <State>.TransitionTrigger (() => true);
    var trigger2 = new IStateMachine <State>.TransitionTrigger (() => false);
    _sm.LogLevel = Log.Level.All;
    _sm.OnTransitionTo (State.State1, action);
    _sm.OnTransitionFrom (InitialState, action);
    _sm.OnTransition (InitialState, State.State1, action);
    _sm.OnTransition (InitialState, State.State6, action2);
    _sm.AddTrigger (InitialState, State.State1, trigger1);
    _sm.AddTrigger (State.State3, State.State1, trigger1);
    _sm.AddTrigger (State.State4, State.State1, trigger1);
    _sm.AddTrigger (State.State1, State.State7, trigger2);
    trigger1(); // counter = 3
    trigger1();
    trigger2();
    _sm.Update();
    trigger2();
    _sm.Update();
    trigger2();
    trigger1();
    trigger1();
    _sm.Update();
    trigger2();
    _sm.Update();
    _sm.Update();
    _sm.Update();
    _sm.To (State.State1);
    _sm.To (State.State1);
    _sm.To (State.State1);
    _sm.To (InitialState);
    _sm.To (InitialState);
    _sm.To (State.State2);
    _sm.Update();
    _sm.Pop();
    _sm.Pop();
    _sm.Update();
    _sm.Pop();
    _sm.Push (InitialState);
    _sm.Push (State.State6); // counter = 3 - 1 + 1 = 3
    _sm.Update();
    _sm.Pop();
    _sm.Pop(); // counter = 3 + 1 = 4, State2
    _sm.Update();
    _sm.Update();
    _sm.Update();
    _sm.To (State.State3);
    _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    _sm.Update(); // counter = 4 + 3 = 7
    _sm.To (State.State6); // counter = 7 - 1 + 1 = 7
    _sm.Update();
    _sm.Update();
    _sm.To (State.State7);
    _sm.To (State.State7);
    _sm.Update();
    _sm.To (State.State7);
    _sm.Pop();
    _sm.Push (State.State1); // counter = 7 + 1 = 8
    _sm.Update();
    _sm.Update();
    _sm.Update();
    _sm.Push (State.State3);
    _sm.Update(); // counter = 8 + 1 = 9, State1
    _sm.Pop(); // State6
    _sm.Pop();
    _sm.Update();
    _sm.Update();
    _sm.Update();
    _sm.Update();
    Assert.IsEqual (counter, 9);
    Assert.IsTrue (_sm.Is (State.State6));
  }
}