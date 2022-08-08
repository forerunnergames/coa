using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public class TileMapDropdownable : AbstractDropdownable
{
  private readonly TileMap _droppingThroughTileMap;
  private readonly CollisionObject2D _droppingNode;
  private readonly Vector2 _collisionPoint;

  // @formatter:off

  private static readonly Dictionary <string, int> TileNamesToDropdownFrames = new()
  {
    { "sign", 16 },
    { "sign-arrow-right", 17 },
    { "sign-arrow-left", 17 },
    { "bridge-post", 1 }
  };

  // @formatter:on

  public TileMapDropdownable (TileMap droppingThroughTileMap, CollisionObject2D droppingNode, Vector2 collisionPoint)
  {
    _droppingThroughTileMap = droppingThroughTileMap;
    _droppingNode = droppingNode;
    _collisionPoint = collisionPoint;
  }

  public override async Task Drop()
  {
    var tileName = Tools.GetCollidingTileName (_collisionPoint, _droppingThroughTileMap);

    if (tileName.Empty()) return;

    _IsDropping = true;
    Log.Info ($"Dropping down through [{tileName}].");
    _droppingThroughTileMap.SetCollisionMaskBit (0, false);
    _droppingNode.SetCollisionMaskBit (1, false);
    for (var i = 0; i < TileNamesToDropdownFrames[tileName]; ++i) await _droppingNode.ToSignal (_droppingNode.GetTree(), "idle_frame");
    _droppingThroughTileMap.SetCollisionMaskBit (0, true);
    _droppingNode.SetCollisionMaskBit (1, true);
    _IsDropping = false;
  }
}