using System.Collections.Generic;
using System.Linq;
using Godot;
using static Seasons;
using static Tools;

public class Cliffs : Area2D
{
  [Export] public Season InitialSeason = Season.Summer;
  [Export] public Color InitialClearColor = Color.Color8 (11, 118, 255);
  [Export] public Log.Level LogLevel = Log.Level.Info;
  private Seasons _seasons;
  private Player _player;
  private AnimationPlayer _playerPrimaryAnimator;
  private Area2D _playerAnimationAreaColliders;
  private Rect2 _playerRect;
  private Vector2 _playerPosition;
  private string _playerAnimation;
  private CollisionShape2D _playerAnimationCollider;
  private AudioStreamPlayer _musicPlayer;
  private TileMap _iceTileMap;
  private readonly List <Rect2> _cliffRects = new();
  private List <CollisionShape2D> _colliders;
  private Season _playerSeason;

  public override void _Ready()
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _seasons = new Seasons (GetTree(), InitialSeason, InitialClearColor, LogLevel);
    _colliders = GetNodesInGroup <CollisionShape2D> (GetTree(), "Extents");
    _musicPlayer = GetNode <AudioStreamPlayer> ("../Audio Players/Music");
    _iceTileMap = GetNode <TileMap> ("Ice");
    _player = GetNode <Player> ("../Player");
    _playerPrimaryAnimator = _player.GetNode <AnimationPlayer> ("Animations/Players/Primary");
    _playerAnimation = _playerPrimaryAnimator.CurrentAnimation;
    _playerAnimationAreaColliders = _player.GetNode <Area2D> ("Animations/Area Colliders");
    _playerAnimationCollider = _playerAnimationAreaColliders.GetNode <CollisionShape2D> (_playerAnimation);
  }

  public override void _Process (float delta)
  {
    Update();
    UpdateSeasons (delta);
    UpdatePlayer();
  }

  public override void _UnhandledInput (InputEvent @event)
  {
    _seasons.OnInput (@event);
    if (IsReleased (Tools.Input.Music, @event)) ToggleMusic();
  }

  public Season GetCurrentSeason() => _seasons.CurrentSeason;
  public bool CurrentSeasonIs (Season season) => _seasons.CurrentSeason == season;
  private void UpdateSeasons (float delta) => _seasons.Update (GetTree(), this, delta);

  private void UpdatePlayer()
  {
    if (_playerSeason == _seasons.CurrentSeason && _playerPrimaryAnimator.CurrentAnimation == _playerAnimation &&
        AreAlmostEqual (_playerAnimationCollider.GlobalPosition, _playerPosition, 0.001f)) return;

    _playerSeason = _seasons.CurrentSeason;
    _playerAnimation = _playerPrimaryAnimator.CurrentAnimation;
    _playerAnimationCollider = _playerAnimationAreaColliders.GetNode <CollisionShape2D> (_playerAnimation);
    _playerPosition = _playerAnimationCollider.GlobalPosition;
    _playerRect = GetAreaColliderRect (_playerAnimationAreaColliders, _playerAnimationCollider);
    _cliffRects.Clear();
    _cliffRects.AddRange (_colliders.Select (x => GetAreaColliderRect (this, x)));
    _player.IsInCliffs = IsEnclosedBy (_playerRect, _cliffRects);

    _player.IsTouchingCliffIce = _iceTileMap.Visible &&
                                 IsIntersectingAnyTile (_playerAnimationAreaColliders, _playerAnimationCollider, _iceTileMap);
  }

  private void ToggleMusic() => _musicPlayer.Playing = !_musicPlayer.Playing;
}