using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using WAT;
using static Inputs;
using static Inputs.Input;

public class InputsTest : Test
{
  private static readonly Func <Inputs.Input, string, bool> Pressed = (_, _) => true;
  private static readonly Func <Inputs.Input, string, bool> Unpressed = (_, _) => false;

  [Test]
  public void TestInputActiveWhenButtonPressed()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod()?.Name}:\n---");
    Assert.IsTrue (Energy.IsActive (Pressed));
  }

  [Test]
  public void TestInputNotActiveWhenNotPressed()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod()?.Name}:\n---");
    Assert.IsFalse (Energy.IsActive (Unpressed));
  }

  [Test]
  public void TestOptionalRightUpPressed()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod()?.Name}:\n---");

    // @formatter:off
    TestInput (Optional, Pressed, new List <Inputs.Input> { Right, Up }, false, new List <Inputs.Input> { Right, Up, None },
      new List <Inputs.Input> { Item, Text, Respawn, Season, Music, Down, Left, Jump, Energy }, "optional");
    // @formatter:on
  }

  // TestAll 299
  [Test]
  public void TestOptionalMusicHorizontal()
  {
    GD.Print ($"---\n{MethodBase.GetCurrentMethod()?.Name}:\n---");

    // @formatter:off
    TestInput (Optional, Pressed, new List <Inputs.Input> { Music, Horizontal }, false,
      new List <Inputs.Input> { Music, Horizontal, Left, Right, None },
      new List <Inputs.Input> { Item, Text, Respawn, Season, Up, Down, Jump, Energy, Vertical }, "optional");
    // @formatter:on
  }

  private void TestInput (Func <Inputs.Input[], IInputWrapper> wrapperFunc, Func <Inputs.Input, string, bool> inputFunc,
    List <Inputs.Input> inputs, bool expectedResult, IReadOnlyCollection <Inputs.Input> expectedAllowed,
    IReadOnlyCollection <Inputs.Input> expectedDisallowed, string name)
  {
    var wrapper = new CompositeInputWrapper (_ (wrapperFunc (inputs.ToArray())));
    var actualAllowed = wrapper.Allowed().ToList();
    var actualDisallowed = wrapper.Disallowed().ToList();
    var data = "";
    var actual = wrapper.Compose (ref data, inputFunc);

    // @formatter:off

    Assert.IsEqual (expectedResult, actual);

    actual = !expectedAllowed.Except (actualAllowed).Any() && !actualAllowed.Except (expectedAllowed).Any();

    Assert.IsTrue (actual,
      $"Mismatch of allowed inputs. {name.Capitalize()} ({Tools.ToString (inputs)}), " +
      $"expected allowed: ({Tools.ToString (expectedAllowed)}), actual allowed: ({Tools.ToString (actualAllowed)}), " +
      $"expected disallowed: ({Tools.ToString (expectedDisallowed)}), actual disallowed: ({Tools.ToString (actualDisallowed)}), " +
      "result: FAILED");

    actual = !expectedDisallowed.Except (actualDisallowed).Any() && !actualDisallowed.Except (expectedDisallowed).Any();

    Assert.IsTrue (actual,
      $"Mismatch of disallowed inputs. {name.Capitalize()} ({Tools.ToString (inputs)}), " +
      $"expected allowed: ({Tools.ToString (expectedAllowed)}), actual allowed: ({Tools.ToString (actualAllowed)}), " +
      $"expected disallowed: ({Tools.ToString (expectedDisallowed)}), actual disallowed: ({Tools.ToString (actualDisallowed)}), " +
      "result: FAILED");

    // @formatter:on
  }

  // [Test]
  public void TestAll()
  {
    var methodName = MethodBase.GetCurrentMethod()?.Name;
    GD.Print ($"---\n{methodName}:\n---");
    var inputs = new HashSet <Inputs.Input>();
    var requiredInputs = new HashSet <Inputs.Input>();
    var optionalInputs = new HashSet <Inputs.Input>();
    var tests = 0;
    var passed = 0;
    var failed = 0;
    var @true = 0;
    var @false = 0;

    foreach (var input1 in Values)
    {
      foreach (var input2 in Values)
      {
        // foreach (var input3 in Values)
        // {
        //   foreach (var input4 in Values)
        //   {
        inputs.Clear();
        requiredInputs.Clear();
        optionalInputs.Clear();
        inputs.Add (input1);
        inputs.Add (input2);
        // inputs.Add (input3);
        // inputs.Add (input4);
        requiredInputs.Add (input1);
        requiredInputs.Add (input2);
        // optionalInputs.Add (input3);
        // optionalInputs.Add (input4);

            // @formatter:off

            var requiredWrapper = new CompositeInputWrapper (_ (Required (inputs.ToArray())));
            var requiredExpected = inputs.All (x => x.IsActive (Pressed)) && requiredWrapper.Disallowed().All (x => !x.IsActive (Unpressed));
            var requiredData = "";
            var requiredActual = requiredWrapper.Compose (ref requiredData, Pressed);

            var optionalExpectedWrapper = new CompositeInputWrapper (_ (Optional (inputs.ToArray())));
            var optionalExpectedResult = inputs.Any (x => x.IsActive (Pressed)) && optionalExpectedWrapper.Disallowed().All (x => !x.IsActive (Unpressed));
            var optionalActualWrapper = new CompositeInputWrapper (_ (Optional (inputs.ToArray())));
            var optionalData = "";
            var optionalActualResult = optionalActualWrapper.Compose (ref optionalData, Pressed);

            var mixedWrapper = new CompositeInputWrapper (_ (Required (requiredInputs.ToArray()), Optional (optionalInputs.ToArray())));
            var mixedExpected = requiredInputs.All (x => x.IsActive (Pressed)) && mixedWrapper.Disallowed().All (x => !x.IsActive (Unpressed));
            var mixedData = "";
            var mixedActual = mixedWrapper.Compose (ref mixedData, Pressed);

            ++tests;
            if (requiredExpected == requiredActual) ++passed;
            if (requiredExpected!= requiredActual) ++failed;
            if (requiredActual) ++@true;
            if (!requiredActual) ++@false;

            // Assert.IsEqual (requiredExpected, requiredActual,
            //   $"{methodName} {tests}: Required ({Tools.ToString (inputs)}), " +
            //   $"disallowed: ({Tools.ToString (requiredWrapper.Disallowed())}), expected: {requiredExpected}, " +
            //   $"actual: {requiredActual}, result: FAILED");

            // if (tests == 18847)
            // {
            //   GD.Print ( $"{methodName} {tests}: Required ({Tools.ToString (inputs)}), " +
            //     $"allowed: ({Tools.ToString (requiredWrapper.Allowed())}), disallowed: ({Tools.ToString (requiredWrapper.Disallowed())}), expected: {requiredExpected}, " +
            //     $"actual: {requiredActual}, result: {(requiredExpected == requiredActual ? "PASSED" : "FAILED")}");
            // }

            ++tests;
            if (optionalExpectedResult == optionalActualResult) ++passed;
            if (optionalExpectedResult!= optionalActualResult) ++failed;
            if (optionalActualResult) ++@true;
            if (!optionalActualResult) ++@false;

            Assert.IsEqual (optionalExpectedResult, optionalActualResult,
              $"{methodName} {tests}: Optional ({Tools.ToString (inputs)}), " +
              $"disallowed: ({Tools.ToString (optionalExpectedWrapper.Disallowed())}), " +
              $"expected: {optionalExpectedResult}, actual: {optionalActualResult}, result: FAILED");

            // if (tests is >= 23600 and <= 23624)
            if (optionalExpectedResult != optionalActualResult)
            {
              GD.Print (
                $"{methodName} {tests}: Optional ({Tools.ToString (inputs)}), " +
                $"expected allowed: ({Tools.ToString (optionalExpectedWrapper.Allowed())}), actual allowed: ({Tools.ToString (optionalActualWrapper.Allowed())}), " +
                $"expected disallowed: ({Tools.ToString (optionalExpectedWrapper.Disallowed())}), actual disallowed: ({Tools.ToString (optionalActualWrapper.Disallowed())}), expected result: {optionalExpectedResult}, " +
                $"actual result: {optionalActualResult}, test result: FAILED");
            }

            ++tests;
            if (mixedExpected == mixedActual) ++passed;
            if (mixedExpected!= mixedActual) ++failed;
            if (mixedActual) ++@true;
            if (!mixedActual) ++@false;

            Assert.IsEqual (mixedExpected, mixedActual,
              $"{methodName} {tests}: Mixed (Required: ({Tools.ToString (requiredInputs)}), " +
              $"Optional: ({Tools.ToString (optionalInputs)})), " +
              $"expected: {mixedExpected}, actual: {mixedActual}, result: FAILED");

        // @formatter:on
        //   }
        // }
      }
    }

    GD.Print ($"Ran {tests:n0} tests. {passed:n0} passed, {failed:n0} failed. True: {@true:n0}, False: {@false:n0}");
  }
}