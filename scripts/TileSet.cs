using System.Collections.Generic;
using System.Linq;
using Godot;

[Tool]
public class TileSet : Godot.TileSet
{
  private enum Tile
  {
    Cliff = 0,
    CliffGem = 1
  }

  private readonly Dictionary <Tile, Tile[]> _binds = new()
  {
    { Tile.Cliff, new[] { Tile.CliffGem } }, { Tile.CliffGem, new[] { Tile.Cliff } }
  };

  public override bool _IsTileBound (int id, int nid) { return _binds[(Tile)id].Contains ((Tile)nid); }
}