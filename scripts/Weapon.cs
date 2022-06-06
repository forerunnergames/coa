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
  private readonly AnimationPlayer _playerAnimator1;
  private readonly AnimationPlayer _playerAnimator2;
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

  public Weapon (Node player)
  {
    _playerAnimator1 = player.GetNode <AnimationPlayer> ("Sprites/AnimationPlayer1");
    _playerAnimator2 = player.GetNode <AnimationPlayer> ("Sprites/AnimationPlayer2");
    _backpackSprite = player.GetNode <Sprite> ("Sprites/backpack");
    _itemInBackpackSprite = player.GetNode <Sprite> ("Sprites/item-in-backpack");
    InitializeStateMachine();
  }

  // ReSharper disable once MemberCanBeMadeStatic.Global
  public void Update (float delta)
  {
    _delta = delta;
    _sm.Update();
  }

  public State GetState() => _sm.GetState();
  public void Unequip() => _sm.ToIf (State.Unequipped, _sm.Is (State.Equipped));
  public void OnEquipAnimationStarted() => _isEquipping = true;
  public void OnEquipAnimationFinished() => _isEquipping = false;
  public void OnUnequipAnimationStarted() => _isUnequipping = true;
  public void OnUnequipAnimationFinished() => _isUnequipping = false;

  private bool ShouldEquip()
  {
    return WasItemKeyPressedOnce() && _backpackSprite.Visible && _itemInBackpackSprite.Visible && !_isUnequipping &&
           _playerAnimator1.AssignedAnimation != "player_cliff_hanging" && _playerAnimator1.AssignedAnimation != "player_climbing_up";
  }

  private bool ShouldUnequip()
  {
    return WasItemKeyPressedOnce() && _backpackSprite.Visible && !_itemInBackpackSprite.Visible && !_isEquipping;
  }

  private void StartEquipping()
  {
    switch (_playerAnimator1.AssignedAnimation)
    {
      case "player_idle_left":
      case "player_walking_left":
      case "player_running_left":
      {
        PlaySyncedAnimation ("player_equipping_left", _playerAnimator2, _playerAnimator1, _delta);

        break;
      }
      case "player_idle_back":
      case "player_cliff_hanging":
      case "player_falling":
      {
        PlaySyncedAnimation ("player_equipping_back", _playerAnimator2, _playerAnimator1, _delta);

        break;
      }
    }
  }

  private void StartUnequipping()
  {
    switch (_playerAnimator1.AssignedAnimation)
    {
      case "player_idle_left":
      case "player_walking_left":
      case "player_running_left":
      {
        PlaySyncedAnimation ("player_unequipping_left", _playerAnimator2, _playerAnimator1, _delta);

        break;
      }
      case "player_idle_back":
      case "player_cliff_hanging":
      {
        PlaySyncedAnimation ("player_unequipping_back", _playerAnimator2, _playerAnimator1, _delta);

        break;
      }
    }
  }

  private void InitializeStateMachine()
  {
    _sm = new StateMachine <State> (TransitionTable, State.Unequipped);
    _sm.OnTransition (State.Unequipped, State.Equipped, StartEquipping);
    _sm.OnTransition (State.Equipped, State.Unequipped, StartUnequipping);
    _sm.AddTrigger (State.Unequipped, State.Equipped, ShouldEquip);
    _sm.AddTrigger (State.Equipped, State.Unequipped, ShouldUnequip);
    // _sm.AddTrigger (State.Equipped, State.Attacking, WasAttackKeyPressedOnce);
    // _sm.AddTrigger (State.Attacking, State.Equipped, IsAttackFinished);
  }
}