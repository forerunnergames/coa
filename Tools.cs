using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

// ReSharper disable MemberCanBePrivate.Global
public static class Tools
{
  public enum Input
  {
    Horizontal,
    Vertical,
    Item,
    Text,
    Respawn,
    Up,
    Down,
    Left,
    Right,
    Jump
  }

  private static readonly Dictionary <Input, string[]> Inputs = new()
  {
    { Input.Horizontal, new[] { "move_left", "move_right" } },
    { Input.Vertical, new[] { "move_up", "move_down" } },
    { Input.Item, new[] { "use_item" } },
    { Input.Text, new[] { "show_text" } },
    { Input.Respawn, new[] { "respawn" } },
    { Input.Up, new[] { "move_up" } },
    { Input.Down, new[] { "move_down" } },
    { Input.Left, new[] { "move_left" } },
    { Input.Right, new[] { "move_right" } },
    { Input.Jump, new[] { "jump" } }
  };

  public static bool IsReleased (Input i, InputEvent e) => e is InputEventKey k && Inputs[i].Any (x => k.IsActionReleased (x));
  public static bool IsOneActiveOf (Input i) => Inputs[i].Where (Godot.Input.IsActionPressed).Take (2).Count() == 1;
  public static bool IsAnyActiveOf (Input i) => Inputs[i].Any (Godot.Input.IsActionPressed);
  public static bool IsLeftArrowPressed() => Godot.Input.IsActionPressed (Inputs[Input.Left][0]);
  public static bool WasLeftArrowPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Left][0]);
  public static bool IsRightArrowPressed() => Godot.Input.IsActionPressed (Inputs[Input.Right][0]);
  public static bool WasRightArrowPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Right][0]);
  public static bool IsUpArrowPressed() => Godot.Input.IsActionPressed (Inputs[Input.Up][0]);
  public static bool WasUpArrowReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Up][0]);
  public static bool WasUpArrowPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Up][0]);
  public static bool IsDownArrowPressed() => Godot.Input.IsActionPressed (Inputs[Input.Down][0]);
  public static bool WasDownArrowPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Down][0]);
  public static bool IsAnyHorizontalArrowPressed() => IsLeftArrowPressed() || IsRightArrowPressed();
  public static bool IsEveryHorizontalArrowPressed() => IsLeftArrowPressed() && IsRightArrowPressed();
  public static bool IsAnyVerticalArrowPressed() => IsUpArrowPressed() || IsDownArrowPressed();
  public static bool IsEveryVerticalArrowPressed() => IsUpArrowPressed() && IsDownArrowPressed();
  public static bool IsAnyArrowKeyPressed() => IsAnyHorizontalArrowPressed() || IsAnyVerticalArrowPressed();
  public static bool IsItemKeyPressed() => Godot.Input.IsActionPressed (Inputs[Input.Item][0]);
  public static bool WasItemKeyReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Item][0]);
  public static bool WasJumpKeyPressed() => Godot.Input.IsActionJustPressed (Inputs[Input.Jump][0]);
  public static bool WasJumpKeyReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Jump][0]);
  public static float SafelyClampMin (float f, float min) => IsSafelyLessThan (f, min) ? min : f;
  public static float SafelyClampMax (float f, float max) => IsSafelyGreaterThan (f, max) ? max : f;
  public static float SafelyClamp (float f, float min, float max) => SafelyClampMin (SafelyClampMax (f, max), min);
  public static bool IsSafelyLessThan (float f1, float f2) => f1 < f2 && !Mathf.IsEqualApprox (f1, f2);
  public static bool IsSafelyGreaterThan (float f1, float f2) => f1 > f2 && !Mathf.IsEqualApprox (f1, f2);
  public static void LoopAudio (AudioStream a) { LoopAudio (a, 0.0f, a.GetLength()); }

  /// <summary>
  /// Whether or not the specified input type is the only active input.
  /// </summary>
  /// <param name="input"></param>
  /// <param name="disableExclusivity">if true, bypass exclusivity requirement for active input
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
  public static bool IsExclusivelyActiveUnless (Input input, bool disableExclusivity)
  {
    var activeInclusions = new List <string>();
    var activeExclusions = new List <string>();

    foreach (var (key, values) in Inputs)
    {
      foreach (var value in values)
      {
        var isActionPressed = Godot.Input.IsActionPressed (value);

        if (key == input && isActionPressed) activeInclusions.Add (value);
        else if (isActionPressed) activeExclusions.Add (value);
      }
    }

    // If not ignoring exclusions, active exclusions must be unique; i.e., must not be an active inclusion.
    //   E.g., InputType.Vertical & InputType.Up both contain "move_up", so if the specified input type is
    //   Vertical and "move_up" is an active inclusion, then InputType.Up ["move_up"] will not count as an
    //   active exclusion, since it isn't unique (even though it is active).
    return activeInclusions.Any() && (disableExclusivity || !activeExclusions.Except (activeInclusions).Any());
  }

  public static void LoopAudio (AudioStream stream, float loopBeginSeconds, float loopEndSecondsWavOnly)
  {
    switch (stream)
    {
      case AudioStreamOGGVorbis ogg:
      {
        ogg.Loop = true;
        ogg.LoopOffset = loopBeginSeconds;

        break;
      }
      case AudioStreamSample wav:
      {
        wav.LoopMode = AudioStreamSample.LoopModeEnum.Forward;
        wav.LoopBegin = Mathf.RoundToInt (loopBeginSeconds * wav.MixRate);
        wav.LoopEnd = Mathf.RoundToInt (loopEndSecondsWavOnly * wav.MixRate);

        break;
      }
    }
  }

  public static string ToString <T> (IEnumerable <T> e, string sep = ", ", Func <T, string> f = null) =>
    e.Select (f ?? (s => s.ToString())).DefaultIfEmpty (string.Empty).Aggregate ((a, b) => a + sep + b);

  public static bool IsIntersectingAnyTile (Vector2 globalPosition, TileMap tileMap)
  {
    return tileMap.GetCellv (tileMap.WorldToMap (tileMap.ToLocal (globalPosition))) != -1;
  }
}