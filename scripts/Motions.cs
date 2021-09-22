using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static Gravity;
using static Motions;
using static Positionings;
using static Tools;

public static class Motions
{
  public const float VelocityEpsilon = 1.0f; //0.001f;

  // @formatter:off
  public static IMotionWrapper Required (params Motion[] motions) => new CompositeRequiredMotionWrapper (motions);
  public static IMotionWrapper Optional (params Motion[] motions) => new CompositeOptionalMotionWrapper (motions);
  public static IMotionWrapper[] _ (params IMotionWrapper[] wrappers) => new IMotionWrapper[] { new CompositeMotionWrapper (wrappers) };
  public static readonly IEnumerable <Motion> Values = Enum.GetValues (typeof (Motion)).Cast <Motion>();
  // @formatter:on

  public static readonly ImmutableDictionary <Motion, string[]> Mapping = new Dictionary <Motion, string[]>
  {
    { Motion.None, Array.Empty <string>() },
    { Motion.Any, new[] { "left", "right", "up", "down" } },
    { Motion.Horizontal, new[] { "left", "right" } },
    { Motion.Vertical, new[] { "up", "down" } },
    { Motion.Up, new[] { "up" } },
    { Motion.Down, new[] { "down" } },
    { Motion.Left, new[] { "left" } },
    { Motion.Right, new[] { "right" } },
  }.ToImmutableDictionary();

  public enum Motion
  {
    None,
    Any,
    Up,
    Down,
    Left,
    Right,
    Vertical,
    Horizontal
  }

  // @formatter:off
  public static bool IsActive (this Motion motion, Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning)
  {
    return gravity switch
    {
      GravityType.BeforeApplied => motion switch
      {
        Motion.None => positioning is Positioning.Ground && AreAlmostEqual (velocity, Godot.Vector2.Zero, VelocityEpsilon) || positioning is not Positioning.Ground && AreAlmostEqual (velocity, -GravityForce, VelocityEpsilon),
        Motion.Any => positioning is Positioning.Ground && !AreAlmostEqual (velocity, Godot.Vector2.Zero, VelocityEpsilon) || positioning is not Positioning.Ground && !AreAlmostEqual (velocity, -GravityForce, VelocityEpsilon),
        Motion.Up => velocity.y + GravityForce.y < -VelocityEpsilon,
        Motion.Down => positioning is Positioning.Ground && velocity.y > VelocityEpsilon || positioning is not Positioning.Ground && velocity.y + GravityForce.y > VelocityEpsilon,
        Motion.Left => velocity.x < -VelocityEpsilon,
        Motion.Right => velocity.x > VelocityEpsilon,
        Motion.Vertical => Motion.Up.IsActive (velocity, gravity, positioning) || Motion.Down.IsActive (velocity, gravity, positioning),
        Motion.Horizontal => Motion.Left.IsActive (velocity, gravity, positioning) || Motion.Right.IsActive (velocity, gravity, positioning),
        _ => false
      },
      GravityType.AfterApplied => motion switch
      {
        Motion.None => positioning is Positioning.Ground && AreAlmostEqual (velocity, GravityForce, VelocityEpsilon) || positioning is not Positioning.Ground && AreAlmostEqual (velocity, Godot.Vector2.Zero, VelocityEpsilon),
        Motion.Any => positioning is Positioning.Ground && !AreAlmostEqual (velocity, GravityForce, VelocityEpsilon) || positioning is not Positioning.Ground && !AreAlmostEqual (velocity, Godot.Vector2.Zero, VelocityEpsilon),
        Motion.Up => velocity.y < -VelocityEpsilon,
        Motion.Down => positioning is Positioning.Ground && velocity.y > GravityForce.y + VelocityEpsilon || positioning is not Positioning.Ground && velocity.y > VelocityEpsilon,
        Motion.Left => velocity.x < -VelocityEpsilon,
        Motion.Right => velocity.x > VelocityEpsilon,
        Motion.Vertical => Motion.Up.IsActive (velocity, gravity, positioning) || Motion.Down.IsActive (velocity, gravity, positioning),
        Motion.Horizontal => Motion.Left.IsActive (velocity, gravity, positioning) || Motion.Right.IsActive (velocity, gravity, positioning),
        _ => false
      },
      GravityType.None => motion switch
      {
        Motion.None => AreAlmostEqual (velocity, Godot.Vector2.Zero, VelocityEpsilon),
        Motion.Any => !AreAlmostEqual (velocity, Godot.Vector2.Zero, VelocityEpsilon),
        Motion.Up => velocity.y < -VelocityEpsilon,
        Motion.Down => velocity.y > VelocityEpsilon,
        Motion.Left => velocity.x < -VelocityEpsilon,
        Motion.Right => velocity.x > VelocityEpsilon,
        Motion.Vertical => Math.Abs (velocity.y) > VelocityEpsilon,
        Motion.Horizontal => Math.Abs (velocity.x) > VelocityEpsilon,
        _ => false
      },
      _ => false
    };
  }
  // @formatter:on
}

public interface IMotionWrapper
{
  public bool ComposeFromRequired (bool requiredResult, Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning);
  public bool ComposeFromOptional (bool optionalResult, Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning);
  public bool Compose (Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning);
  public IEnumerable <Motion> Allowed();
  public IEnumerable <Motion> Disallowed();
}

// @formatter:off
public abstract class AbstractCompositeMotionWrapper : IMotionWrapper
{
  protected readonly IEnumerable <Motion> Motions;
  protected AbstractCompositeMotionWrapper (IEnumerable <Motion> motions) => Motions = motions.Distinct().ToArray();
  public abstract IEnumerable <Motion> Allowed();
  public abstract IEnumerable <Motion> Disallowed();
  public abstract bool ComposeFromRequired (bool requiredResult, Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning);
  public abstract bool ComposeFromOptional (bool optionalResult, Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning);
  public abstract bool Compose (Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning);
  public override string ToString() => Tools.ToString (Motions);
}
// @formatter:on

public class CompositeRequiredMotionWrapper : AbstractCompositeMotionWrapper
{
  private readonly IEnumerable <Motion> _allowed;
  private readonly IEnumerable <Motion> _disallowed;

  public CompositeRequiredMotionWrapper (IEnumerable <Motion> motions) : base (motions)
  {
    // @formatter:off
    // if up/down/left/right is allowed, add accompanying vertical/horizontal
    // if vertical/horizontal is allowed, do NOT add accompanying up/down/left/right because it's an OR operation in IsActive,
    // adding them would instead cause an AND operation when Required, which would always be false.
    // the disallowed calculation must operate on the original allowed motions WITHOUT any accompanying motions added
    _allowed = Values.Where (x => Motions.Any (y => x == y || y != Motion.None && Mapping[y].All (z => Mapping[x].Any (a => a == z))));
    _disallowed = Values.Where ( x => Motions.All (y => x != y && Mapping[y].All (z => Mapping[x].All (a => a != z || y == Motion.Any))));
    // @formatter:on
  }

  public override IEnumerable <Motion> Allowed() => _allowed;
  public override IEnumerable <Motion> Disallowed() => _disallowed;

  public override bool ComposeFromRequired (bool requiredResult, Godot.Vector2 velocity, GravityType? gravity,
    Positioning? positioning) =>
    Motions.Aggregate (requiredResult, (result, motion) => result && motion.IsActive (velocity, gravity, positioning));

  public override bool ComposeFromOptional (bool optionalResult, Godot.Vector2 velocity, GravityType? gravity,
    Positioning? positioning) =>
    Motions.Aggregate (optionalResult, (result, motion) => result && motion.IsActive (velocity, gravity, positioning));

  public override bool Compose (Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning) =>
    ComposeFromRequired (true, velocity, gravity, positioning);

  public override string ToString() => "Required: " + base.ToString();
}

public class CompositeOptionalMotionWrapper : AbstractCompositeMotionWrapper
{
  private readonly IEnumerable <Motion> _allowed;
  private readonly IEnumerable <Motion> _disallowed;

  public CompositeOptionalMotionWrapper (IEnumerable <Motion> motions) : base (new List <Motion> (motions) { Motion.None })
  {
    _allowed = Values.Where (x => Motions.Any (y => x == y || Mapping[y].Any (z => Mapping[x].All (a => a == z))));

    _disallowed = Values.Where (x =>
      Motions.All (y => x != y && Mapping[y].All (z => x != Motion.None && Mapping[x].All (a => a != z))));
  }

  public override IEnumerable <Motion> Allowed() => _allowed;
  public override IEnumerable <Motion> Disallowed() => _disallowed;

  public override bool ComposeFromRequired (bool requiredResult, Godot.Vector2 velocity, GravityType? gravity,
    Positioning? positioning) =>
    requiredResult;

  public override bool ComposeFromOptional (bool optionalResult, Godot.Vector2 velocity, GravityType? gravity,
    Positioning? positioning) =>
    Motions.Aggregate (optionalResult, (result, motion) => result || motion.IsActive (velocity, gravity, positioning));

  public override bool Compose (Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning) =>
    ComposeFromOptional (false, velocity, gravity, positioning);

  public override string ToString() => "Optional: " + base.ToString();
}

public class CompositeMotionWrapper : IMotionWrapper
{
  private readonly IMotionWrapper[] _wrappers;
  private readonly IEnumerable <Motion> _allowed;
  private readonly IEnumerable <Motion> _disallowed;

  public CompositeMotionWrapper (IMotionWrapper[] wrappers)
  {
    _wrappers = wrappers;
    _allowed = _wrappers.SelectMany (x => x.Allowed()).Distinct();
    _disallowed = _wrappers.SelectMany (x => x.Disallowed().Except (_wrappers.SelectMany (y => y.Allowed()))).Distinct();
  }

  public bool ComposeFromRequired (bool requiredResult, Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning) =>
    _wrappers.Aggregate (requiredResult, (result, wrapper) => wrapper.ComposeFromRequired (result, velocity, gravity, positioning)) &&
    _disallowed.All (x => !x.IsActive (velocity, gravity, positioning));

  public bool ComposeFromOptional (bool optionalResult, Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning) =>
    _wrappers.Aggregate (optionalResult, (result, wrapper) => wrapper.ComposeFromOptional (result, velocity, gravity, positioning)) &&
    _disallowed.All (x => !x.IsActive (velocity, gravity, positioning));

  public bool Compose (Godot.Vector2 velocity, GravityType? gravity, Positioning? positioning) =>
    _wrappers[0].Compose (velocity, gravity, positioning) && _disallowed.All (x => !x.IsActive (velocity, gravity, positioning));

  public IEnumerable <Motion> Allowed() => _allowed;
  public IEnumerable <Motion> Disallowed() => _disallowed;
  public override string ToString() => "Composite: " + Tools.ToString (_wrappers);
}