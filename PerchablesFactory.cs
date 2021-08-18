using System;
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
    { "cliffs-sign", new List <Rect2> {
        new(new Vector2 (8, 24), new Vector2 (8, 8)),
        new(new Vector2 (16, 16), new Vector2 (8, 8)),
        new(new Vector2 (24, 8), new Vector2 (72, 8)),
        new(new Vector2 (96, 16), new Vector2 (24, 8))}},
    { "Player", new List <Rect2> {
        new(new Vector2 (-40, -16), new Vector2 (8, 8)),
        new(new Vector2 (-32, -40), new Vector2 (8, 8)),
        new(new Vector2 (-24, -48), new Vector2 (40, 8)),
        new(new Vector2 (24, -48), new Vector2 (24, 8)),
        new(new Vector2 (50, -40), new Vector2 (8, 8)),
        new(new Vector2 (58, -32), new Vector2 (8, 8))}}
  };
  // @formatter:on

  public static IEnumerable <IPerchable> Create (Node2D node, PerchableDrawPrefs drawPrefs, float positionEpsilon)
  {
    return node switch
    {
      KinematicBody2D => new List <IPerchable>
      {
        new KinematicPerch (node.Name, new Vector2 (node.Position), drawPrefs, new List <Rect2> (NamesToPerchableAreas[node.Name]),
          positionEpsilon)
      },
      TileMap tileMap => new List <IPerchable> (tileMap.GetUsedCells().Cast <Vector2>()
        .Select (cell => new { cell, tileName = GetTileCellName (cell, tileMap) }).Select (@t =>
          new TilePerch (tileMap.Name, @t.tileName, GetTileCellGlobalPosition (@t.cell, tileMap), @t.cell, drawPrefs,
            new List <Rect2> (NamesToPerchableAreas[@t.tileName]), positionEpsilon))),
      _ => throw new InvalidOperationException ($"Unsupported node type: {node}.")
    };
  }

  public static IPerchable CreateDefault (Node2D node, PerchableDrawPrefs drawPrefs, float positionEpsilon) =>
    new DefaultPerch (node.Position, drawPrefs, new List <Rect2> { new(node.ToLocal (node.Position), node.GlobalScale) },
      positionEpsilon);
}