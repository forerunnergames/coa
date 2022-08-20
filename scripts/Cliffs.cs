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
  private AudioStreamPlayer _ambiencePlayer;
  private AudioStreamPlayer _musicPlayer;
  private TileMap _iceTileMap;
  private readonly Dictionary <Season, AudioStream> _music = new();
  private readonly Dictionary <Season, AudioStream> _ambience = new();
  private readonly Dictionary <Season, float> _musicVolumes = new();
  private readonly Dictionary <Season, float> _ambienceVolumes = new();
  private readonly List <Rect2> _cliffRects = new();
  private List <CollisionShape2D> _colliders;
  private List <Waterfall> _waterfalls;
  private float _currentSeasonFadeInTimeSeconds;
  private float _currentSeasonFadeOutTimeSeconds;
  private Color _clearColor;
  private bool _seasonChangeInProgress;
  private Season _newSeason;
  private Season _oldSeason;
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
    _colliders = GetNodesInGroup <CollisionShape2D> (GetTree(), "Extents");
    _waterfalls = GetNodesInGroups <Waterfall> (GetTree(), "Waterfall", "Parent");
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

  private void InitializeSeasons()
  {
    Enum.GetValues (typeof (Season)).Cast <Season>().ToList()
      .ForEach (x => SetGroupVisible (Enum.GetName (typeof (Season), x), false, GetTree()));

    ChangeSeasonTo (InitialSeason);
    _skipFade = true;
  }

  private void NextSeason() => ChangeSeasonTo (CurrentSeason.Next());

  private void ChangeSeasonTo (Season season)
  {
    _oldSeason = CurrentSeason;
    _newSeason = season;
    _seasonChangeInProgress = true;
    _currentSeasonFadeOutTimeSeconds = 0.0f;
    _currentSeasonFadeInTimeSeconds = 0.0f;
    _ambiencePlayer.Stop();
    _musicPlayer.Stop();
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

    CurrentSeason = _newSeason;

    if (_skipFade || AreAlmostEqual (Modulate, Colors.Black, 0.01f) && AreAlmostEqual (_clearColor, Colors.Black, 0.1f))
    {
      SetGroupVisible (Enum.GetName (typeof (Season), _oldSeason), false, GetTree());
      SetGroupVisible (Enum.GetName (typeof (Season), _newSeason), true, GetTree());
      _musicPlayer.Stream = _music[_newSeason];
      _ambiencePlayer.Stream = _ambience[_newSeason];
      _musicPlayer.VolumeDb = _musicVolumes[_newSeason];
      _ambiencePlayer.VolumeDb = _ambienceVolumes[_newSeason];
      LoopAudio (_musicPlayer.Stream);
      LoopAudio (_ambiencePlayer.Stream);
      _ambiencePlayer.Play();
      _waterfalls.ForEach (x => x.OnSeasonChange (_newSeason));
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