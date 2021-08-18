using System.Collections.Generic;
using Godot;

public class TilePerch : AbstractPerch
{
  private readonly Vector2 _tileCell;
  private readonly string _tileMapName;

  public TilePerch (string tileMapName, string tileName, Vector2 globalOrigin, Vector2 cell, PerchableDrawPrefs drawPrefs,
    List <Rect2> perchableAreasInTileSpace, float positionEpsilon) : base (tileName, globalOrigin, drawPrefs,
    perchableAreasInTileSpace, positionEpsilon)
  {
    _tileMapName = tileMapName;
    _tileCell = cell;
  }

  public override bool HasParentName (string name) => _tileMapName == name;

  public override string ToString() =>
    $"[Tile map name: [{_tileMapName}], Tile Name: [{Name}], Global Origin: [{GlobalOrigin}], Cell: [{_tileCell}], " +
    $"Disabled: [{Disabled}], FlippedHorizontally: {FlippedHorizontally}, FlippedVertically: {FlippedVertically}, Global Areas:" +
    $"\n{PerchableAreasInGlobalSpaceString}]";
}