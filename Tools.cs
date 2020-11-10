using System.Collections.Generic;
using System.Linq;
using Godot;

public static class Tools
{
  public enum InputType
  {
    Horizontal,
    Vertical,
    Item,
    Up,
    Down,
    Left,
    Right
  }

  private static readonly Dictionary <InputType, string[]> InputTypes = new Dictionary <InputType, string[]>()
  {
    { InputType.Horizontal, new[] { "move_left", "move_right" } },
    { InputType.Vertical, new[] { "move_up", "move_down" } },
    { InputType.Item, new[] { "use_item" } },
    { InputType.Up, new[] { "move_up" } },
    { InputType.Down, new[] { "move_down" } },
    { InputType.Left, new[] { "move_left" } },
    { InputType.Right, new[] { "move_right" } }
  };

  public static bool IsLeftArrowPressed()
  {
    return Input.IsActionPressed (InputTypes [InputType.Left] [0]);
  }

  private static bool WasLeftArrowPressedOnce()
  {
    return Input.IsActionJustPressed (InputTypes [InputType.Left] [0]);
  }

  public static bool IsRightArrowPressed()
  {
    return Input.IsActionPressed (InputTypes [InputType.Right] [0]);
  }

  private static bool WasRightArrowPressedOnce()
  {
    return Input.IsActionJustPressed (InputTypes [InputType.Right] [0]);
  }

  private static bool IsUpArrowPressed()
  {
    return Input.IsActionPressed (InputTypes [InputType.Up] [0]);
  }

  public static bool WasUpArrowPressedOnce()
  {
    return Input.IsActionJustPressed (InputTypes [InputType.Up] [0]);
  }

  private static bool IsDownArrowPressed()
  {
    return Input.IsActionPressed (InputTypes [InputType.Down] [0]);
  }

  public static bool WasDownArrowPressedOnce()
  {
    return Input.IsActionJustPressed (InputTypes [InputType.Down] [0]);
  }

  public static bool IsAnyHorizontalArrowPressed()
  {
    return IsLeftArrowPressed() || IsRightArrowPressed();
  }

  public static bool IsAnyVerticalArrowPressed()
  {
    return IsUpArrowPressed() || IsDownArrowPressed();
  }

  public static bool IsItemKeyPressed()
  {
    return Input.IsActionPressed (InputTypes [InputType.Item] [0]);
  }

  public static float SafelyClampMin (float value, float min)
  {
    return IsSafelyLessThan (value, min) ? min : value;
  }

  private static float SafelyClampMax (float value, float max)
  {
    return IsSafelyGreaterThan (value, max) ? max : value;
  }

  public static float SafelyClamp (float value, float min, float max)
  {
    return SafelyClampMin (SafelyClampMax (value, max), min);
  }

  private static bool IsSafelyLessThan (float f1, float f2)
  {
    return f1 < f2 && !Mathf.IsEqualApprox (f1, f2);
  }

  private static bool IsSafelyGreaterThan (float f1, float f2)
  {
    return f1 > f2 && !Mathf.IsEqualApprox (f1, f2);
  }

  /// <summary>
  /// Whether or not the specified input type is the only active input.
  /// </summary>
  /// <param name="inputType"></param>
  /// <param name="ignoreExclusions">if true, bypass exclusivity requirement for active input
  /// <br/>
  /// Useful when the desired action from the specified input is already being executed,
  /// since ignoring exclusivity prevents said action from being canceled when
  /// an excluded input becomes active.
  /// <br/>
  /// For example, holding the right arrow key down
  /// and running, and you want to disable jumping (space bar) while running. If the
  /// player is already running, and exclusivity of input is required, i.e., in order
  /// to run, ONLY the right arrow key may be pressed, then pressing the space bar will
  /// cancel the running because the input is no longer exclusive. In this case you would
  /// want to IGNORE exclusivity, continuing to run even when the space bar is pressed in
  /// addition to the right arrow key.
  /// <br/>
  /// Then as long as you implement the same thing for running as with jumping, then jumping
  /// will not work when running for the same reason; i.e., the space bar requires exclusivity.
  /// The exception for ignoring exclusions would be if already jumping; however, in this example
  /// the player is running, so exclusions would NOT be ignored for jumping, therefore jumping
  /// would be disabled while running because the right arrow key is already being pressed.
  /// </param>
  /// <returns></returns>
  public static bool IsExclusivelyActiveUnless (InputType inputType, bool ignoreExclusions)
  {
    var activeInclusions = new List <string>();
    var activeExclusions = new List <string>();

    foreach (var dict in InputTypes)
    {
      foreach (var value in dict.Value)
      {
        var isActionPressed = Input.IsActionPressed (value);
        if (dict.Key == inputType && isActionPressed) activeInclusions.Add (value);
        else if (isActionPressed) activeExclusions.Add (value);
      }
    }

    // If not ignoring exclusions, active exclusions must be unique; i.e., must not be an active inclusion.
    //   E.g., InputType.Vertical & InputType.Up both contain "move_up", so if the specified input type is
    //   Vertical and "move_up" is an active inclusion, then InputType.Up ["move_up"] will not count as an
    //   active exclusion, since it isn't unique (even though it is active).
    return activeInclusions.Any() && (ignoreExclusions || !activeExclusions.Except (activeInclusions).Any());
  }
}