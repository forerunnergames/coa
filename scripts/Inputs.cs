using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Inputs;

[SuppressMessage ("ReSharper", "MemberCanBePrivate.Global")]
public static class Inputs
{
  public static IInputWrapper Required (params Input[] inputs) => new CompositeRequiredInputWrapper (inputs);
  public static IInputWrapper Required (IInputWrapper wrapper) => Required (wrapper.Inputs());
  public static IInputWrapper Optional (params Input[] inputs) => new CompositeOptionalInputWrapper (inputs);
  public static IInputWrapper Optional (IInputWrapper wrapper) => Optional (wrapper.Inputs());
  public static IInputWrapper[] _ (params IInputWrapper[] wrappers) => new IInputWrapper[] { new CompositeInputWrapper (wrappers) };
  public static readonly IEnumerable <Input> Values = Enum.GetValues (typeof (Input)).Cast <Input>();

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

  public static bool IsActive (this Input i)
  {
    if (i == Input.None) return Mapping.Values.All (x => !x.Any (Godot.Input.IsActionPressed));

    var count = Mapping[i].Count (Godot.Input.IsActionPressed);
    var max = MaxActiveActions.GetValueOrDefault (i, 1);

    return count >= Math.Min (max, 1) && count <= max;
  }

  // @formatter:off
  public static bool IsPressed (Input i) => Mapping[i].Any (Godot.Input.IsActionPressed);
  public static bool WasPressed (Input i) => Mapping[i].Any (Godot.Input.IsActionJustPressed);
  public static bool WasPressed (Input i, Godot.InputEvent e) => e is Godot.InputEventKey k && Mapping[i].Any (x => k.IsActionPressed (x));
  public static bool WasReleased (Input i, Godot.InputEvent e) => e is Godot.InputEventKey k && Mapping[i].Any (x => k.IsActionReleased (x));
  public static bool WasReleased (Input i) => Mapping[i].Any (Godot.Input.IsActionJustReleased);
  // @formatter:on
}

public interface IInputWrapper
{
  public bool Result();
  public IEnumerable <Input> Allowed();
  public IEnumerable <Input> Disallowed();
  public Input[] Inputs();
}

public abstract class AbstractCompositeInputWrapper : IInputWrapper
{
  private readonly Input[] _inputs;
  private readonly IEnumerable <Input> _allowed;
  private readonly IEnumerable <Input> _disallowed;

  protected AbstractCompositeInputWrapper (IEnumerable <Input> inputs)
  {
    _inputs = inputs.ToArray();
    _disallowed = Values.Except (_inputs).Where (x => _inputs.Any (y => !Mapping[x].Intersect (Mapping[y]).Any())).Distinct();
    _allowed = _inputs.Except (_disallowed).Distinct();
  }

  public abstract bool Result();
  public IEnumerable <Input> Allowed() => _allowed;
  public IEnumerable <Input> Disallowed() => _disallowed;
  public Input[] Inputs() => _inputs;
  public override string ToString() => Tools.ToString (_inputs);
}

public class CompositeRequiredInputWrapper : AbstractCompositeInputWrapper
{
  public CompositeRequiredInputWrapper (Input[] inputs) : base (inputs) { }
  public override bool Result() => Allowed().All (x => x.IsActive());
  public override string ToString() => "Required: " + base.ToString();
}

public class CompositeOptionalInputWrapper : AbstractCompositeInputWrapper
{
  public CompositeOptionalInputWrapper (IEnumerable <Input> inputs) : base (new List <Input> (inputs) { Input.None }) { }
  public override bool Result() => true;
  public override string ToString() => "Optional: " + base.ToString();
}

public class CompositeInputWrapper : IInputWrapper
{
  private readonly IInputWrapper[] _wrappers;
  private readonly IEnumerable <Input> _allowed;
  private readonly IEnumerable <Input> _disallowed;

  public CompositeInputWrapper (IInputWrapper[] wrappers)
  {
    _wrappers = wrappers;
    _allowed = _wrappers.SelectMany (x => x.Allowed()).Distinct();
    _disallowed = _wrappers.SelectMany (x => x.Disallowed().Except (_wrappers.SelectMany (y => y.Allowed()))).Distinct();
  }

  public bool Result() => _wrappers.All (x => x.Result()) && _disallowed.All (y => !y.IsActive());
  public IEnumerable <Input> Allowed() => _allowed;
  public IEnumerable <Input> Disallowed() => _disallowed;
  public Input[] Inputs() => _wrappers.SelectMany (x => x.Inputs()).ToArray();
  public override string ToString() => Tools.ToString (_wrappers);
}