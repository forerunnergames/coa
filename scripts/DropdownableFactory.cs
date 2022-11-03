using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;

public static class DropdownableFactory
{
  private static IDropdownable _nullDropdownable = null!;

  // @formatter:off

  private static readonly Dictionary <string, int> DroppingThroughNamesToDropdownFrames = new()
  {
    { "sign", 16 },
    { "sign-arrow-right", 17 },
    { "sign-arrow-left", 17 },
    { "bridge-post", 1 },
    { "Cabin Fence", 15},
    { "Cabin Stairs Top", 8},
    { "Cabin Stairs Middle", 8},
    { "Cabin Stairs Bottom", 11}
  };

  // @formatter:on

  [SuppressMessage ("ReSharper", "ExplicitCallerInfoArgument")]
  public static IDropdownable Create (CollisionObject2D droppingNode, Node2D droppingThroughNode, Vector2 collisionPoint,
    [CallerFilePath] string name = "") =>
    droppingThroughNode switch
    {
      PhysicsBody2D physicsBody => new PhysicsBodyDropdownable (physicsBody, droppingNode, DroppingThroughNamesToDropdownFrames, name),
      TileMap tileMap => new TileMapDropdownable (tileMap, droppingNode, collisionPoint, DroppingThroughNamesToDropdownFrames, name),
      _ => _nullDropdownable ??= new NullDropdownable (name)
    };
}