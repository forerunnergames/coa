using System.Collections.Generic;
using System.Linq;
using Godot;
using static Tools;

// ReSharper disable once UnusedType.Global
public class Bridge : Node2D
{
  private readonly RandomNumberGenerator _rng = new();
  private Sprite _piece1;
  private Sprite _piece2;
  private Sprite _piece3;
  private RigidBody2D _piece3Body;
  private List <Sprite> _pieces;
  private Vector2 _appliedForce = Vector2.Zero;
  private static readonly Vector2 LinearVelocity = new(1000, 0);

  public override void _Ready()
  {
    _rng.Randomize();
    _piece1 = GetNode <Sprite> ("Bridge Piece RigidBody 1/Sprite");
    _piece2 = GetNode <Sprite> ("Bridge Piece RigidBody 2/Sprite");
    _piece3 = GetNode <Sprite> ("Bridge Piece RigidBody 3/Sprite");
    _piece3Body = _piece3.GetParent <RigidBody2D>();
    _pieces = new List <Sprite> { _piece1, _piece2, _piece3 };
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnAppliedForceTimerTimeout()
  {
    _appliedForce.x = _rng.RandiRange (0, 200) * (Name.Contains ("Right") ? -1 : 1);
    _piece3Body.ApplyImpulse (Vector2.Zero, _appliedForce);
  }

  // TODO Remove. For debugging purposes only.
  public override void _UnhandledInput (InputEvent @event)
  {
    if (!WasMouseLeftClicked (@event)) return;

    var clickedPiece = _pieces.FirstOrDefault (piece => MouseInSprite (piece, GetMousePositionInSpriteSpace (piece)));

    if (clickedPiece == null) return;

    var isLeftBridge = clickedPiece.GetParent <RigidBody2D>().GetParent().Name == "Broken Bridge Left - Dynamic";
    clickedPiece.GetParent <RigidBody2D>().LinearVelocity = isLeftBridge ? LinearVelocity : -LinearVelocity;
  }
}