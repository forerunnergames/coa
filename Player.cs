using Godot;

public class Player : KinematicBody2D
{
  [Export] public int HorizontalSpeed = 800;
  [Export] public int VerticalSpeed = 800;
  [Export] public int HorizontalCliffClimbingSpeed = 200;
  [Export] public int VerticalCliffClimbingSpeed = 200;

  // ReSharper disable once UnusedMember.Global
  // ReSharper disable once MemberCanBePrivate.Global
  // ReSharper disable once UnassignedField.Global
  public bool IsClimbingCliffs // Field is assigned from Cliffs.cs
  {
    get { return _isClimbingCliffs; }
    set
    {
      _wasClimbingCliffs = _isClimbingCliffs && !value;
      _wasNotClimbingCliffs = !_isClimbingCliffs && value;
      _isClimbingCliffs = value;
    }
  }

  private bool _isClimbingCliffs;
  private bool _wasClimbingCliffs;
  private bool _wasNotClimbingCliffs;

  private Vector2 _velocity;
  private int _oldVerticalSpeed;
  private int _oldHorizontalSpeed;

  private void GetInput()
  {
    _velocity = new Vector2();

    if (Input.IsActionPressed ("ui_right"))
    {
      _velocity.x += 1;
    }
    else if (Input.IsActionPressed ("ui_left"))
    {
      _velocity.x -= 1;
    }
    else if (Input.IsActionPressed ("ui_up"))
    {
      _velocity.y -= 1;
    }
    else if (Input.IsActionPressed ("ui_down"))
    {
      _velocity.y += 1;
    }

    _velocity = _velocity.Normalized();
    _velocity.x *= HorizontalSpeed;
    _velocity.y *= VerticalSpeed;
  }

  public override void _PhysicsProcess (float delta)
  {
    GetInput();
    MoveAndCollide (_velocity * delta);

    if (_wasNotClimbingCliffs && IsClimbingCliffs)
    {
      _oldVerticalSpeed = VerticalSpeed;
      _oldHorizontalSpeed = HorizontalSpeed;
      VerticalSpeed = VerticalCliffClimbingSpeed;
      HorizontalSpeed = HorizontalCliffClimbingSpeed;
      _wasNotClimbingCliffs = false;
    }
    else if (_wasClimbingCliffs && !IsClimbingCliffs)
    {
      VerticalSpeed = _oldVerticalSpeed;
      HorizontalSpeed = _oldHorizontalSpeed;
      _wasClimbingCliffs = false;
    }
  }
}