using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static Tools;

public class Cliffs : Area2D
{
  [Export] public Season InitialSeason = Season.Summer;
  [Export] public Color InitialClearColor = Color.Color8 (11, 118, 255);
  [Export] public Log.Level LogLevel = Log.Level.Info;

  // Field must be publicly accessible from Player.cs
  public Season CurrentSeason;

  public enum Season
  {
    Summer,
    Winter
  }

  private Player _player;
  private AnimationPlayer _playerPrimaryAnimator;
  private Area2D _playerAnimationAreaColliders;
  private Rect2 _playerRect;
  private Vector2 _playerPosition;
  private string _playerAnimation;
  private CollisionShape2D _playerAnimationCollider;
  private bool _isPlayerInWaterfall;
  private AudioStreamPlayer _ambiencePlayer;
  private AudioStreamPlayer _musicPlayer;
  private TileMap _iceTileMap;
  private readonly Dictionary <Season, int> _waterfallZIndex = new();
  private readonly Dictionary <Season, int> _waterfallMistZIndex = new();
  private readonly Dictionary <Season, AudioStream> _music = new();
  private readonly Dictionary <Season, AudioStream> _ambience = new();
  private readonly Dictionary <Season, float> _musicVolumes = new();
  private readonly Dictionary <Season, float> _ambienceVolumes = new();
  private readonly List <CollisionShape2D> _colliders = new();
  private readonly List <Rect2> _cliffRects = new();
  private float _currentSeasonFadeInTimeSeconds;
  private float _currentSeasonFadeOutTimeSeconds;
  private Color _clearColor;
  private bool _seasonChangeInProgress;
  private Season _newSeason;
  private Season _playerSeason;
  private bool _fadeIn;
  private bool _skipFade;
  private Log _log;

  public override void _Ready()
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (Name) { CurrentLevel = LogLevel };
    _clearColor = InitialClearColor;
    VisualServer.SetDefaultClearColor (_clearColor);
    _waterfallZIndex.Add (Season.Summer, 33);
    _waterfallZIndex.Add (Season.Winter, 1);
    _waterfallMistZIndex.Add (Season.Summer, 33);
    _waterfallMistZIndex.Add (Season.Winter, 33);
    _ambience.Add (Season.Summer, ResourceLoader.Load <AudioStream> ("res://assets/sounds/ambience_summer.wav"));
    _ambience.Add (Season.Winter, ResourceLoader.Load <AudioStream> ("res://assets/sounds/ambience_winter.wav"));
    _ambienceVolumes.Add (Season.Summer, -10);
    _ambienceVolumes.Add (Season.Winter, -20);
    _music.Add (Season.Winter, ResourceLoader.Load <AudioStream> ("res://assets/music/music6.wav"));
    _music.Add (Season.Summer, ResourceLoader.Load <AudioStream> ("res://assets/music/music5.wav"));
    _musicVolumes.Add (Season.Winter, -15);
    _musicVolumes.Add (Season.Summer, -25);
    _ambiencePlayer = GetNode <AudioStreamPlayer> ("../Audio Players/Ambience");
    _musicPlayer = GetNode <AudioStreamPlayer> ("../Audio Players/Music");
    _iceTileMap = GetNode <TileMap> ("Ice");
    _colliders.AddRange (GetTree().GetNodesInGroup ("Extents").Cast <CollisionShape2D>());
    _player = GetNode <Player> ("../Player");
    _playerPrimaryAnimator = _player.GetNode <AnimationPlayer> ("Animations/Players/Primary");
    _playerAnimation = _playerPrimaryAnimator.CurrentAnimation;
    _playerAnimationAreaColliders = _player.GetNode <Area2D> ("Animations/Area Colliders");
    _playerAnimationCollider = _playerAnimationAreaColliders.GetNode <CollisionShape2D> (_playerAnimation);
    InitializeSeasons();
  }

  public override void _Process (float delta)
  {
    Update();
    UpdateSeasons (delta);
    UpdatePlayer();
  }

  public override void _UnhandledInput (InputEvent @event)
  {
    if (IsReleased (Tools.Input.Season, @event) && !_seasonChangeInProgress) NextSeason();
    if (IsReleased (Tools.Input.Music, @event)) ToggleMusic();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnWaterfallEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"Player entered waterfall.");
    _isPlayerInWaterfall = true;
    _player.IsInFrozenWaterfall = CurrentSeason == Season.Winter;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnWaterfallExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"Player exited waterfall.");
    _isPlayerInWaterfall = false;
    _player.IsInFrozenWaterfall = false;
  }

  private void InitializeSeasons()
  {
    foreach (Season season in Enum.GetValues (typeof (Season)))
    {
      SetGroupVisible (Enum.GetName (typeof (Season), season), false);
    }

    ChangeSeasonTo (InitialSeason);
    _skipFade = true;
  }

  private void NextSeason() => ChangeSeasonTo (CurrentSeason.Next());

  private void ChangeSeasonTo (Season season)
  {
    _newSeason = season;
    _seasonChangeInProgress = true;
    _currentSeasonFadeOutTimeSeconds = 0.0f;
    _currentSeasonFadeInTimeSeconds = 0.0f;
    _ambiencePlayer.Stop();
    _musicPlayer.Stop();
  }

  private void UpdateWaterfall (Season season, float delta)
  {
    var isWinter = season == Season.Winter;
    var waterfall = GetNode <Area2D> ("Waterfall");
    waterfall.Visible = true;
    waterfall.ZIndex = _waterfallZIndex[season];

    for (var i = 1; i <= 3; ++i)
    {
      var mist = waterfall.GetNode <AnimatedSprite> ("Mist " + i);
      mist.ZIndex = _waterfallMistZIndex[season];
    }

    foreach (Node node1 in waterfall.GetChildren())
    {
      if (node1 is StaticBody2D body) UpdateFrozenWaterfallTopGround (body, season, delta);

      if (node1 is not AnimatedSprite sprite) continue;

      sprite.Playing = !isWinter;
      sprite.Visible = true;

      foreach (Node node2 in sprite.GetChildren())
      {
        if (node2 is not AudioStreamPlayer2D sound) continue;

        LoopAudio (sound.Stream);
        sound.Playing = !isWinter;
      }
    }

    _player.IsInFrozenWaterfall = _isPlayerInWaterfall && isWinter;
  }

  private async void UpdateFrozenWaterfallTopGround (PhysicsBody2D waterSurfaceCollider, Season season, float delta)
  {
    var isWinter = season == Season.Winter;
    waterSurfaceCollider.SetCollisionMaskBit (0, isWinter);
    waterSurfaceCollider.SetCollisionLayerBit (1, isWinter);
    GetNode <CollisionShape2D> ("Ground 3/CollisionShape2D").Disabled = isWinter;
    GetNode <CollisionShape2D> ("Edge 3/CollisionShape2D").Disabled = isWinter;
    await ToSignal (GetTree().CreateTimer (delta, false), "timeout");

    foreach (int shapeOwnerId in waterSurfaceCollider.GetShapeOwners())
    {
      waterSurfaceCollider.ShapeOwnerSetOneWayCollision (Convert.ToUInt32 (shapeOwnerId), isWinter);
    }
  }

  private void SetGroupVisible (string groupName, bool isVisible)
  {
    foreach (Node node in GetTree().GetNodesInGroup (groupName))
    {
      if (node is not CanvasItem item) continue;

      _log.Debug ($"Setting {item.Name} {(item.Visible ? "visible" : "invisible")}.");
      item.Visible = isVisible;
    }
  }

  private void UpdateSeasons (float delta)
  {
    if (!_seasonChangeInProgress)
    {
      _fadeIn = false;
      _skipFade = false;

      return;
    }

    if (!_skipFade && !_fadeIn)
    {
      _currentSeasonFadeOutTimeSeconds += delta;
      Modulate = Modulate.LinearInterpolate (Colors.Black, _currentSeasonFadeOutTimeSeconds * 0.2f);
      _clearColor = _clearColor.LinearInterpolate (Colors.Black, _currentSeasonFadeOutTimeSeconds * 0.2f);
      VisualServer.SetDefaultClearColor (_clearColor);
      _fadeIn = AreAlmostEqual (Modulate, Colors.Black, 0.01f) && AreAlmostEqual (_clearColor, Colors.Black, 0.1f);

      return;
    }

    if (_skipFade || AreAlmostEqual (Modulate, Colors.Black, 0.01f) && AreAlmostEqual (_clearColor, Colors.Black, 0.1f))
    {
      SetGroupVisible (Enum.GetName (typeof (Season), CurrentSeason), false);
      SetGroupVisible (Enum.GetName (typeof (Season), _newSeason), true);
      _musicPlayer.Stream = _music[_newSeason];
      _ambiencePlayer.Stream = _ambience[_newSeason];
      _musicPlayer.VolumeDb = _musicVolumes[_newSeason];
      _ambiencePlayer.VolumeDb = _ambienceVolumes[_newSeason];
      LoopAudio (_musicPlayer.Stream);
      LoopAudio (_ambiencePlayer.Stream);
      _ambiencePlayer.Play();
      UpdateWaterfall (_newSeason, delta);
    }

    if (!_skipFade)
    {
      _currentSeasonFadeInTimeSeconds += delta;
      Modulate = Modulate.LinearInterpolate (Colors.White, _currentSeasonFadeInTimeSeconds * 0.1f);
      _clearColor = _clearColor.LinearInterpolate (InitialClearColor, _currentSeasonFadeInTimeSeconds * 0.1f);
      VisualServer.SetDefaultClearColor (_clearColor);
    }

    _seasonChangeInProgress = !AreAlmostEqual (Modulate, Colors.White, 0.01f) ||
                              !AreAlmostEqual (_clearColor, InitialClearColor, 0.1f);

    if (_seasonChangeInProgress) return;

    CurrentSeason = _newSeason;
    _musicPlayer.Play();
    _log.Info ($"Current season is now: {CurrentSeason}");
  }

  private void UpdatePlayer()
  {
    if (_playerSeason == CurrentSeason && _playerPrimaryAnimator.CurrentAnimation == _playerAnimation &&
        AreAlmostEqual (_playerAnimationCollider.GlobalPosition, _playerPosition, 0.001f)) return;

    _playerSeason = CurrentSeason;
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