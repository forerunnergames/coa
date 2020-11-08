using Godot;

public class Cliffs : Area2D
{
  [Export] public string PlayerColliderName;
  private Rect2 _playerRect;
  private Rect2 _cliffsRect;
  private bool _isPlayerIntersectingCliffs;
  private Area2D _playerArea;
  private Vector2 _playerExtents;
  private Vector2 _cliffsExtents;
  private Vector2 _playerPosition;
  private Vector2 _cliffsPosition;
  private CollisionShape2D _cliffsCollider;

  public override void _Ready()
  {
    _cliffsCollider = GetNode <CollisionShape2D> ("CollisionShape2D");
    _cliffsPosition = _cliffsCollider.GlobalPosition;
  }

  // ReSharper disable once UnusedMember.Global
  public void _on_cliffs_area_entered (Area2D area)
  {
    if (area.Name != PlayerColliderName) return;

    _playerArea = area;
    _isPlayerIntersectingCliffs = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _on_cliffs_area_exited (Area2D area)
  {
    if (area.Name != PlayerColliderName) return;

    _playerArea = area;
    _isPlayerIntersectingCliffs = false;
  }

  public override void _Process (float delta)
  {
    Update();

    if (_playerArea == null) return;

    _playerExtents = GetExtents (_playerArea);
    _cliffsExtents = GetExtents (this);
    _playerPosition = _playerArea.GlobalPosition;
    _cliffsPosition = _cliffsCollider.GlobalPosition;

    _playerRect.Position = _playerPosition - _playerExtents;
    _playerRect.Size = _playerExtents * 2;
    _cliffsRect.Position = _cliffsPosition - _cliffsExtents;
    _cliffsRect.Size = _cliffsExtents * 2;

    GetNode <KinematicBody2D> ("../Player")
      .Set ("IsClimbingCliffs", _isPlayerIntersectingCliffs && _cliffsRect.Encloses (_playerRect));
  }

  private Vector2 GetExtents (Area2D area)
  {
    var collisionShape = area.GetNode <CollisionShape2D> ("CollisionShape2D");
    var collisionRect = collisionShape.Shape as RectangleShape2D;

    if (collisionRect == null)
    {
      OnWrongCollisionShape (area, collisionShape.Shape);
      return Vector2.Zero;
    }

    return collisionRect.Extents;
  }

  private static void OnWrongCollisionShape (Area2D area, Shape2D shape)
  {
    GD.PrintErr (area.Name + " collision shape must be a " + typeof (RectangleShape2D) +
                 ", not a " + shape.GetType());
  }
}