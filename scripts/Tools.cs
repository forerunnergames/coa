using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using Object = Godot.Object;

// ReSharper disable MemberCanBePrivate.Global
public static class Tools
{
  private static readonly Log Log = new();

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
    Energy,
    Attack
  }

  private static readonly Dictionary <Input, string[]> Inputs = new()
  {
    { Input.Horizontal, new[] { "move_left", "move_right" } },
    { Input.Vertical, new[] { "move_up", "move_down" } },
    { Input.Item, new[] { "use_item" } },
    { Input.Attack, new[] { "attack" } },
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

  // @formatter:off
  public delegate bool Condition();
  public delegate void DrawPrimitive (Vector2[] points, Color[] colors, Vector2[] uvs);
  public delegate void DrawRect (Rect2 rect, Color color, bool filled);
  public delegate Transform GetLocalTransform();
  public delegate Vector2 Transform (Vector2 point);
  public delegate Vector2 GetGlobalScale();
  public static bool IsReleased (Input i, InputEvent e) => e is InputEventKey k && Inputs[i].Any (x => k.IsActionReleased (x));
  public static bool IsPressed (Input i, InputEvent e) => e is InputEventKey k && Inputs[i].Any (x => k.IsActionPressed (x));
  public static bool IsOneActiveOf (Input i) => Inputs[i].Where (x => Godot.Input.IsActionPressed (x)).Take (2).Count() == 1;
  public static bool IsAnyActiveOf (Input i) => Inputs[i].Any (x => Godot.Input.IsActionPressed (x));
  public static bool WasMouseLeftClicked (InputEvent e) => e is InputEventMouseButton { ButtonIndex: (int)ButtonList.Left, Pressed: true };
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
  public static bool WasAttackKeyPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Attack][0]);
  public static bool IsAttackKeyPressed() => Godot.Input.IsActionPressed (Inputs[Input.Attack][0]);
  public static bool WasAttackKeyReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Attack][0]);
  public static bool WasItemKeyReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Item][0]);
  public static bool WasItemKeyPressedOnce() => Godot.Input.IsActionJustPressed (Inputs[Input.Item][0]);
  public static bool IsEnergyKeyPressed() => Godot.Input.IsActionPressed (Inputs[Input.Energy][0]);
  public static bool WasJumpKeyPressed() => Godot.Input.IsActionJustPressed (Inputs[Input.Jump][0]);
  public static bool WasJumpKeyReleased() => Godot.Input.IsActionJustReleased (Inputs[Input.Jump][0]);
  public static float SafelyClampMin (float f, float min) => IsSafelyLessThan (f, min) ? min : f;
  public static float SafelyClampMax (float f, float max) => IsSafelyGreaterThan (f, max) ? max : f;
  public static float SafelyClamp (float f, float min, float max) => SafelyClampMin (SafelyClampMax (f, max), min);
  public static bool IsSafelyLessThan (float f1, float f2) => f1 < f2 && !Mathf.IsEqualApprox (f1, f2);
  public static bool IsSafelyGreaterThan (float f1, float f2) => f1 > f2 && !Mathf.IsEqualApprox (f1, f2);
  public static bool LessThan (Vector2 v1, Vector2 v2) => v1.x < v2.x && v1.y < v2.y;
  public static bool GreaterThan (Vector2 v1, Vector2 v2) => v1.x > v2.x && v1.y > v2.y;
  public static bool LessThanOrEqual (Vector2 v1, Vector2 v2) => v1.x <= v2.x && v1.y <= v2.y;
  public static bool GreaterThanOrEqual (Vector2 v1, Vector2 v2) => v1.x >= v2.x && v1.y >= v2.y;
  public static bool AreAlmostEqual (Vector2 v1, Vector2 v2, float epsilon) => Mathf.Abs (v1.x - v2.x) < epsilon && Mathf.Abs (v1.y - v2.y) < epsilon;
  public static bool AreAlmostEqual (float f1, float f2, float epsilon) => Mathf.Abs (f1 - f2) < epsilon && Mathf.Abs (f1 - f2) < epsilon;
  public static bool IsEnclosedBy (Rect2 original, IReadOnlyCollection <Rect2> overlaps) => overlaps.Aggregate (new List <Rect2> { original }, (x, _) => GetUncoveredRects (x, overlaps)).Count == 0;
  public static bool IsEnclosedBy (Rect2 r1, Rect2 r2) => r1.Position.x >= r2.Position.x && r1.End.x <= r2.End.x && r1.Position.y >= r2.Position.y && r1.End.y <= r2.End.y;
  public static Vector2 GetTileCellAtCenterOf (Area2D area, string colliderName, TileMap t) => GetTileCellAtCenterOf (area, area.GetNode <CollisionShape2D> (colliderName), t);
  public static bool IsIntersectingAnyTile (Area2D area, CollisionShape2D collider, TileMap t) => GetIntersectingTileId (area, collider, t) != -1;
  public static bool IsIntersectingAnyTile (Rect2 rect, TileMap t) => GetIntersectingTileId (rect, t) != -1;
  public static int GetIntersectingTileId (Rect2 rect, TileMap t) => t.GetCellv (GetIntersectingTileCell (rect, t));
  public static int GetIntersectingTileId (Area2D area, CollisionShape2D collider, TileMap t) => t.GetCellv (GetIntersectingTileCell (area, collider, t));
  public static Vector2 GetIntersectingTileCell (Area2D area, string colliderName, TileMap t) => GetIntersectingTileCell (area, area.GetNode <CollisionShape2D> (colliderName), t);
  public static string GetIntersectingTileName (Area2D area, CollisionShape2D collider, TileMap t) => GetTileName (GetIntersectingTileCell (area, collider, t), t);
  public static string GetCollidingTileName (Vector2 collisionPoint, TileMap t) => GetTileName (GetCollidingTileCell (collisionPoint, t), t);
  public static Vector2 GetIntersectingTileCellGlobalOrigin (Area2D area, CollisionShape2D collider, TileMap t) => GetTileCellGlobalOrigin (GetIntersectingTileCell (area, collider, t), t);
  public static bool MouseInSprite (Sprite sprite, Vector2 spriteLocalMousePos) => sprite.GetRect().HasPoint (spriteLocalMousePos) && sprite.IsPixelOpaque (spriteLocalMousePos);
  public static Vector2 GetMousePositionInSpriteSpace (Sprite sprite) => sprite.GetLocalMousePosition();
  public static void ToggleVisibility (CanvasItem sprite) => sprite.Visible = !sprite.Visible;
  public static void ToggleVisibility (CanvasItem sprite, Condition condition) => sprite.Visible = condition() && !sprite.Visible;
  public static float RoundUpToNextMultiple (float value, float multiple) => (float)Math.Ceiling ((decimal)value / (decimal)multiple) * multiple;
  public static void LoopAudio (AudioStream a) => LoopAudio (a, 0.0f, a.GetLength());
  // @formatter:on

  public static bool AreAlmostEqual (Color color1, Color color2, float epsilon) =>
    Mathf.Abs (color1.r - color2.r) < epsilon && Mathf.Abs (color1.g - color2.g) < epsilon &&
    Mathf.Abs (color1.b - color2.b) < epsilon && Mathf.Abs (color1.a - color2.a) < epsilon;

  public static bool AlmostHasPoint (Rect2 rect, Vector2 point, float epsilon) =>
    (AreAlmostEqual (point.x, rect.Position.x, epsilon) || point.x > (double)rect.Position.x) &&
    (AreAlmostEqual (point.y, rect.Position.y, epsilon) || point.y > (double)rect.Position.y) &&
    point.x < rect.Position.x + (double)rect.Size.x && point.y < rect.Position.y + (double)rect.Size.y;

  public static float NextAnimationFrameDelaySecs (AnimationPlayer player) =>
    player.CurrentAnimationPosition < player.CurrentAnimationLength
      ? RoundUpToNextMultiple (player.CurrentAnimationPosition, player.GetAnimation (player.CurrentAnimation).Step) -
        player.CurrentAnimationPosition
      : 0;

  public static string ToString <T> (IEnumerable <T> e, string sep = ", ", string prepend = "", string append = "",
    Func <T, string> f = null) =>
    e.Select (f ?? (s => prepend + s + append)).DefaultIfEmpty (string.Empty).Aggregate ((a, b) => a + sep + b);

  public static Rect2 GetColliderRect (CollisionObject2D collisionObject, CollisionShape2D collider)
  {
    if (!collisionObject.HasNode (collider.Name) || collider.Shape is not RectangleShape2D rect) return new Rect2();

    var extents = rect.Extents * collisionObject.GlobalScale.Abs();

    return new Rect2 (collider.GlobalPosition - extents, extents * 2);
  }

  public static string GetTileName (Vector2 cell, TileMap t)
  {
    var id = t.GetCellv (cell);

    return id != -1 ? t.TileSet.TileGetName (id) : "";
  }

  public static Vector2 GetTileCellAtCenterOf (Area2D area, CollisionShape2D collider, TileMap t) =>
    GetIntersectingTileCell (new Rect2 (GetColliderRect (area, collider).GetCenter(), Vector2.One), t);

  public static Vector2 GetCollidingTileCell (Vector2 collisionPoint, TileMap t) =>
    GetIntersectingTileCell (new Rect2 (collisionPoint, Vector2.One), t);

  public static Vector2 GetIntersectingTileCell (Area2D area, CollisionShape2D collider, TileMap t) =>
    GetIntersectingTileCell (GetColliderRect (area, collider), t);

  public static Vector2 GetIntersectingTileCell (Rect2 collider, TileMap t) =>
    t.GetUsedCells().Cast <Vector2>().ToList().DefaultIfEmpty (Vector2.Zero).FirstOrDefault (x =>
      collider.Intersects (new Rect2 (GetTileCellGlobalOrigin (x, t), GetTileCellGlobalSize (x, t))));

  public static Vector2 GetTileCellGlobalOrigin (Vector2 cell, TileMap t)
  {
    var xFlipped = t.IsCellXFlipped ((int)cell.x, (int)cell.y);
    var yFlipped = t.IsCellYFlipped ((int)cell.x, (int)cell.y);
    var globalOrigin = t.ToGlobal (t.MapToWorld (cell));
    var globalSize = GetTileCellGlobalSize (cell, t);
    globalOrigin.x += xFlipped ? globalSize.x : 0;
    globalOrigin.y += yFlipped ? globalSize.y : 0;

    return globalOrigin;
  }

  public static Vector2 GetTileCellGlobalSize (Vector2 cell, TileMap t)
  {
    var id = t.GetCell ((int)cell.x, (int)cell.y);

    if (id == -1) return Vector2.Zero;

    return t.TileSet.TileGetTileMode (id) == Godot.TileSet.TileMode.AutoTile
      ? t.TileSet.AutotileGetSize (id) * t.Scale
      : t.TileSet.TileGetRegion (id).Size * t.Scale;
  }

  public static async void PlaySyncedAnimation (string animationName, AnimationPlayer playOn, AnimationPlayer syncTo, float delta)
  {
    await playOn.ToSignal (playOn.GetTree().CreateTimer (NextAnimationFrameDelaySecs (syncTo) - delta, false), "timeout");
    playOn.Play (animationName);
  }

  public static void SetGroupVisible (string groupName, bool isVisible, SceneTree sceneTree)
  {
    foreach (Node node in sceneTree.GetNodesInGroup (groupName))
    {
      if (node is not CanvasItem item) continue;

      Log.Debug ($"Setting {item.Name} {(item.Visible ? "visible" : "invisible")}.");
      item.Visible = isVisible;
    }
  }

  public static List <T> GetNodesInGroup <T> (SceneTree sceneTree, string group) where T : Node =>
    GetNodesInGroups <T> (sceneTree, group);

  public static List <T> GetINodesInGroup <T> (SceneTree sceneTree, string group) where T : INode =>
    GetINodesInGroups <T> (sceneTree, group);

  public static List <T> GetNodesInGroupWithParent <T> (string parent, SceneTree sceneTree, string group) where T : Node =>
    GetNodesInGroupsWithParent <T> (parent, sceneTree, group);

  public static List <T> GetINodesInGroupWithParent <T> (string parent, SceneTree sceneTree, string group) where T : INode =>
    GetINodesInGroupsWithParent <T> (parent, sceneTree, group);

  public static List <T> GetNodesInGroupsWithParent <T> (string parent, SceneTree sceneTree, params string[] groups) where T : Node =>
    GetNodesInGroups <T> (sceneTree, groups).Where (x => x.GetParent()?.Name == parent).ToList();

  public static List <T> GetINodesInGroupsWithParent <T> (string parent, SceneTree sceneTree, params string[] groups)
    where T : INode =>
    GetINodesInGroups <T> (sceneTree, groups).Where (x => x.AsNode().GetParent()?.Name == parent).ToList();

  public static List <T> GetNodesInGroupsWithAnyOfParents <T> (string[] parents, SceneTree sceneTree, params string[] groups)
    where T : Node =>
    GetNodesInGroups <T> (sceneTree, groups).Where (x => parents.ToList().Any (y => x.GetParent()?.Name == y)).ToList();

  public static List <T> GetINodesInGroupsWithAnyOfParents <T> (string[] parents, SceneTree sceneTree, params string[] groups)
    where T : INode =>
    GetINodesInGroups <T> (sceneTree, groups).Where (x => parents.ToList().Any (y => x.AsNode().GetParent()?.Name == y)).ToList();

  public static List <T> GetNodesInGroupWithGrandparent <T> (string grandparent, SceneTree sceneTree, string group) where T : Node =>
    GetNodesInGroupsWithGrandparent <T> (grandparent, sceneTree, group);

  public static List <T> GetINodesInGroupWithGrandparent <T> (string grandparent, SceneTree sceneTree, string group) where T : INode =>
    GetINodesInGroupsWithGrandparent <T> (grandparent, sceneTree, group);

  public static List <T> GetNodesInGroupsWithGrandparent <T> (string grandparent, SceneTree sceneTree, params string[] groups)
    where T : Node =>
    GetNodesInGroups <T> (sceneTree, groups).Where (x => x.GetParent()?.GetParent()?.Name == grandparent).ToList();

  public static List <T> GetINodesInGroupsWithGrandparent <T> (string grandparent, SceneTree sceneTree, params string[] groups)
    where T : INode =>
    GetINodesInGroups <T> (sceneTree, groups).Where (x => x.AsNode().GetParent()?.GetParent()?.Name == grandparent).ToList();

  public static List <T> GetNodesInGroupsWithAnyOfGrandparents <T> (string[] grandparents, SceneTree sceneTree, params string[] groups)
    where T : Node =>
    GetNodesInGroups <T> (sceneTree, groups).Where (x => grandparents.ToList().Any (y => x.GetParent()?.GetParent()?.Name == y))
      .ToList();

  public static List <T>
    GetINodesInGroupsWithAnyOfGrandparents <T> (string[] grandparents, SceneTree sceneTree, params string[] groups) where T : INode =>
    GetINodesInGroups <T> (sceneTree, groups)
      .Where (x => grandparents.ToList().Any (y => x.AsNode().GetParent()?.GetParent()?.Name == y)).ToList();

  public static List <T> GetNodesInGroups <T> (SceneTree sceneTree, params string[] groups) where T : Node
  {
    if (groups == null || groups.Length == 0) return new List <T>();

    var nodes = sceneTree.GetNodesInGroup (groups[0]).Cast <Node>().Where (x => x is T).Cast <T>();

    return groups.Length == 1 ? nodes.ToList() : nodes.Where (x => groups.Distinct().All (x.IsInGroup)).ToList();
  }

  public static List <T> GetINodesInGroups <T> (SceneTree sceneTree, params string[] groups) where T : INode
  {
    if (groups == null || groups.Length == 0) return new List <T>();

    var nodes = sceneTree.GetNodesInGroup (groups[0]).Cast <Node>().Where (x => x is T).Cast <T>();

    return groups.Length == 1 ? nodes.ToList() : nodes.Where (x => groups.Distinct().All (x.AsNode().IsInGroup)).ToList();
  }

  // ReSharper disable once EntityNameCapturedOnly.Global
  public static void NotifyGroup <T1, T2> (string group, T2 signal, Action callback) where T1 : ISignallingNode <T2> where T2 : Enum =>
    GetINodesInGroup <T1> ((callback.Target as Node)?.GetTree(), group).ForEach (x =>
      x.AsNode().Connect (x.NameOf (signal), callback.Target as Object, callback.GetMethodInfo().Name));

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
    var i = 0;

    while (p.x < rect.Position.x || p.x % multiple.x != 0)
    {
      p.x = rng.RandiRange (Mathf.RoundToInt (rect.Position.x), Mathf.RoundToInt (rect.End.x) - 1);

      if (++i < 100) continue;

      Log.Warn ($"{nameof (RandomPointIn)}: Possible infinite loop detected for x value, using default value of {rect.Position.x}...");
      p.x = rect.Position.x;

      break;
    }

    var j = 0;

    while (p.y < rect.Position.y || p.y % multiple.y != 0)
    {
      p.y = rng.RandiRange (Mathf.RoundToInt (rect.Position.y), Mathf.RoundToInt (rect.End.y) - 1);

      if (++j < 100) continue;

      Log.Warn ($"{nameof (RandomPointIn)}: Possible infinite loop detected for y value, using default value of {rect.Position.y}...");
      p.y = rect.Position.y;

      break;
    }

    return p;
  }

  private static List <Rect2> GetUncoveredRects (IEnumerable <Rect2> originals, IReadOnlyCollection <Rect2> overlaps) =>
    originals.Where (x => !overlaps.Any (y => IsEnclosedBy (x, y))).Aggregate (new List <Rect2>(),
      (a, x) => overlaps.Any (y => x.Intersects (y))
        ? overlaps.Where (y => x.Intersects (y)).Aggregate (a, (b, y) => b.Union (GetUncoveredRects (x, y)).ToList())
        : a.Union (new List <Rect2> { x }).ToList());

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