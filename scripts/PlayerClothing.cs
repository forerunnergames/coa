using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using static Tools;

public class PlayerClothing
{
  private ClickMode _clickMode = ClickMode.Remove;
  private readonly List <Sprite> _clothes = new();
  private readonly Node _parent;
  private readonly Log _log;

  private enum ClickMode
  {
    Add,
    Remove
  }

  // @formatter:off

  private static readonly Dictionary <string, Dictionary <string, int>> ZIndices = new()
  {
    { "player_idle_left", new Dictionary <string, int> {
      { "item-in-backpack", 0 },
      { "backpack", 1 },
      { "shirt-sleeve-right", 2 },
      { "foot-right", 3 },
      { "boot-right", 4 },
      { "arm-right", 5 },
      { "hand-right", 5 },
      { "glove-right", 6 },
      { "foot-left", 7 },
      { "body", 8 },
      { "shirt", 9 },
      { "backpack-straps", 10 },
      { "head", 11 },
      { "head-outline-rear", 11 },
      { "boot-left", 12 },
      { "pants", 13 },
      { "belt", 14 },
      { "hair", 15 },
      { "scarf", 16 },
      { "hat", 17 },
      { "hat-outline", 17 },
      { "item-in-hand", 18 },
      { "arm-left", 19 },
      { "hand-left", 19 },
      { "shirt-sleeve-left", 20 },
      { "glove-left", 20 }}},
    { "player_idle_back", new Dictionary <string, int> {
      { "item-in-hand", 0 },
      { "body", 1 },
      { "head", 2 },
      { "head-outline-rear", 2 },
      { "foot-left", 3 },
      { "foot-right", 3 },
      { "boot-left", 4 },
      { "boot-right", 4 },
      { "pants", 5 },
      { "shirt", 6 },
      { "belt", 7 },
      { "arm-left", 8 },
      { "arm-right", 8 },
      { "shirt-sleeve-left", 9 },
      { "shirt-sleeve-right", 9 },
      { "hand-left", 10 },
      { "hand-right", 10 },
      { "glove-left", 11 },
      { "glove-right", 11 },
      { "scarf", 12 },
      { "backpack", 13 },
      { "backpack-straps", 13 },
      { "hair", 14 },
      { "hat-outline", 15 },
      { "hat", 16 },
      { "item-in-backpack", 17 }}},
    { "player_cliff_hanging", new Dictionary <string, int> {
      { "item-in-hand", 0 },
      { "body", 1 },
      { "head", 2 },
      { "head-outline-rear", 2 },
      { "foot-left", 3 },
      { "foot-right", 3 },
      { "boot-left", 4 },
      { "boot-right", 4 },
      { "pants", 5 },
      { "shirt", 6 },
      { "belt", 7 },
      { "arm-left", 8 },
      { "arm-right", 8 },
      { "shirt-sleeve-left", 9 },
      { "shirt-sleeve-right", 9 },
      { "hand-left", 10 },
      { "hand-right", 10 },
      { "glove-left", 11 },
      { "glove-right", 11 },
      { "scarf", 12 },
      { "backpack", 13 },
      { "backpack-straps", 13 },
      { "hair", 14 },
      { "hat-outline", 15 },
      { "hat", 16 },
      { "item-in-backpack", 17 }}},
    { "player_cliff_arresting", new Dictionary <string, int> {
      { "item-in-hand", 0 },
      { "body", 1 },
      { "head", 2 },
      { "head-outline-rear", 2 },
      { "foot-left", 3 },
      { "foot-right", 3 },
      { "boot-left", 4 },
      { "boot-right", 4 },
      { "pants", 5 },
      { "shirt", 6 },
      { "belt", 7 },
      { "arm-left", 8 },
      { "arm-right", 8 },
      { "shirt-sleeve-left", 9 },
      { "shirt-sleeve-right", 9 },
      { "hand-left", 10 },
      { "hand-right", 10 },
      { "glove-left", 11 },
      { "glove-right", 11 },
      { "scarf", 12 },
      { "backpack", 13 },
      { "backpack-straps", 13 },
      { "hair", 14 },
      { "hat-outline", 15 },
      { "hat", 16 },
      { "item-in-backpack", 17 }}},
    { "player_free_falling", new Dictionary <string, int> {
      { "item-in-backpack", 0 },
      { "backpack", 1 },
      { "shirt-sleeve-right", 2 },
      { "foot-right", 3 },
      { "boot-right", 4 },
      { "arm-right", 5 },
      { "hand-right", 5 },
      { "glove-right", 6 },
      { "foot-left", 7 },
      { "body", 8 },
      { "shirt", 9 },
      { "backpack-straps", 10 },
      { "head", 11 },
      { "head-outline-rear", 11 },
      { "boot-left", 12 },
      { "pants", 13 },
      { "belt", 14 },
      { "hair", 15 },
      { "scarf", 16 },
      { "hat", 17 },
      { "hat-outline", 17 },
      { "item-in-hand", 18 },
      { "arm-left", 19 },
      { "hand-left", 19 },
      { "shirt-sleeve-left", 20 },
      { "glove-left", 20 }}},
    { "player_equipping_left", new Dictionary <string, int> {
      { "item-in-backpack", 0 },
      { "backpack", 1 },
      { "shirt-sleeve-right", 2 },
      { "foot-right", 3 },
      { "boot-right", 4 },
      { "arm-right", 5 },
      { "hand-right", 5 },
      { "glove-right", 6 },
      { "foot-left", 7 },
      { "body", 8 },
      { "shirt", 9 },
      { "backpack-straps", 10 },
      { "head", 11 },
      { "head-outline-rear", 11 },
      { "boot-left", 12 },
      { "pants", 13 },
      { "belt", 14 },
      { "hair", 15 },
      { "scarf", 16 },
      { "hat", 17 },
      { "hat-outline", 17 },
      { "item-in-hand", 18 },
      { "arm-left", 19 },
      { "hand-left", 19 },
      { "shirt-sleeve-left", 20 },
      { "glove-left", 20 }}},
    { "player_unequipping_left", new Dictionary <string, int> {
      { "item-in-backpack", 0 },
      { "backpack", 1 },
      { "shirt-sleeve-right", 2 },
      { "foot-right", 3 },
      { "boot-right", 4 },
      { "arm-right", 5 },
      { "hand-right", 5 },
      { "glove-right", 6 },
      { "foot-left", 7 },
      { "body", 8 },
      { "shirt", 9 },
      { "backpack-straps", 10 },
      { "head", 11 },
      { "head-outline-rear", 11 },
      { "boot-left", 12 },
      { "pants", 13 },
      { "belt", 14 },
      { "hair", 15 },
      { "scarf", 16 },
      { "hat", 17 },
      { "hat-outline", 17 },
      { "item-in-hand", 18 },
      { "arm-left", 19 },
      { "hand-left", 19 },
      { "shirt-sleeve-left", 20 },
      { "glove-left", 20 }}},
    { "player_equipping_back", new Dictionary <string, int> {
      { "item-in-hand", 0 },
      { "body", 1 },
      { "head", 2 },
      { "head-outline-rear", 2 },
      { "foot-left", 3 },
      { "foot-right", 3 },
      { "boot-left", 4 },
      { "boot-right", 4 },
      { "pants", 5 },
      { "shirt", 6 },
      { "belt", 7 },
      { "arm-left", 8 },
      { "shirt-sleeve-left", 9 },
      { "hand-left", 10 },
      { "glove-left", 11 },
      { "scarf", 12 },
      { "backpack", 13 },
      { "backpack-straps", 13 },
      { "hair", 14 },
      { "hat-outline", 15 },
      { "hat", 16 },
      { "item-in-backpack", 17 },
      { "arm-right", 18 },
      { "shirt-sleeve-right", 19 },
      { "hand-right", 20 },
      { "glove-right", 21 }}},
    { "player_unequipping_back", new Dictionary <string, int> {
      { "item-in-hand", 0 },
      { "body", 1 },
      { "head", 2 },
      { "head-outline-rear", 2 },
      { "foot-left", 3 },
      { "foot-right", 3 },
      { "boot-left", 4 },
      { "boot-right", 4 },
      { "pants", 5 },
      { "shirt", 6 },
      { "belt", 7 },
      { "arm-left", 8 },
      { "shirt-sleeve-left", 9 },
      { "hand-left", 10 },
      { "glove-left", 11 },
      { "scarf", 12 },
      { "backpack", 13 },
      { "backpack-straps", 13 },
      { "hair", 14 },
      { "hat-outline", 15 },
      { "hat", 16 },
      { "item-in-backpack", 17 },
      { "arm-right", 18 },
      { "shirt-sleeve-right", 19 },
      { "hand-right", 20 },
      { "glove-right", 21 }}},
    { "player_walking_left", new Dictionary <string, int> {
      { "item-in-backpack", 0 },
      { "backpack", 1 },
      { "shirt-sleeve-right", 2 },
      { "foot-right", 3 },
      { "boot-right", 4 },
      { "arm-right", 5 },
      { "hand-right", 5 },
      { "glove-right", 6 },
      { "foot-left", 7 },
      { "body", 8 },
      { "shirt", 9 },
      { "backpack-straps", 10 },
      { "head", 11 },
      { "head-outline-rear", 11 },
      { "boot-left", 12 },
      { "pants", 13 },
      { "belt", 14 },
      { "hair", 15 },
      { "scarf", 16 },
      { "hat", 17 },
      { "hat-outline", 17 },
      { "item-in-hand", 18 },
      { "arm-left", 19 },
      { "hand-left", 19 },
      { "shirt-sleeve-left", 20 },
      { "glove-left", 20 }}},
    { "player_running_left", new Dictionary <string, int> {
      { "item-in-backpack", 0 },
      { "backpack", 1 },
      { "shirt-sleeve-right", 2 },
      { "foot-right", 3 },
      { "boot-right", 4 },
      { "arm-right", 5 },
      { "hand-right", 5 },
      { "glove-right", 6 },
      { "foot-left", 7 },
      { "body", 8 },
      { "shirt", 9 },
      { "backpack-straps", 10 },
      { "head", 11 },
      { "head-outline-rear", 11 },
      { "boot-left", 12 },
      { "pants", 13 },
      { "belt", 14 },
      { "hair", 15 },
      { "scarf", 16 },
      { "hat", 17 },
      { "hat-outline", 17 },
      { "item-in-hand", 18 },
      { "arm-left", 19 },
      { "hand-left", 19 },
      { "shirt-sleeve-left", 20 },
      { "glove-left", 20 }}},
    { "player_attacking", new Dictionary <string, int> {
      { "item-in-backpack", 0 },
      { "backpack", 1 },
      { "shirt-sleeve-right", 2 },
      { "foot-right", 3 },
      { "boot-right", 4 },
      { "arm-right", 5 },
      { "hand-right", 5 },
      { "glove-right", 6 },
      { "foot-left", 7 },
      { "body", 8 },
      { "shirt", 9 },
      { "backpack-straps", 10 },
      { "head", 11 },
      { "head-outline-rear", 11 },
      { "boot-left", 12 },
      { "pants", 13 },
      { "belt", 14 },
      { "hair", 15 },
      { "scarf", 16 },
      { "hat", 17 },
      { "hat-outline", 17 },
      { "item-in-hand", 18 },
      { "arm-left", 19 },
      { "hand-left", 19 },
      { "shirt-sleeve-left", 20 },
      { "glove-left", 20 }}},
    { "player_climbing_up", new Dictionary <string, int> {
      { "item-in-hand", 0 },
      { "body", 1 },
      { "head", 2 },
      { "head-outline-rear", 2 },
      { "foot-left", 3 },
      { "foot-right", 3 },
      { "boot-left", 4 },
      { "boot-right", 4 },
      { "pants", 5 },
      { "shirt", 6 },
      { "belt", 7 },
      { "arm-left", 8 },
      { "arm-right", 8 },
      { "shirt-sleeve-left", 9 },
      { "shirt-sleeve-right", 9 },
      { "hand-left", 10 },
      { "hand-right", 10 },
      { "glove-left", 11 },
      { "glove-right", 11 },
      { "scarf", 12 },
      { "backpack", 13 },
      { "backpack-straps", 13 },
      { "hair", 14 },
      { "hat-outline", 15 },
      { "hat", 16 },
      { "item-in-backpack", 17 }}}
  };

  private static readonly Dictionary <string, string> OppositeVisibilities = new()
  {
    { "arm-left", "shirt-sleeve-left" },
    { "arm-right", "shirt-sleeve-right" },
    { "hand-left", "glove-left" },
    { "hand-right", "glove-right" },
    { "foot-left", "boot-left" },
    { "foot-right", "boot-right" },
    { "hair", "hat" }
  };

  private static readonly Dictionary <string, string> DependentVisibilities = new()
  {
    { "backpack-straps", "backpack" },
    { "hat-outline", "hat" },
    { "shirt-sleeve-left", "shirt" },
    { "shirt-sleeve-right", "shirt" }
  };

  private static readonly List <string> BodyParts = new()
  {
    "arm-left",
    "arm-right",
    "hand-left",
    "hand-right",
    "foot-left",
    "foot-right",
    "hair",
    "head",
    "head-outline-rear",
    "body"
  };

  private static readonly List <string> Unclickables = new()
  {
    "item-in-hand"
  };

  // @formatter:on

  public PlayerClothing (Node parent, Log.Level logLevel, [CallerFilePath] string name = "")
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (name) { CurrentLevel = logLevel };
    _parent = parent;

    ZIndices.First().Value.Where (x => !BodyParts.Contains (x.Key) && !Unclickables.Contains (x.Key)).ToList()
      .ForEach (y => _clothes.Add (parent.GetNode <Sprite> (y.Key)));
  }

  public void OnAnimation (string animationName) =>
    ZIndices[animationName].ToList().ForEach (x => _parent.GetNode <Sprite> (x.Key).ZIndex = x.Value);

  public void UpdateAll (string currentAnimation)
  {
    Sprite item;

    switch (_clickMode)
    {
      case ClickMode.Remove:
      {
        item = GetClickedItemForRemoving (currentAnimation);

        if (item == null) return;

        if (!item.Visible)
        {
          _clickMode = ClickMode.Add;
          item = GetClickedItemForAdding (currentAnimation);
        }

        break;
      }
      case ClickMode.Add:
      {
        item = GetClickedItemForAdding (currentAnimation);

        if (item == null) return;

        if (item.Visible)
        {
          _clickMode = ClickMode.Remove;
          item = GetClickedItemForRemoving (currentAnimation);
        }

        break;
      }
      default:
      {
        _log.Warn ($"Ignoring unrecognized value for {nameof (ClickMode)}: {_clickMode}");

        return;
      }
    }

    if (item == null) return;

    ToggleVisibility (item);
    UpdateSecondary();
  }

  public void UpdateSecondary()
  {
    var shirt = _clothes.First (x => x.Name == "shirt");
    var shirtSleeveLeft = _clothes.First (x => x.Name == "shirt-sleeve-left");
    var shirtSleeveRight = _clothes.First (x => x.Name == "shirt-sleeve-right");
    var backpack = _clothes.First (x => x.Name == "backpack");
    var backpackStraps = _clothes.First (x => x.Name == "backpack-straps");
    var itemInBackpack = _clothes.First (x => x.Name == "item-in-backpack");
    var itemInHand = _parent.GetNode <Sprite> ("item-in-hand");

    shirt.Visible = _clickMode switch
    {
      ClickMode.Add => shirt.Visible || shirtSleeveLeft.Visible || shirtSleeveRight.Visible,
      ClickMode.Remove => shirt.Visible && shirtSleeveLeft.Visible && shirtSleeveRight.Visible,
      _ => _log.Warn ($"Ignoring unrecognized value for {nameof (ClickMode)}: {_clickMode} for {shirt.GetType()}: {shirt.Name}")
    };

    backpack.Visible = _clickMode switch
    {
      ClickMode.Add => backpack.Visible || backpackStraps.Visible || itemInBackpack.Visible,
      ClickMode.Remove => backpack.Visible && backpackStraps.Visible,
      _ => _log.Warn ($"Ignoring unrecognized value for {nameof (ClickMode)}: {_clickMode} for {backpack.GetType()}: {backpack.Name}")
    };

    itemInBackpack.Visible = _clickMode switch
    {
      ClickMode.Add => (itemInBackpack.Visible || backpack.Visible) && !itemInHand.Visible,
      ClickMode.Remove => itemInBackpack.Visible && backpack.Visible,
      _ => _log.Warn (
        $"Ignoring unrecognized value for {nameof (ClickMode)}: {_clickMode} for {itemInBackpack.GetType()}: {itemInBackpack.Name}")
    };

    DependentVisibilities.ToList()
      .ForEach (x => _parent.GetNode <Sprite> (x.Key).Visible = _parent.GetNode <Sprite> (x.Value).Visible);

    OppositeVisibilities.ToList()
      .ForEach (x => _parent.GetNode <Sprite> (x.Key).Visible = !_parent.GetNode <Sprite> (x.Value).Visible);
  }

  private Sprite GetClickedItemForRemoving (string currentAnimation) =>
    _clothes.OrderByDescending (clothes => ZIndices[currentAnimation][clothes.Name])
      .Where (clothing => MouseInSprite (clothing, GetMousePositionInSpriteSpace (clothing)))
      .OrderByDescending (clothing => clothing.Visible).FirstOrDefault();

  private Sprite GetClickedItemForAdding (string currentAnimation) =>
    _clothes.OrderBy (clothes => ZIndices[currentAnimation][clothes.Name])
      .Where (clothing => MouseInSprite (clothing, GetMousePositionInSpriteSpace (clothing)))
      .OrderByDescending (clothing => !clothing.Visible).FirstOrDefault();
}