using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public class Dropdown : AbstractDropdownable
{
  private readonly GroundColliderComparer _groundColliderComparer = new();
  private readonly CollisionObject2D _droppingNode;
  private readonly List <RayCast2D> _groundDetectors;

  public Dropdown (CollisionObject2D droppingNode, List <RayCast2D> groundDetectors)
  {
    _droppingNode = droppingNode;
    _groundDetectors = groundDetectors;
  }

  public override async Task Drop()
  {
    _IsDropping = false;
    List <Task> tasks = new();

    foreach (var (groundCollider, collisionPoint) in GetTouchingGroundColliders())
    {
      if (!groundCollider.IsInGroup ("Dropdownable")) continue;

      var dropdownable = DropdownableFactory.Create (_droppingNode, groundCollider, collisionPoint);
      tasks.Add (dropdownable.Drop());
      if (dropdownable.IsDropping()) _IsDropping = true;
    }

    await Task.WhenAll (tasks);
    _IsDropping = false;
  }

  private List <Tuple <Node2D, Vector2>> GetTouchingGroundColliders() =>
    _groundDetectors.Where (x => x.GetCollider() is StaticBody2D or TileMap)
      .Select (z => Tuple.Create (z.GetCollider() as Node2D, z.GetCollisionPoint())).Distinct (_groundColliderComparer).ToList();

  private class GroundColliderComparer : IEqualityComparer <Tuple <Node2D, Vector2>>
  {
    public bool Equals (Tuple <Node2D, Vector2> x, Tuple <Node2D, Vector2> y)
    {
      if (ReferenceEquals (x, y)) return true;
      if (ReferenceEquals (x, null)) return false;
      if (ReferenceEquals (y, null)) return false;

      return x.GetType() == y.GetType() && x.Item1.Equals (y.Item1);
    }

    public int GetHashCode (Tuple <Node2D, Vector2> obj) => obj.Item1.GetHashCode();
  }
}