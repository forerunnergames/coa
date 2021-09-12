using System.Collections.Generic;
using System.Linq;
using Godot;
using static Tools;

public static class PerchablesFactory
{
  // @formatter:off

  private static readonly Dictionary <string, List <Rect2>> NamesToPerchableAreas = new()
  {
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

  public static IEnumerable <IPerchable> Create (Node2D node, PerchableDrawPrefs drawPrefs, float positionEpsilon)
  {
    return node switch
    {
      AnimatedSprite sprite => new List <IPerchable> (from string animationName in sprite.Frames.GetAnimationNames()
        where NamesToPerchableAreas.ContainsKey (animationName)
        select new AnimatedSpritePerch (animationName, new Vector2 (node.GlobalPosition), drawPrefs,
          new List <Rect2> (NamesToPerchableAreas[animationName]), positionEpsilon)),
      TileMap tileMap => new List <IPerchable> (tileMap.GetUsedCells().Cast <Vector2>()
        .Select (cell => new { cell, tileName = GetTileName (cell, tileMap) }).Select (@t => new TilePerch (tileMap.Name, @t.tileName,
          GetTileCellGlobalPosition (@t.cell, tileMap), @t.cell, drawPrefs, new List <Rect2> (NamesToPerchableAreas[@t.tileName]),
          positionEpsilon))),
      _ => new List <IPerchable>()
    };
  }

  public static IPerchable CreateDefault (Node2D node, PerchableDrawPrefs drawPrefs, float positionEpsilon) =>
    new DefaultPerch (node.Position, drawPrefs, new List <Rect2> { new(node.ToLocal (node.Position), node.GlobalScale) },
      positionEpsilon);
}