using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Inputs;

[SuppressMessage ("ReSharper", "MemberCanBePrivate.Global")]
public static class Inputs
{
  public static IInputWrapper Required (params Input[] inputs) => new RequiredInputWrapper (inputs);
  public static IInputWrapper Required (IInputWrapper wrapper) => Required (wrapper.GetItems().ToArray());
  public static IInputWrapper Optional (params Input[] inputs) => new OptionalInputWrapper (new List <Input> (inputs) { Input.None });
  public static IInputWrapper Optional (IInputWrapper wrapper) => Optional (wrapper.GetItems().ToArray());
  public static IInputWrapper[] _ (params IInputWrapper[] wrappers) => new IInputWrapper[] { new CompositeInputWrapper (wrappers) };
  public static readonly IEnumerable <Input> Values = Enum.GetValues (typeof (Input)).Cast <Input>();
  public delegate bool IsInputActive (string action);

  public static readonly ImmutableDictionary <Input, string[]> Mapping = new Dictionary <Input, string[]>
  {
    { Input.None, Array.Empty <string>() },
    { Input.Horizontal, new[] { "move_left", "move_right" } },
    { Input.Vertical, new[] { "move_up", "move_down" } },
    { Input.Item, new[] { "use_item" } },
    { Input.Text, new[] { "show_text" } },
    { Input.Respawn, new[] { "respawn" } },
    { Input.Season, new[] { "season" } },
    { Input.Music, new[] { "music" } },
    { Input.Up, new[] { "move_up", "read_sign" } },
    { Input.Down, new[] { "move_down" } },
    { Input.Left, new[] { "move_left" } },
    { Input.Right, new[] { "move_right" } },
    { Input.Jump, new[] { "jump" } },
    { Input.Energy, new[] { "energy" } }
  }.ToImmutableDictionary();

  // @formatter:off

  public static readonly ImmutableDictionary <Input, int> MaxActiveActions = new Dictionary <Input, int>
  {
    { Input.None, 0 },
    { Input.Horizontal, 1 },
    { Input.Vertical, 1 },
    { Input.Up, 2 },
  }.ToImmutableDictionary();

  // @formatter:on

  public enum Input
  {
    None,
    Horizontal,
    Vertical,
    Item,
    Text,
    Respawn,
    Season,
    Music,
    Up,
    Down,
    Left,
    Right,
    Jump,
    Energy
  }

  public static bool IsActive (this Input i, IsInputActive isInputActive)
  {
    // TODO Remove.
    Godot.GD.Print ("IsActive: Input: ", i);

    // TODO Remove.
    foreach (var action in Mapping[i])
    {
      Godot.GD.Print ("action: ", action, ", isInputActive: ", isInputActive (action), ", is pressed (should match isInputActive): ",
        Godot.Input.IsActionPressed (action));
    }

    if (i == Input.None) return Mapping.All (x => x.Value.All (y => !isInputActive (y)));

    var count = Mapping[i].Count (x => isInputActive (x));
    var max = MaxActiveActions.GetValueOrDefault (i, 1);

    return count >= Math.Min (max, 1) && count <= max;
  }

  // @formatter:off
  public static bool IsPressed (Input i) => Mapping[i].Any (x => Godot.Input.IsActionPressed (x));
  public static bool WasPressed (Input i) => Mapping[i].Any (x => Godot.Input.IsActionJustPressed (x));
  public static bool WasPressed (Input i, Godot.InputEvent e) => e is Godot.InputEventKey k && Mapping[i].Any (x => k.IsActionPressed (x));
  public static bool WasReleased (Input i, Godot.InputEvent e) => e is Godot.InputEventKey k && Mapping[i].Any (x => k.IsActionReleased (x));
  public static bool WasReleased (Input i) => Mapping[i].Any (x => Godot.Input.IsActionJustReleased (x));
  public static bool WasMouseLeftClicked (Godot.InputEvent e) => e is Godot.InputEventMouseButton { ButtonIndex: (int)Godot.ButtonList.Left, Pressed: true };
  // @formatter:on
}

public interface IInputWrapper : IRequiredOptionalWrapper <Input>
{
}

public class RequiredInputWrapper : RequiredWrapper <Input>, IInputWrapper
{
  public RequiredInputWrapper (IReadOnlyCollection <Input> inputs) : base (inputs,
    Values().Where (x => inputs.Any (y => x == y || y != Input.None && Mapping[y].All (z => Mapping[x].Any (a => a == z)))),
    Values().Where (x => inputs.All (y => x != y && Mapping[y].All (z => Mapping[x].All (a => a != z)))))
  {
  }
}

public class OptionalInputWrapper : OptionalWrapper <Input>, IInputWrapper
{
  public OptionalInputWrapper (IReadOnlyCollection <Input> inputs) : base (inputs,
    Values().Where (x => inputs.Any (y => x == y || Mapping[y].Any (z => Mapping[x].All (a => a == z)))),
    Values().Where (x => inputs.All (y => x != y && Mapping[y].All (z => x != Input.None && Mapping[x].All (a => a != z)))))
  {
  }
}

public class CompositeInputWrapper : CompositeRequiredOptionalWrapper <Input>, IInputWrapper
{
  public CompositeInputWrapper (IEnumerable <IInputWrapper> wrappers) : base (wrappers) { }
}