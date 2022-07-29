using System.Collections.Generic;
using Godot;

public class TilePerch : AbstractPerch
{
  private readonly Vector2 _tileCell;
  private readonly string _tileMapName;

  public TilePerch (string tileMapName, string tileName, Vector2 localScale, Vector2 globalScale, Vector2 globalOrigin, Vector2 cell,
    bool shouldFlipHorizontally, bool shouldFlipVertically, PerchableDrawPrefs drawPrefs, List <Rect2> perchableAreasInTileSpace,
    float positionEpsilon) : base (tileName, localScale, globalScale, globalOrigin, drawPrefs, perchableAreasInTileSpace,
    positionEpsilon)
  {
    _tileMapName = tileMapName;
    _tileCell = cell;
    if (shouldFlipHorizontally) FlipHorizontally();
    if (shouldFlipVertically) FlipVertically();
  }

  public override bool HasParentName (string name) => _tileMapName == name;

  public override string ToString() =>
    $"[Tile map name: [{_tileMapName}], Tile Name: [{Name}], Global Origin: [{GlobalOrigin}], Cell: [{_tileCell}], " +
    $"Disabled: [{Disabled}], FlippedHorizontally: {FlippedHorizontally}, FlippedVertically: {FlippedVertically}, Global Areas:" +
    $"\n{PerchableAreasInGlobalSpaceString}]";
}