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
  public static IMotionWrapper Required (params Motion[] motions) => new RequiredMotionWrapper (motions);
  public static IMotionWrapper Optional (params Motion[] motions) => new OptionalMotionWrapper (new List <Motion> (motions) { Motion.None });
  public static IMotionWrapper[] _ (params IMotionWrapper[] wrappers) => new IMotionWrapper[] { new CompositeMotionWrapper (wrappers) };
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

  public readonly struct PhysicsBodyData
  {
    public PhysicsBodyData (Godot.Vector2? velocity, GravityType? gravity, Positioning? positioning)
    {
      Velocity = velocity;
      Gravity = gravity;
      Positioning = positioning;
    }

    public Godot.Vector2? Velocity { get; }
    public GravityType? Gravity { get; }
    public Positioning? Positioning { get; }
  }

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
  public static bool IsActive (this Motion motion, ref PhysicsBodyData body)
  {
    if (!body.Velocity.HasValue) return false;

    return body.Gravity switch
    {
      GravityType.BeforeApplied => motion switch
      {
        Motion.None => body.Positioning is Positioning.Ground && AreAlmostEqual (body.Velocity.Value, Godot.Vector2.Zero, VelocityEpsilon) || body.Positioning is Positioning.Air && AreAlmostEqual (body.Velocity.Value, -GravityForce, VelocityEpsilon),
        Motion.Any => body.Positioning is Positioning.Ground && !AreAlmostEqual (body.Velocity.Value, Godot.Vector2.Zero, VelocityEpsilon) || body.Positioning is Positioning.Air && !AreAlmostEqual (body.Velocity.Value, -GravityForce, VelocityEpsilon),
        Motion.Up => body.Velocity.Value.y + GravityForce.y < -VelocityEpsilon,
        Motion.Down => body.Positioning is Positioning.Ground && body.Velocity.Value.y > VelocityEpsilon || body.Positioning is not Positioning.Ground && body.Velocity.Value.y + GravityForce.y > VelocityEpsilon,
        Motion.Left => body.Velocity.Value.x < -VelocityEpsilon,
        Motion.Right => body.Velocity.Value.x > VelocityEpsilon,
        Motion.Vertical => Motion.Up.IsActive (ref body) || Motion.Down.IsActive (ref body),
        Motion.Horizontal => Motion.Left.IsActive (ref body) || Motion.Right.IsActive (ref body),
        _ => false
      },
      GravityType.AfterApplied => motion switch
      {
        Motion.None => body.Positioning is Positioning.Ground && AreAlmostEqual (body.Velocity.Value, GravityForce, VelocityEpsilon) || body.Positioning is Positioning.Air && AreAlmostEqual (body.Velocity.Value, Godot.Vector2.Zero, VelocityEpsilon),
        Motion.Any => body.Positioning is Positioning.Ground && !AreAlmostEqual (body.Velocity.Value, GravityForce, VelocityEpsilon) || body.Positioning is Positioning.Air && !AreAlmostEqual (body.Velocity.Value, Godot.Vector2.Zero, VelocityEpsilon),
        Motion.Up => body.Velocity.Value.y < -VelocityEpsilon,
        Motion.Down => body.Positioning is Positioning.Ground && body.Velocity.Value.y > GravityForce.y + VelocityEpsilon || body.Positioning is not Positioning.Ground && body.Velocity.Value.y > VelocityEpsilon,
        Motion.Left => body.Velocity.Value.x < -VelocityEpsilon,
        Motion.Right => body.Velocity.Value.x > VelocityEpsilon,
        Motion.Vertical => Motion.Up.IsActive (ref body) || Motion.Down.IsActive (ref body),
        Motion.Horizontal => Motion.Left.IsActive (ref body) || Motion.Right.IsActive (ref body),
        _ => false
      },
      GravityType.None => motion switch
      {
        Motion.None => AreAlmostEqual (body.Velocity.Value, Godot.Vector2.Zero, VelocityEpsilon),
        Motion.Any => !AreAlmostEqual (body.Velocity.Value, Godot.Vector2.Zero, VelocityEpsilon),
        Motion.Up => body.Velocity.Value.y < -VelocityEpsilon,
        Motion.Down => body.Velocity.Value.y > VelocityEpsilon,
        Motion.Left => body.Velocity.Value.x < -VelocityEpsilon,
        Motion.Right => body.Velocity.Value.x > VelocityEpsilon,
        Motion.Vertical => Math.Abs (body.Velocity.Value.y) > VelocityEpsilon,
        Motion.Horizontal => Math.Abs (body.Velocity.Value.x) > VelocityEpsilon,
        _ => false
      },
      _ => false
    };
  }
  // @formatter:on
}

public interface IMotionWrapper : IRequiredOptionalWrapper <Motion>
{
}

public class RequiredMotionWrapper : RequiredWrapper <Motion>, IMotionWrapper
{
  public RequiredMotionWrapper (IReadOnlyCollection <Motion> motions) : base (motions,
    Values().Where (x => motions.Any (y => x == y || y != Motion.None && Mapping[y].All (z => Mapping[x].Any (a => a == z)))),
    Values().Where (x => motions.All (y => x != y && Mapping[y].All (z => Mapping[x].All (a => a != z || y == Motion.Any)))))
  {
  }
}

public class OptionalMotionWrapper : OptionalWrapper <Motion>, IMotionWrapper
{
  public OptionalMotionWrapper (IReadOnlyCollection <Motion> motions) : base (motions,
    Values().Where (x => motions.Any (y => x == y || Mapping[y].Any (z => Mapping[x].All (a => a == z)))),
    Values().Where (x => motions.All (y => x != y && Mapping[y].All (z => x != Motion.None && Mapping[x].All (a => a != z)))))
  {
  }
}

public class CompositeMotionWrapper : CompositeRequiredOptionalWrapper <Motion>, IMotionWrapper
{
  public CompositeMotionWrapper (IEnumerable <IMotionWrapper> wrappers) : base (wrappers) { }
}