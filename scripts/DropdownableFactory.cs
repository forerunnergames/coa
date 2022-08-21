using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;

public static class DropdownableFactory
{
  private static readonly IDropdownable NullDropdownable = null!;

  [SuppressMessage ("ReSharper", "ExplicitCallerInfoArgument")]
  public static IDropdownable Create (CollisionObject2D droppingNode, Node2D droppingThroughNode, Vector2 collisionPoint,
    [CallerFilePath] string name = "") =>
    droppingThroughNode switch
    {
      PhysicsBody2D physicsBody => new PhysicsBodyDropdownable (physicsBody, droppingNode, name),
      TileMap tileMap => new TileMapDropdownable (tileMap, droppingNode, collisionPoint, name),
      _ => NullDropdownable ?? new NullDropdownable (name)
    };
}