using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Godot;
using WAT;
using static Gravity;
using static Motions;
using static Positionings;

public class MotionsTest : Test
{
  private static readonly IEnumerable <Motion> Values = Enum.GetValues (typeof (Motion)).Cast <Motion>();
  private static readonly Func <Motion, PhysicsBodyData, bool> ActiveFunc = (motion, data) => motion.IsActive (ref data);

  [Test]
  public void TestMotionNoneIsActiveWithOnlyGravity()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var physicsBodyData = new PhysicsBodyData (GravityForce, GravityType.AfterApplied, Positioning.Ground);
    Assert.IsTrue (Motion.None.IsActive (ref physicsBodyData));
  }

  [Test]
  public void TestNothingOtherThanRequiredMotionNoneIsActiveWithOnlyGravity()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var physicsBodyData = new PhysicsBodyData (GravityForce, GravityType.AfterApplied, Positioning.Ground);
    Assert.IsTrue (Required (Motion.None).Compose (ref physicsBodyData, ActiveFunc));
  }

  [Test]
  public void TestNothingOtherThanWrappedRequiredMotionNoneIsActiveWithOnlyGravity()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var physicsBodyData = new PhysicsBodyData (GravityForce, GravityType.AfterApplied, Positioning.Ground);
    // @formatter:off
    Assert.IsTrue (new CompositeMotionWrapper (_ (Required (Motion.None))).Compose (ref physicsBodyData, ActiveFunc));
    // @formatter:on
  }

  [Test]
  public void TestRequiredAnyUpHorizontal()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var physicsBodyData = new PhysicsBodyData (new Vector2 (-2, -2), GravityType.AfterApplied, Positioning.Air);

    TestMotion (Required, new List <Motion> { Motion.Any, Motion.Up, Motion.Horizontal }, ref physicsBodyData, ActiveFunc, true,
      new List <Motion> { Motion.Any, Motion.Up, Motion.Horizontal, Motion.Vertical }, new List <Motion> { Motion.None, Motion.Down },
      "required");
  }

  [Test]
  public void TestOptionalRightUp()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var physicsBodyData = new PhysicsBodyData (new Vector2 (-2, -2), GravityType.AfterApplied, Positioning.Air);

    TestMotion (Optional, new List <Motion> { Motion.Right, Motion.Up }, ref physicsBodyData, ActiveFunc, false,
      new List <Motion> { Motion.Right, Motion.Up, Motion.None }, new List <Motion> { Motion.Left, Motion.Down }, "optional");
  }

  [Test]
  public void TestOptionalAny()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var physicsBodyData = new PhysicsBodyData (new Vector2 (-2, -2), GravityType.AfterApplied, Positioning.Air);

    // @formatter:off
    TestMotion (Optional, new List <Motion> { Motion.Any }, ref physicsBodyData, ActiveFunc, true,
      new List <Motion> { Motion.Any, Motion.None, Motion.Up, Motion.Down, Motion.Left, Motion.Right }, new List <Motion>(),
      "optional");
    // @formatter:on
  }

  [Test]
  public void TestOptionalAnyNone()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");
    var physicsBodyData = new PhysicsBodyData (new Vector2 (-2, -2), GravityType.AfterApplied, Positioning.Air);

    // @formatter:off
    TestMotion (Optional, new List <Motion> { Motion.Any, Motion.None }, ref physicsBodyData, ActiveFunc, true,
      new List <Motion> { Motion.Any, Motion.None, Motion.Up, Motion.Down, Motion.Left, Motion.Right }, new List <Motion>(),
      "optional");
    // @formatter:on
  }

  [Test]
  public void TestOptionalAnyRightUp()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod().Name}:\n---");

    // TestAll 23612: Optional (Any, Right, Up), velocity: (0, 0), allowed: (None, Any, Up, Down, Left, Right), disallowed: (), expected: True, actual: False, result: FAILED
    var physicsBodyData = new PhysicsBodyData (Vector2.Zero, GravityType.AfterApplied, Positioning.Air);

    // @formatter:off
    TestMotion (Optional, new List <Motion> { Motion.Any, Motion.Right, Motion.Up }, ref physicsBodyData, ActiveFunc, true,
      new List <Motion> { Motion.Any, Motion.None, Motion.Up, Motion.Down, Motion.Left, Motion.Right }, new List <Motion>(),
      "optional");
    // @formatter:on
  }

  // Last updated 3/28/2022: Ran 110,592 tests. 110,592 passed, 0 failed. True: 35,809, False: 74,783
  [Test]
  public void TestAll()
  {
    var methodName = MethodBase.GetCurrentMethod().Name;
    GD.Print ($"---\n{methodName}:\n---");
    var velocityComponents = ImmutableList.Create (-2, 0, 2);
    var velocity = Vector2.Zero;
    var motions = new HashSet <Motion>();
    var physicsBodyData = new PhysicsBodyData (velocity, GravityType.AfterApplied, Positioning.Air);
    var requiredMotions = new HashSet <Motion>();
    var optionalMotions = new HashSet <Motion>();
    var tests = 0;
    var passed = 0;
    var failed = 0;
    var @true = 0;
    var @false = 0;

    foreach (var motion1 in Values)
    {
      foreach (var motion2 in Values)
      {
        foreach (var motion3 in Values)
        {
          foreach (var motion4 in Values)
          {
            foreach (var velocityX in velocityComponents)
            {
              foreach (var velocityY in velocityComponents)
              {
                motions.Clear();
                requiredMotions.Clear();
                optionalMotions.Clear();
                motions.Add (motion1);
                motions.Add (motion2);
                motions.Add (motion3);
                motions.Add (motion4);
                requiredMotions.Add (motion1);
                requiredMotions.Add (motion2);
                optionalMotions.Add (motion3);
                optionalMotions.Add (motion4);
                velocity.x = velocityX;
                velocity.y = velocityY;

                // @formatter:off

                var requiredWrapper = new CompositeMotionWrapper (_ (Required (motions.ToArray())));
                var requiredExpected = motions.All (x => ActiveFunc.Invoke (x, physicsBodyData)) && requiredWrapper.Disallowed().All (x => !ActiveFunc.Invoke (x, physicsBodyData));
                var requiredActual = requiredWrapper.Compose (ref physicsBodyData, ActiveFunc);

                var optionalExpectedWrapper = new CompositeMotionWrapper (_ (Optional (motions.ToArray())));
                var optionalExpectedResult = motions.Any (x => velocity.Equals (Vector2.Zero) || ActiveFunc.Invoke (x, physicsBodyData)) && optionalExpectedWrapper.Disallowed().All (x => !ActiveFunc.Invoke (x, physicsBodyData));
                var optionalActualWrapper = new CompositeMotionWrapper (_ (Optional (motions.ToArray())));
                var optionalActualResult = optionalActualWrapper.Compose (ref physicsBodyData, ActiveFunc);

                var mixedWrapper = new CompositeMotionWrapper (_ (Required (requiredMotions.ToArray()), Optional (optionalMotions.ToArray())));
                var mixedExpected = requiredMotions.All (x => ActiveFunc.Invoke (x, physicsBodyData)) && mixedWrapper.Disallowed().All (x => !ActiveFunc.Invoke (x, physicsBodyData));
                var mixedActual = mixedWrapper.Compose (ref physicsBodyData, ActiveFunc);

                ++tests;
                if (requiredExpected == requiredActual) ++passed;
                if (requiredExpected!= requiredActual) ++failed;
                if (requiredActual) ++@true;
                if (!requiredActual) ++@false;

                // Assert.IsEqual (requiredExpected, requiredActual,
                //   $"{methodName} {tests}: Required ({Tools.ToString (motions)}), velocity: {velocity}, " +
                //   $"disallowed: ({Tools.ToString (requiredWrapper.Disallowed())}), expected: {requiredExpected}, " +
                //   $"actual: {requiredActual}, result: FAILED");

                // if (tests == 18847)
                // {
                //   GD.Print ( $"{methodName} {tests}: Required ({Tools.ToString (motions)}), velocity: {velocity}, " +
                //     $"allowed: ({Tools.ToString (requiredWrapper.Allowed())}), disallowed: ({Tools.ToString (requiredWrapper.Disallowed())}), expected: {requiredExpected}, " +
                //     $"actual: {requiredActual}, result: {(requiredExpected == requiredActual ? "PASSED" : "FAILED")}");
                // }

                ++tests;
                if (optionalExpectedResult == optionalActualResult) ++passed;
                if (optionalExpectedResult!= optionalActualResult) ++failed;
                if (optionalActualResult) ++@true;
                if (!optionalActualResult) ++@false;

                Assert.IsEqual (optionalExpectedResult, optionalActualResult,
                  $"{methodName} {tests}: Optional ({Tools.ToString (motions)}), velocity: {velocity}, " +
                  $"disallowed: ({Tools.ToString (optionalExpectedWrapper.Disallowed())}), " +
                  $"expected: {optionalExpectedResult}, actual: {optionalActualResult}, result: FAILED");

                // if (tests is >= 23600 and <= 23624)
                // {
                //   GD.Print (
                //     $"{methodName} {tests}: Optional ({Tools.ToString (motions)}), velocity: {velocity}, " +
                //     $"expected allowed: ({Tools.ToString (optionalExpectedWrapper.Allowed())}), actual allowed: ({Tools.ToString (optionalActualWrapper.Allowed())}), " +
                //     $"expected disallowed: ({Tools.ToString (optionalExpectedWrapper.Disallowed())}), actual disallowed: ({Tools.ToString (optionalActualWrapper.Disallowed())}), expected result: {optionalExpectedResult}, " +
                //     $"actual result: {optionalActualResult}, test result: {(optionalExpectedResult == optionalActualResult ? "PASSED" : "FAILED")}");
                // }

                ++tests;
                if (mixedExpected == mixedActual) ++passed;
                if (mixedExpected!= mixedActual) ++failed;
                if (mixedActual) ++@true;
                if (!mixedActual) ++@false;

                Assert.IsEqual (mixedExpected, mixedActual,
                  $"{methodName} {tests}: Mixed (Required: ({Tools.ToString (requiredMotions)}), " +
                  $"Optional: ({Tools.ToString (optionalMotions)})), " +
                  $"velocity: {velocity}, expected: {mixedExpected}, actual: {mixedActual}, result: FAILED");

                // @formatter:on
              }
            }
          }
        }
      }
    }

    GD.Print ($"Ran {tests:n0} tests. {passed:n0} passed, {failed:n0} failed. True: {@true:n0}, False: {@false:n0}");
  }

  private void TestMotion (Func <Motion[], IMotionWrapper> wrapperFunc, List <Motion> motions, ref PhysicsBodyData physicsBodyData,
    Func <Motion, PhysicsBodyData, bool> activeFunc, bool expectedResult, IReadOnlyCollection <Motion> expectedAllowed,
    IReadOnlyCollection <Motion> expectedDisallowed, string name)
  {
    var wrapper = new CompositeMotionWrapper (_ (wrapperFunc (motions.ToArray())));
    var actualAllowed = wrapper.Allowed().ToList();
    var actualDisallowed = wrapper.Disallowed().ToList();
    var actual = wrapper.Compose (ref physicsBodyData, activeFunc);

    // @formatter:off

    Assert.IsEqual (expectedResult, actual);

    actual = !expectedAllowed.Except (actualAllowed).Any() && !actualAllowed.Except (expectedAllowed).Any();

    Assert.IsTrue (actual,
      $"Mismatch of allowed motions. {name.Capitalize()} ({Tools.ToString (motions)}), velocity: {physicsBodyData.Velocity}, " +
      $"expected allowed: ({Tools.ToString (expectedAllowed)}), actual allowed: ({Tools.ToString (actualAllowed)}), " +
      $"expected disallowed: ({Tools.ToString (expectedDisallowed)}), actual disallowed: ({Tools.ToString (actualDisallowed)}), " +
      "result: FAILED");

    actual = !expectedDisallowed.Except (actualDisallowed).Any() && !actualDisallowed.Except (expectedDisallowed).Any();

    Assert.IsTrue (actual,
      $"Mismatch of disallowed motions. {name.Capitalize()} ({Tools.ToString (motions)}), velocity: {physicsBodyData.Velocity}, " +
      $"expected allowed: ({Tools.ToString (expectedAllowed)}), actual allowed: ({Tools.ToString (actualAllowed)}), " +
      $"expected disallowed: ({Tools.ToString (expectedDisallowed)}), actual disallowed: ({Tools.ToString (actualDisallowed)}), " +
      "result: FAILED");

    // @formatter:on
  }
}