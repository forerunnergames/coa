public static class Gravity
{
  public static readonly Godot.Vector2 GravityForce = new(0.0f, 30.0f);

  public enum GravityType
  {
    BeforeApplied,
    AfterApplied,
    None
  }

  public static Godot.Vector2? ApplyBefore (this GravityType gravityType, Godot.Vector2? velocity)
  {
    return gravityType switch
    {
      GravityType.None => velocity.HasValue ? Godot.Vector2.Zero : null,
      GravityType.BeforeApplied => velocity.HasValue ? velocity.Value + GravityForce : null,
      GravityType.AfterApplied => velocity,
      _ => null
    };
  }


  public static Godot.Vector2? ApplyAfter (this GravityType gravityType, Godot.Vector2? velocity)
  {
    return gravityType switch
    {
      GravityType.None => velocity,
      GravityType.BeforeApplied => velocity,
      GravityType.AfterApplied => velocity.HasValue ? velocity.Value + GravityForce : null,
      _ => null
    };
  }
}