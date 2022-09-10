using System.Collections.Generic;
using Godot;
using static Tools;

public class Weapon
{
  public enum State
  {
    Unequipped,
    Equipped,
    Attacking
  }

  private StateMachine <State> _sm;
  private readonly Sprite _backpackSprite;
  private readonly Sprite _itemInBackpackSprite;
  private readonly AnimationPlayer _primaryPlayerAnimator;
  private readonly AnimationPlayer _secondaryPlayerAnimator;
  private bool _isUnequipping;
  private bool _isEquipping;
  private float _delta;

  // @formatter:off

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.Unequipped, new[] { State.Equipped }},
    { State.Equipped, new[] { State.Attacking, State.Unequipped }},
    { State.Attacking, new[] { State.Equipped }},
  };

  // @formatter:on

  // ReSharper disable once SuggestBaseTypeForParameterInConstructor
  public Weapon (Node2D animations, Log.Level logLevel)
  {
    _primaryPlayerAnimator = animations.GetNode <AnimationPlayer> ("Players/Primary");
    _secondaryPlayerAnimator = animations.GetNode <AnimationPlayer> ("Players/Secondary");
    _backpackSprite = animations.GetNode <Sprite> ("Sprites/backpack");
    _itemInBackpackSprite = animations.GetNode <Sprite> ("Sprites/item-in-backpack");
    InitializeStateMachine (logLevel);
  }

  // ReSharper disable once MemberCanBeMadeStatic.Global
  public void Update (float delta)
  {
    _delta = delta;
    _sm.Update (delta: delta);
  }

  public State GetState() => _sm.GetState();
  public void Unequip() => _sm.ToIf (State.Unequipped, _sm.Is (State.Equipped));
  public void OnEquipAnimationStarted() => _isEquipping = true;
  public void OnEquipAnimationFinished() => _isEquipping = false;
  public void OnUnequipAnimationStarted() => _isUnequipping = true;
  public void OnUnequipAnimationFinished() => _isUnequipping = false;

  // @formatter:off

  private bool ShouldEquip() =>
    WasItemKeyPressedOnce() && _backpackSprite.Visible && _itemInBackpackSprite.Visible && !_isUnequipping &&
    _primaryPlayerAnimator.AssignedAnimation != "player_cliff_arresting" &&
    _primaryPlayerAnimator.AssignedAnimation != "player_cliff_hanging" &&
    _primaryPlayerAnimator.AssignedAnimation != "player_climbing_up" &&
    _primaryPlayerAnimator.AssignedAnimation != "player_free_falling";

  // @formatter:on

  private bool ShouldUnequip() =>
    WasItemKeyPressedOnce() && _backpackSprite.Visible && !_itemInBackpackSprite.Visible && !_isEquipping;

  private void StartEquipping()
  {
    switch (_primaryPlayerAnimator.AssignedAnimation)
    {
      case "player_idle_left":
      case "player_walking_left":
      case "player_running_left":
      case "player_free_falling":
      {
        PlaySyncedAnimation ("player_equipping_left", _secondaryPlayerAnimator, _primaryPlayerAnimator, _delta);

        break;
      }
      case "player_idle_back":
      case "player_cliff_arresting":
      case "player_cliff_hanging":
      {
        PlaySyncedAnimation ("player_equipping_back", _secondaryPlayerAnimator, _primaryPlayerAnimator, _delta);

        break;
      }
    }
  }

  private void StartUnequipping()
  {
    switch (_primaryPlayerAnimator.AssignedAnimation)
    {
      case "player_idle_left":
      case "player_walking_left":
      case "player_running_left":
      case "player_free_falling":
      {
        PlaySyncedAnimation ("player_unequipping_left", _secondaryPlayerAnimator, _primaryPlayerAnimator, _delta);

        break;
      }
      case "player_idle_back":
      case "player_cliff_arresting":
      case "player_cliff_hanging":
      {
        PlaySyncedAnimation ("player_unequipping_back", _secondaryPlayerAnimator, _primaryPlayerAnimator, _delta);

        break;
      }
    }
  }

  private void InitializeStateMachine (Log.Level logLevel)
  {
    _sm = new StateMachine <State> (TransitionTable, State.Unequipped, logLevel);
    _sm.OnTransition (State.Unequipped, State.Equipped, StartEquipping);
    _sm.OnTransition (State.Equipped, State.Unequipped, StartUnequipping);
    _sm.AddTrigger (State.Unequipped, State.Equipped, condition: ShouldEquip);
    _sm.AddTrigger (State.Equipped, State.Unequipped, condition: ShouldUnequip);
    // _sm.AddTrigger (State.Equipped, State.Attacking, condition: WasAttackKeyPressedOnce);
    // _sm.AddTrigger (State.Attacking, State.Equipped, condition: IsAttackFinished);
  }
}