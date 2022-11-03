using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;

public class Dropdown : AbstractDropdownable
{
  private readonly GroundColliderComparer _groundColliderComparer = new();
  private readonly CollisionObject2D _droppingNode;
  private readonly List <RayCast2D> _groundDetectors;
  private readonly List <Task> _tasks = new();
  private readonly List <TileMap> _tileMapRayCastExceptions = new();
  private readonly string _name;

  // ReSharper disable once ExplicitCallerInfoArgument
  public Dropdown (CollisionObject2D droppingNode, List <RayCast2D> groundDetectors, [CallerFilePath] string name = "") : base (
    new Dictionary <string, int>(), name)
  {
    _droppingNode = droppingNode;
    _groundDetectors = groundDetectors;
    _name = name;
  }

  public override async Task Drop()
  {
    _IsDropping = false;
    _tasks.Clear();
    _tileMapRayCastExceptions.Clear();
    var touchingGroundColliders = GetTouchingGroundColliders();

    while (touchingGroundColliders.Count > 0)
    {
      foreach (var (groundCollider, collisionPoint) in touchingGroundColliders)
      {
        // Workaround for https://github.com/godotengine/godot/issues/17090 "Collision exceptions don't work with TileMap node"
        if (groundCollider is TileMap tileMap) _tileMapRayCastExceptions.Add (tileMap);

        _groundDetectors.ForEach (x =>
        {
          x.AddException (groundCollider);
          x.ForceRaycastUpdate();
        });

        if (!groundCollider.IsInGroup ("Dropdownable")) continue;

        // ReSharper disable once ExplicitCallerInfoArgument
        var dropdownable = DropdownableFactory.Create (_droppingNode, groundCollider, collisionPoint, _name);
        _tasks.Add (dropdownable.Drop());
        if (dropdownable.IsDropping()) _IsDropping = true;
      }

      touchingGroundColliders = GetTouchingGroundColliders (_tileMapRayCastExceptions);
    }

    _groundDetectors.ForEach (x => x.ClearExceptions());
    await Task.WhenAll (_tasks);
    _IsDropping = false;
  }

  private List <Tuple <Node2D, Vector2>> GetTouchingGroundColliders (ICollection <TileMap> tileMapExceptions = null) =>
    _groundDetectors
      .Where (x => x.GetCollider() is StaticBody2D ||
                   x.GetCollider() is TileMap tileMap && (!tileMapExceptions?.Contains (tileMap) ?? true))
      .Select (y => Tuple.Create (y.GetCollider() as Node2D, y.GetCollisionPoint())).Distinct (_groundColliderComparer).ToList();

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