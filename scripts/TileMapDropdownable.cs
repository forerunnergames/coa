using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;

public class TileMapDropdownable : AbstractDropdownable
{
  private readonly TileMap _droppingThroughTileMap;
  private readonly CollisionObject2D _droppingNode;
  private readonly Vector2 _collisionPoint;

  // ReSharper disable once ExplicitCallerInfoArgument
  public TileMapDropdownable (TileMap droppingThroughTileMap, CollisionObject2D droppingNode, Vector2 collisionPoint,
    Dictionary <string, int> tileNamesToDropdownFrames, [CallerFilePath] string name = "") : base (tileNamesToDropdownFrames, name)
  {
    _droppingThroughTileMap = droppingThroughTileMap;
    _droppingNode = droppingNode;
    _collisionPoint = collisionPoint;
  }

  public override async Task Drop()
  {
    var droppingThroughTileName = Tools.GetCollidingTileName (_collisionPoint, _droppingThroughTileMap);

    if (droppingThroughTileName.Empty()) return;

    _IsDropping = true;
    Log.Info ($"Dropping down through tile [{droppingThroughTileName}].");
    _droppingThroughTileMap.SetCollisionMaskBit (0, false);
    _droppingNode.SetCollisionMaskBit (1, false);

    await IdleFrames (droppingThroughTileName, _droppingNode);

    _droppingThroughTileMap.SetCollisionMaskBit (0, true);
    _droppingNode.SetCollisionMaskBit (1, true);
    _IsDropping = false;
  }
}