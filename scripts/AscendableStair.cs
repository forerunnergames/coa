using Godot;

// ReSharper disable once UnusedType.Global
public class AscendableStair : StaticBody2D, IAscendableStair
{
  private Rect2? _colliderRect;
  private Rect2 _otherColliderRect;
  private CollisionShape2D _collider;
  public override void _Ready() => _collider = GetChild <CollisionShape2D> (0);
  public Node AsNode() => this;

  public bool IsHorizontalCenterPointTouching (Rect2 colliderRect)
  {
    _otherColliderRect = _otherColliderRect.Position != colliderRect.GetCenter()
      ? new Rect2 (colliderRect.GetCenter(), new Vector2 (1, colliderRect.Size.y))
      : _otherColliderRect;

    _colliderRect ??= Tools.GetColliderRect (this, _collider);

    GD.Print ("original player collider rect: ", colliderRect, ", center: ", colliderRect.GetCenter(),
      ", proxy player collider rect: ", _otherColliderRect, ", center: ", _otherColliderRect.GetCenter(), ", stairs collider rect: ",
      _colliderRect, ", center: ", _colliderRect?.GetCenter());

    return (_otherColliderRect = _otherColliderRect.Position != colliderRect.GetCenter()
      ? new Rect2 (colliderRect.GetCenter(), new Vector2 (1, colliderRect.Size.y))
      : _otherColliderRect).Intersects (_colliderRect ??= Tools.GetColliderRect (this, _collider), true);
  }

  public void SetMode (IAscendableStair.Mode mode) =>
    (_collider.RotationDegrees, _collider.OneWayCollision) = mode switch
    {
      IAscendableStair.Mode.Ascending or IAscendableStair.Mode.Idle => (0, true),
      IAscendableStair.Mode.Descending => (180, true),
      _ => (_collider.RotationDegrees, _collider.OneWayCollision)
    };
}