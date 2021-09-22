public static class Positionings
{
  public enum Positioning
  {
    Air,
    Ground
  }

  public static Positioning GetPositioning (Godot.KinematicBody2D body) => body.IsOnFloor() ? Positioning.Ground : Positioning.Air;

  public static bool IsActive (this Positioning positioning, Godot.KinematicBody2D body) =>
    positioning switch
    {
      Positioning.Air => !body.IsOnFloor(),
      Positioning.Ground => body.IsOnFloor(),
      _ => false
    };
}