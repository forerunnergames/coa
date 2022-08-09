using System.Collections.Generic;
using System.Linq;
using Godot;
using static Tools;

public static class PerchableFactory
{
  private static readonly Vector2 LocalScale = new(8, 8);

  // @formatter:off

  private static readonly Dictionary <string, List <Rect2>> NamesToPerchableAreas = new()
  {
    { "tree", new List <Rect2> {
        new(new Vector2 (8, 192), new Vector2 (16, 8)),
        new(new Vector2 (24, 184), new Vector2 (24, 8)),
        new(new Vector2 (40, 168), new Vector2 (8, 8)),
        new(new Vector2 (48, 160), new Vector2 (8, 8)),
        new(new Vector2 (56, 152), new Vector2 (16, 8)),
        new(new Vector2 (72, 128), new Vector2 (8, 8)),
        new(new Vector2 (72, 144), new Vector2 (8, 8)),
        new(new Vector2 (80, 120), new Vector2 (16, 8)),
        new(new Vector2 (88, 96), new Vector2 (8, 8)),
        new(new Vector2 (96, 64), new Vector2 (8, 8)),
        new(new Vector2 (96, 88), new Vector2 (8, 8)),
        new(new Vector2 (104, 24), new Vector2 (8, 8)),
        new(new Vector2 (104, 40), new Vector2 (8, 8)),
        new(new Vector2 (104, 56), new Vector2 (8, 8)),
        new(new Vector2 (104, 232), new Vector2 (8, 8)),
        new(new Vector2 (112, 0), new Vector2 (8, 8)),
        new(new Vector2 (120, 16), new Vector2 (8, 8)),
        new(new Vector2 (128, 40), new Vector2 (8, 8)),
        new(new Vector2 (136, 48), new Vector2 (8, 8)),
        new(new Vector2 (144, 56), new Vector2 (8, 8)),
        new(new Vector2 (144, 72), new Vector2 (8, 8)),
        new(new Vector2 (144, 96), new Vector2 (8, 8)),
        new(new Vector2 (152, 104), new Vector2 (16, 8)),
        new(new Vector2 (160, 128), new Vector2 (24, 8)),
        new(new Vector2 (168, 112), new Vector2 (8, 8)),
        new(new Vector2 (176, 152), new Vector2 (32, 8)),
        new(new Vector2 (184, 136), new Vector2 (8, 8)),
        new(new Vector2 (200, 176), new Vector2 (32, 8)),
        new(new Vector2 (208, 160), new Vector2 (8, 8)),
        new(new Vector2 (232, 184), new Vector2 (16, 8)),
        new(new Vector2 (248, 192), new Vector2 (8, 8))}},
    { "flowers-pink-pair-left", new List <Rect2> {
        new(new Vector2 (40, 40), new Vector2 (8, 16)),
        new(new Vector2 (48, 40), new Vector2 (16, 24)),
        new(new Vector2 (80, 48), new Vector2 (24, 24))}},
    { "flowers-pink-pair-right", new List <Rect2> {
        new(new Vector2 (24, 40), new Vector2 (24, 24)),
        new(new Vector2 (64, 32), new Vector2 (24, 24))}},
    { "flowers-pink-single-left", new List <Rect2> {
        new(new Vector2 (48, 40), new Vector2 (24, 24))}},
    { "sign", new List <Rect2> {
        new(new Vector2 (8, 24), new Vector2 (8, 8)),
        new(new Vector2 (16, 16), new Vector2 (8, 8)),
        new(new Vector2 (24, 8), new Vector2 (72, 8)),
        new(new Vector2 (96, 16), new Vector2 (24, 8))}},
    { "sign-arrow-left", new List <Rect2> {
        new(new Vector2 (0, 32), new Vector2 (8, 8)),
        new(new Vector2 (8, 24), new Vector2 (8, 8)),
        new(new Vector2 (16, 16), new Vector2 (8, 8)),
        new(new Vector2 (24, 8), new Vector2 (8, 8)),
        new(new Vector2 (32, 0), new Vector2 (16, 8)),
        new(new Vector2 (48, 16), new Vector2 (72, 8)),
        new(new Vector2 (120, 24), new Vector2 (8, 8))}},
    { "sign-arrow-right", new List <Rect2> {
        new(new Vector2 (0, 24), new Vector2 (8, 8)),
        new(new Vector2 (8, 16), new Vector2 (72, 8)),
        new(new Vector2 (80, 0), new Vector2 (16, 8)),
        new(new Vector2 (96, 8), new Vector2 (8, 8)),
        new(new Vector2 (104, 16), new Vector2 (8, 8)),
        new(new Vector2 (112, 24), new Vector2 (8, 8)),
        new(new Vector2 (120, 32), new Vector2 (8, 8))}},
    { "bridge-post", new List <Rect2> {
        new(new Vector2 (8, 0), new Vector2 (16, 8))}},
    { "player_idle_left", new List <Rect2> {
        new(new Vector2 (-40, -16), new Vector2 (8, 8)),
        new(new Vector2 (-32, -40), new Vector2 (8, 8)),
        new(new Vector2 (-24, -48), new Vector2 (40, 8)),
        new(new Vector2 (24, -48), new Vector2 (24, 8)),
        new(new Vector2 (48, -40), new Vector2 (8, 8)),
        new(new Vector2 (56, -32), new Vector2 (8, 8))}},
    { "player_cliff_hanging", new List <Rect2> {
        new(new Vector2 (-40, -64), new Vector2 (24, 8)),
        new(new Vector2 (-16, -56), new Vector2 (24, 8)),
        new(new Vector2 (8, -64), new Vector2 (24, 8))}}
  };

  // @formatter:on

  public static IEnumerable <IPerchable> Create (Node2D node, PerchableDrawPrefs drawPrefs, float positionEpsilon) =>
    node switch
    {
      AnimatedSprite sprite => new List <IPerchable> (from string animationName in sprite.Frames.GetAnimationNames()
        where NamesToPerchableAreas.ContainsKey (animationName)
        select new AnimatedSpritePerch (animationName, LocalScale, new Vector2 (node.GlobalScale), new Vector2 (node.GlobalPosition),
          drawPrefs, new List <Rect2> (NamesToPerchableAreas[animationName]), positionEpsilon)),
      TileMap tileMap => new List <IPerchable> (tileMap.GetUsedCells().Cast <Vector2>()
        .Select (cell => new { cell, tileName = GetTileName (cell, tileMap) }).Select (t => new TilePerch (tileMap.Name, t.tileName,
          LocalScale, node.GlobalScale, GetTileCellGlobalOrigin (t.cell, tileMap), t.cell,
          tileMap.IsCellXFlipped ((int)t.cell.x, (int)t.cell.y), tileMap.IsCellYFlipped ((int)t.cell.x, (int)t.cell.y), drawPrefs,
          new List <Rect2> (NamesToPerchableAreas[t.tileName]), positionEpsilon))),
      _ => new List <IPerchable>()
    };

  public static IPerchable CreateDefault (Node2D node, PerchableDrawPrefs drawPrefs, float positionEpsilon) =>
    new DefaultPerch (LocalScale, node.GlobalScale, node.Position, drawPrefs,
      new List <Rect2> { new(node.ToLocal (node.Position), node.GlobalScale) }, positionEpsilon);
}