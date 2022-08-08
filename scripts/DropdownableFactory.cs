using Godot;

public static class DropdownableFactory
{
  private static readonly IDropdownable NullDropdownable = new NullDropdownable();

  public static IDropdownable Create (CollisionObject2D droppingNode, Node2D droppingThroughNode, Vector2 collisionPoint) =>
    droppingThroughNode switch
    {
      PhysicsBody2D physicsBody => new PhysicsBodyDropdownable (physicsBody, droppingNode),
      TileMap tileMap => new TileMapDropdownable (tileMap, droppingNode, collisionPoint),
      _ => NullDropdownable
    };
}