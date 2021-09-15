using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

// ReSharper disable MemberCanBePrivate.Global
public static class Tools
{
  public enum Input
  {
    Horizontal,
    Vertical,
    Item,
    Text,
    Respawn,
    Season,
    Music,
    Up,
    Down,
    Left,
    Right,
    Jump,
    Energy
  }

  private static readonly Dictionary <Input, string[]> Inputs = new()
  {
    { Input.Horizontal, new[] { "move_left", "move_right" } },
    { Input.Vertical, new[] { "move_up", "move_down" } },
    { Input.Item, new[] { "use_item" } },
    { Input.Text, new[] { "show_text" } },
    { Input.Respawn, new[] { "respawn" } },
    { Input.Season, new[] { "season" } },
    { Input.Music, new[] { "music" } },
    { Input.Up, new[] { "move_up", "read_sign" } },
    { Input.Down, new[] { "move_down" } },
    { Input.Left, new[] { "move_left" } },
    { Input.Right, new[] { "move_right" } },
    { Input.Jump, new[] { "jump" } },
    { Input.Energy, new[] { "energy" } }
  };

  public delegate void DrawPrimitive (Vector2[] points, Color[] colors, Vector2[] uvs);
  public delegate void DrawRect (Rect2 rect, Color color, bool filled);
  public delegate Transform GetLocalTransform();
  public delegate Vector2 Transform (Vector2 point);
  public delegate Vector2 GetGlobalScale();
  public static bool IsReleased (Input i, InputEvent e) => e is InputEventKey k && Inputs[i].Any (x => k.IsActionReleased (x));
  public static bool IsPressed (Input i, InputEvent e) => e is InputEventKey k && Inputs[i].Any (x => k.IsActionPressed (x));
  public static bool IsOneActiveOf (Input i) => Inputs[i].Where (Godot.Input.IsActionPressed).Take (2).Count() == 1;
  public static bool IsAnyActiveOf (Input i) => Inputs[i].Any (Godot.Input.IsActionPressed);
  public static bool IsLeftArrowPressed() => Godot.Input.IsActionPressed (Inputs[Input.Left][0]);
  public static bool WasLeftArrowPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Left][0]);
  public static bool IsRightArrowPressed() => Godot.Input.IsActionPressed (Inputs[Input.Right][0]);
  public static bool WasRightArrowPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Right][0]);
  public static bool IsUpArrowPressed() => Godot.Input.IsActionPressed (Inputs[Input.Up][0]);
  public static bool WasUpArrowReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Up][0]);
  public static bool WasUpArrowPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Up][0]);
  public static bool IsDownArrowPressed() => Godot.Input.IsActionPressed (Inputs[Input.Down][0]);
  public static bool WasDownArrowReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Down][0]);
  public static bool WasDownArrowPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Down][0]);
  public static bool IsAnyHorizontalArrowPressed() => IsLeftArrowPressed() || IsRightArrowPressed();
  public static bool IsEveryHorizontalArrowPressed() => IsLeftArrowPressed() && IsRightArrowPressed();
  public static bool IsAnyVerticalArrowPressed() => IsUpArrowPressed() || IsDownArrowPressed();
  public static bool IsEveryVerticalArrowPressed() => IsUpArrowPressed() && IsDownArrowPressed();
  public static bool IsAnyArrowKeyPressed() => IsAnyHorizontalArrowPressed() || IsAnyVerticalArrowPressed();
  public static bool IsItemKeyPressed() => Godot.Input.IsActionPressed (Inputs[Input.Item][0]);
  public static bool IsEnergyKeyPressed() => Godot.Input.IsActionPressed (Inputs[Input.Energy][0]);
  public static bool WasItemKeyReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Item][0]);
  public static bool WasJumpKeyPressed() => Godot.Input.IsActionJustPressed (Inputs[Input.Jump][0]);
  public static bool WasJumpKeyReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Jump][0]);
  public static float SafelyClampMin (float f, float min) => IsSafelyLessThan (f, min) ? min : f;
  public static float SafelyClampMax (float f, float max) => IsSafelyGreaterThan (f, max) ? max : f;
  public static float SafelyClamp (float f, float min, float max) => SafelyClampMin (SafelyClampMax (f, max), min);
  public static bool IsSafelyLessThan (float f1, float f2) => f1 < f2 && !Mathf.IsEqualApprox (f1, f2);
  public static bool IsSafelyGreaterThan (float f1, float f2) => f1 > f2 && !Mathf.IsEqualApprox (f1, f2);

  public static bool IsAnyArrowKeyPressedExcept (Input arrow)
  {
    var up = IsUpArrowPressed();
    var down = IsDownArrowPressed();
    var left = IsLeftArrowPressed();
    var right = IsRightArrowPressed();

    return arrow switch
    {
      Input.Horizontal => up || down,
      Input.Vertical => left || right,
      Input.Up => left || right || down,
      Input.Down => left || right || up,
      Input.Left => right || up || down,
      Input.Right => left || up || down,
      _ => false
    };
  }

  public static bool AreAlmostEqual (Color color1, Color color2, float epsilon)
  {
    return Mathf.Abs (color1.r - color2.r) < epsilon && Mathf.Abs (color1.g - color2.g) < epsilon &&
           Mathf.Abs (color1.b - color2.b) < epsilon && Mathf.Abs (color1.a - color2.a) < epsilon;
  }

  public static bool AreAlmostEqual (Vector2 vector1, Vector2 vector2, float epsilon)
  {
    return Mathf.Abs (vector1.x - vector2.x) < epsilon && Mathf.Abs (vector1.y - vector2.y) < epsilon;
  }

  public static bool AreAlmostEqual (float f1, float f2, float epsilon)
  {
    return Mathf.Abs (f1 - f2) < epsilon && Mathf.Abs (f1 - f2) < epsilon;
  }

  public static bool AlmostHasPoint (Rect2 rect, Vector2 point, float epsilon)
  {
    return (AreAlmostEqual (point.x, rect.Position.x, epsilon) || point.x > (double)rect.Position.x) &&
           (AreAlmostEqual (point.y, rect.Position.y, epsilon) || point.y > (double)rect.Position.y) &&
           point.x < rect.Position.x + (double)rect.Size.x && point.y < rect.Position.y + (double)rect.Size.y;
  }

  public static void LoopAudio (AudioStream a) { LoopAudio (a, 0.0f, a.GetLength()); }

  /// <summary>
  /// Whether or not the specified input type is the only active input.
  /// </summary>
  /// <param name="input"></param>
  /// <param name="disableExclusivity">if true, bypass exclusivity requirement for active input
  /// <br/>
  /// Useful when the desired action from the specified input is already being executed,
  /// since ignoring exclusivity prevents said action from being canceled when
  /// an excluded input becomes active.
  /// <br/>
  /// For example, holding the right arrow key down
  /// and running, and you want to disable jumping (space bar) while running. If the
  /// player is already running, and exclusivity of input is required, i.e., in order
  /// to run, ONLY the right arrow key may be pressed, then pressing the space bar will
  /// cancel the running because the input is no longer exclusive. In this case you would
  /// want to IGNORE exclusivity, continuing to run even when the space bar is pressed in
  /// addition to the right arrow key.
  /// <br/>
  /// Then as long as you implement the same thing for running as with jumping, then jumping
  /// will not work when running for the same reason; i.e., the space bar requires exclusivity.
  /// The exception for ignoring exclusions would be if already jumping; however, in this example
  /// the player is running, so exclusions would NOT be ignored for jumping, therefore jumping
  /// would be disabled while running because the right arrow key is already being pressed.
  /// </param>
  /// <returns></returns>
  public static bool IsExclusivelyActiveUnless (Input input, bool disableExclusivity)
  {
    var activeInclusions = new List <string>();
    var activeExclusions = new List <string>();

    foreach (var (key, values) in Inputs)
    {
      foreach (var value in values)
      {
        var isActionPressed = Godot.Input.IsActionPressed (value);

        if (key == input && isActionPressed) activeInclusions.Add (value);
        else if (isActionPressed) activeExclusions.Add (value);
      }
    }

    // If not ignoring exclusions, active exclusions must be unique; i.e., must not be an active inclusion.
    //   E.g., InputType.Vertical & InputType.Up both contain "move_up", so if the specified input type is
    //   Vertical and "move_up" is an active inclusion, then InputType.Up ["move_up"] will not count as an
    //   active exclusion, since it isn't unique (even though it is active).
    return activeInclusions.Any() && (disableExclusivity || !activeExclusions.Except (activeInclusions).Any());
  }

  public static void LoopAudio (AudioStream stream, float loopBeginSeconds, float loopEndSecondsWavOnly)
  {
    switch (stream)
    {
      case AudioStreamOGGVorbis ogg:
      {
        ogg.Loop = true;
        ogg.LoopOffset = loopBeginSeconds;

        break;
      }
      case AudioStreamSample wav:
      {
        wav.LoopMode = AudioStreamSample.LoopModeEnum.Forward;
        wav.LoopBegin = Mathf.RoundToInt (loopBeginSeconds * wav.MixRate);
        wav.LoopEnd = Mathf.RoundToInt (loopEndSecondsWavOnly * wav.MixRate);

        break;
      }
    }
  }

  public static Vector2 RandomPointIn (Rect2 rect, RandomNumberGenerator rng, Vector2 multiple)
  {
    var p = rect.Position - Vector2.One;

    // @formatter:off
    while (p.x < rect.Position.x || p.x % multiple.x != 0) p.x = rng.RandiRange (Mathf.RoundToInt (rect.Position.x), Mathf.RoundToInt (rect.End.x) - 1);
    while (p.y < rect.Position.y || p.y % multiple.y != 0) p.y = rng.RandiRange (Mathf.RoundToInt (rect.Position.y), Mathf.RoundToInt (rect.End.y) - 1);
    // @formatter:on

    return p;
  }

  public static Rect2 GetAreaColliderRect (Area2D area, CollisionShape2D collider)
  {
    if (!area.HasNode (collider.Name) || collider.Shape is not RectangleShape2D rect) return new Rect2();

    var extents = rect.Extents * area.GlobalScale.Abs();

    return new Rect2 (collider.GlobalPosition - extents, extents * 2);
  }

  public static string GetTileName (Vector2 cell, TileMap t)
  {
    var id = t.GetCellv (cell);

    return id != -1 ? t.TileSet.TileGetName (id) : "";
  }

  public static Vector2 GetTileCellAtCenterOf (Area2D area, CollisionShape2D collider, TileMap t)
  {
    var rect = GetAreaColliderRect (area, collider);

    return GetTileCellAtAnyOf (new List <Vector2> { t.WorldToMap (t.ToLocal (rect.Position + rect.Size / 2)) }, t);
  }

  public static Vector2 GetIntersectingTileCell (Area2D area, CollisionShape2D collider, TileMap t)
  {
    var rect = GetAreaColliderRect (area, collider);

    var cornerCells = new List <Vector2>
    {
      t.WorldToMap (t.ToLocal (rect.Position)),
      t.WorldToMap (t.ToLocal (new Vector2 (rect.End.x - 1, rect.Position.y))),
      t.WorldToMap (t.ToLocal (new Vector2 (rect.Position.x, rect.End.y - 1))),
      t.WorldToMap (t.ToLocal (rect.End - Vector2.One))
    };

    return GetTileCellAtAnyOf (cornerCells, t);
  }

  // @formatter:off
  public static bool LessThan (Vector2 v1, Vector2 v2) => v1.x < v2.x && v1.y < v2.y;
  public static bool GreaterThan (Vector2 v1, Vector2 v2) => v1.x > v2.x && v1.y > v2.y;
  public static bool LessThanOrEqual (Vector2 v1, Vector2 v2) => v1.x <= v2.x && v1.y <= v2.y;
  public static bool GreaterThanOrEqual (Vector2 v1, Vector2 v2) => v1.x >= v2.x && v1.y >= v2.y;
  public static bool IsEnclosedBy (Rect2 original, IReadOnlyCollection <Rect2> overlaps) => overlaps.Aggregate (new List <Rect2> { original }, (x, _) => GetUncoveredRects (x, overlaps)).Count == 0;
  public static bool IsEnclosedBy (Rect2 rect1, Rect2 rect2) => rect1.Position.x >= rect2.Position.x && rect1.End.x <= rect2.End.x && rect1.Position.y >= rect2.Position.y && rect1.End.y <= rect2.End.y;
  public static Vector2 GetTileCellAtCenterOf (Area2D area, string colliderName, TileMap t) => GetTileCellAtCenterOf (area, area.GetNode <CollisionShape2D> (colliderName), t);
  public static bool IsIntersectingAnyTile (Area2D area, string colliderName, TileMap t) => IsIntersectingAnyTile (area, area.GetNode <CollisionShape2D> (colliderName), t);
  public static bool IsIntersectingAnyTile (Area2D area, CollisionShape2D collider, TileMap t) => GetIntersectingTileId (area, collider, t) != -1;
  public static int GetIntersectingTileId (Area2D area, CollisionShape2D collider, TileMap t) => t.GetCellv (GetIntersectingTileCell (area, collider, t));
  public static string GetIntersectingTileName (Area2D area, CollisionShape2D collider, TileMap t) => GetTileName (GetIntersectingTileCell (area, collider, t), t);
  public static string GetIntersectingTileName (Area2D area, string colliderName, TileMap t) => GetIntersectingTileName (area, area.GetNode <CollisionShape2D> (colliderName), t);
  public static Vector2 GetIntersectingTileCell (Area2D area, string colliderName, TileMap t) => GetIntersectingTileCell (area, area.GetNode <CollisionShape2D> (colliderName), t);
  public static Vector2 GetTileCellGlobalPosition (Vector2 cell, TileMap t) => t.ToGlobal (t.MapToWorld (cell));
  public static Vector2 GetIntersectingTileCellGlobalPosition (Area2D area, CollisionShape2D collider, TileMap t) => GetTileCellGlobalPosition (GetIntersectingTileCell (area, collider, t), t);
  public static string ToString <T> (IEnumerable <T> e, string sep = ", ", Func <T, string> f = null) => e.Select (f ?? (s => s.ToString())).DefaultIfEmpty (string.Empty).Aggregate ((a, b) => a + sep + b);
  // @formatter:on

  private static Vector2 GetTileCellAtAnyOf (IReadOnlyCollection <Vector2> cells, TileMap t) =>
    t.GetUsedCells().Cast <Vector2>().FirstOrDefault (a =>
      cells.Any (b => GreaterThanOrEqual (b, a) && LessThan (b - a, (t.CellSize.Inverse() * 16.0f).Round())));

  private static List <Rect2> GetUncoveredRects (IEnumerable <Rect2> originals, IReadOnlyCollection <Rect2> overlaps)
  {
    return originals.Where (x => !overlaps.Any (y => IsEnclosedBy (x, y))).Aggregate (new List <Rect2>(),
      (a, x) => overlaps.Any (y => x.Intersects (y))
        ? overlaps.Where (y => x.Intersects (y)).Aggregate (a, (b, y) => b.Union (GetUncoveredRects (x, y)).ToList())
        : a.Union (new List <Rect2> { x }).ToList());
  }

  private static IEnumerable <Rect2> GetUncoveredRects (Rect2 original, Rect2 overlap)
  {
    overlap = original.Clip (overlap);
    List <Rect2> uncoveredRects = new();
    var x1 = overlap.Position.x;
    var y1 = overlap.Position.y;
    var x2 = overlap.End.x;
    var y2 = overlap.End.y;
    var x3 = original.Position.x;
    var y3 = original.Position.y;
    var x4 = original.End.x;
    var y4 = original.End.y;
    var w1 = overlap.Size.x;
    var w2 = original.Size.x;
    var w3 = w2 - w1;
    var w4 = x4 - x2;
    var w5 = x1 - x3;
    var h1 = overlap.Size.y;
    var h2 = original.Size.y;
    var h3 = h2 - h1;
    var h4 = y4 - y2;
    var h5 = y1 - y3;
    var left = Mathf.IsEqualApprox (x1, x3);
    var right = Mathf.IsEqualApprox (x2, x4);
    var top = Mathf.IsEqualApprox (y1, y3);
    var bottom = Mathf.IsEqualApprox (y2, y4);
    float x;
    float y;
    float w;
    float h;

    // ReSharper disable once ConvertIfStatementToSwitchStatement
    if (left && right && top)
    {
      x = x3;
      y = y2;
      w = w2;
      h = h3;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (left && right && bottom)
    {
      x = x3;
      y = y3;
      w = w2;
      h = h3;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (left && top && bottom)
    {
      x = x2;
      y = y3;
      w = w3;
      h = h2;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (right && top && bottom)
    {
      x = x3;
      y = y3;
      w = w3;
      h = h2;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (left && right)
    {
      x = x3;
      y = y3;
      w = w2;
      h = h5;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y2;
      w = w2;
      h = h4;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (top && bottom)
    {
      x = x3;
      y = y3;
      w = w5;
      h = h2;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x2;
      y = y3;
      w = w4;
      h = h2;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (left && top)
    {
      x = x2;
      y = y3;
      w = w3;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y2;
      w = w2;
      h = h3;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (right && top)
    {
      x = x3;
      y = y3;
      w = w3;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y2;
      w = w2;
      h = h3;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (left && bottom)
    {
      x = x3;
      y = y3;
      w = w2;
      h = h3;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x2;
      y = y1;
      w = w3;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (right && bottom)
    {
      x = x3;
      y = y3;
      w = w2;
      h = h3;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y1;
      w = w3;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (top)
    {
      x = x3;
      y = y3;
      w = w5;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x2;
      y = y3;
      w = w4;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y2;
      w = w2;
      h = h3;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (bottom)
    {
      x = x3;
      y = y3;
      w = w2;
      h = h3;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y1;
      w = w5;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x2;
      y = y1;
      w = w4;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (left)
    {
      x = x3;
      y = y3;
      w = w2;
      h = h5;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x1;
      y = y1;
      w = w3;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y2;
      w = w2;
      h = h4;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }
    else if (right)
    {
      x = x3;
      y = y3;
      w = w2;
      h = h5;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y1;
      w = w3;
      h = h1;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
      x = x3;
      y = y2;
      w = w2;
      h = h4;
      uncoveredRects.Add (new Rect2 (x, y, w, h));
    }

    return uncoveredRects;
  }
}