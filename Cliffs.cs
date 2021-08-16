using System;
using Godot;
using Godot.Collections;
using static Tools;

public class Cliffs : Area2D
{
  [Export] public Season InitialSeason = Season.Summer;
  [Export] public Color InitialClearColor = Color.Color8 (11, 118, 255);

  // Field must be publicly accessible from Player.cs
  public Season CurrentSeason;

  public enum Season
  {
    Summer,
    Winter
  }

  private Area2D _playerArea;
  private Rect2 _playerRect;
  private Rect2 _cliffsRect;
  private Vector2 _playerExtents;
  private Vector2 _cliffsExtents;
  private Vector2 _playerPosition;
  private Vector2 _cliffsPosition;
  private CollisionShape2D _cliffsCollider;
  private bool _isPlayerIntersectingCliffs;
  private bool _isPlayerInWaterfall;
  private AudioStreamPlayer _ambiencePlayer;
  private AudioStreamPlayer _musicPlayer;
  private TileMap _iceTileMap;
  private Vector2 _topLeft;
  private Vector2 _bottomRight;
  private Vector2 _topRight;
  private Vector2 _bottomLeft;
  private readonly Dictionary <Season, int> _waterfallZIndex = new();
  private readonly Dictionary <Season, AudioStream> _music = new();
  private readonly Dictionary <Season, AudioStream> _ambience = new();
  private readonly Dictionary <Season, float> _musicVolumes = new();
  private readonly Dictionary <Season, float> _ambienceVolumes = new();
  private Color _clearColor;
  private bool _seasonChangeInProgress;
  private Season _newSeason;
  private bool _fadeIn;
  private bool _skipFade;
  private Log _log;

  public override void _Ready()
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (Name);
    _clearColor = InitialClearColor;
    VisualServer.SetDefaultClearColor (_clearColor);
    _waterfallZIndex.Add (Season.Summer, 2);
    _waterfallZIndex.Add (Season.Winter, 1);
    _ambience.Add (Season.Summer, ResourceLoader.Load <AudioStream> ("res://ambience_summer.wav"));
    _ambience.Add (Season.Winter, ResourceLoader.Load <AudioStream> ("res://ambience_winter.wav"));
    _ambienceVolumes.Add (Season.Summer, -10);
    _ambienceVolumes.Add (Season.Winter, -20);
    _music.Add (Season.Winter, ResourceLoader.Load <AudioStream> ("res://music6.wav"));
    _music.Add (Season.Summer, ResourceLoader.Load <AudioStream> ("res://music5.wav"));
    _musicVolumes.Add (Season.Winter, -15);
    _musicVolumes.Add (Season.Summer, -25);
    _ambiencePlayer = GetNode <AudioStreamPlayer> ("../AmbiencePlayer");
    _musicPlayer = GetNode <AudioStreamPlayer> ("../MusicPlayer");
    _cliffsCollider = GetNode <CollisionShape2D> ("CollisionShape2D");
    _cliffsPosition = _cliffsCollider.GlobalPosition;
    _iceTileMap = GetNode <TileMap> ("Ice");
    InitializeSeasons();
  }

  public override void _Process (float delta)
  {
    Update();
    UpdateSeasons (delta);
    UpdatePlayer();
  }

  // Godot input callback
  public override void _UnhandledInput (InputEvent @event)
  {
    if (IsReleased (Tools.Input.Season, @event) && !_seasonChangeInProgress) NextSeason();
    if (IsReleased (Tools.Input.Music, @event) && Name == "Upper Cliffs") ToggleMusic();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnWaterfallEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"{area.GetParent().Name} entered waterfall.");
    _isPlayerInWaterfall = true;
    GetNode <Player> ("../Player").IsInFrozenWaterfall = CurrentSeason == Season.Winter;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnWaterfallExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"{area.GetParent().Name} exited waterfall.");
    _isPlayerInWaterfall = false;
    GetNode <Player> ("../Player").IsInFrozenWaterfall = false;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCliffsEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _playerArea = area;
    _isPlayerIntersectingCliffs = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCliffsExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _playerArea = area;
    _isPlayerIntersectingCliffs = false;
    GetNode <Player> ("../Player").IsInCliffs = false;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCliffsGroundEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Debug ($"{area.GetParent().Name} entered ground of {Name}.");
    GetNode <Player> ("../Player").IsInGround = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCliffsGroundExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Debug ($"{area.GetParent().Name} exited ground of {Name}.");
    GetNode <Player> ("../Player").IsInGround = false;
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

  private void UpdateWaterfall (Season season)
  {
    if (Name != "Upper Cliffs") return;

    var waterfall = GetNode <Area2D> ("Waterfall");
    waterfall.Visible = true;
    waterfall.ZIndex = _waterfallZIndex[season];

    foreach (Node node1 in waterfall.GetChildren())
    {
      if (node1 is not AnimatedSprite sprite) continue;

      sprite.Playing = season != Season.Winter;
      sprite.Visible = true;

      foreach (Node node2 in sprite.GetChildren())
      {
        if (node2 is not AudioStreamPlayer2D sound) continue;

        sound.Playing = season != Season.Winter;
      }
    }

    GetNode <Player> ("../Player").IsInFrozenWaterfall = _isPlayerInWaterfall && season == Season.Winter;
  }

  private void SetGroupVisible (string groupName, bool isVisible)
  {
    foreach (Node node in GetTree().GetNodesInGroup (groupName))
    {
      if (node is not CanvasItem item) continue;

      _log.Debug ($"Setting {item.Name} {(item.Visible ? " visible" : " invisible")}");
      item.Visible = isVisible;
    }
  }

  private float _currentSeasonFadeInTimeSeconds;
  private float _currentSeasonFadeOutTimeSeconds;

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
      _fadeIn = AreColorsAlmostEqual (Modulate, Colors.Black, 0.01f) && AreColorsAlmostEqual (_clearColor, Colors.Black, 0.1f);

      return;
    }

    if (_skipFade || AreColorsAlmostEqual (Modulate, Colors.Black, 0.01f) && AreColorsAlmostEqual (_clearColor, Colors.Black, 0.1f))
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
      UpdateWaterfall (_newSeason);
    }

    if (!_skipFade)
    {
      _currentSeasonFadeInTimeSeconds += delta;
      Modulate = Modulate.LinearInterpolate (Colors.White, _currentSeasonFadeInTimeSeconds * 0.1f);
      _clearColor = _clearColor.LinearInterpolate (InitialClearColor, _currentSeasonFadeInTimeSeconds * 0.1f);
      VisualServer.SetDefaultClearColor (_clearColor);
    }

    _seasonChangeInProgress = !AreColorsAlmostEqual (Modulate, Colors.White, 0.01f) ||
                              !AreColorsAlmostEqual (_clearColor, InitialClearColor, 0.1f);

    if (_seasonChangeInProgress) return;

    CurrentSeason = _newSeason;
    _musicPlayer.Play();
    _log.Info ($"Current season is now: {CurrentSeason}");
  }

  private void UpdatePlayer()
  {
    if (!_isPlayerIntersectingCliffs) return;

    // @formatter:off

    _playerExtents = GetExtents (_playerArea);
    _cliffsExtents = GetExtents (this);
    _playerPosition = _playerArea.GlobalPosition;
    _cliffsPosition = _cliffsCollider.GlobalPosition;

    _playerRect.Position = _playerPosition - _playerExtents;
    _playerRect.Size = _playerExtents * 2;
    _cliffsRect.Position = _cliffsPosition - _cliffsExtents;
    _cliffsRect.Size = _cliffsExtents * 2;

    GetNode <Player> ("../Player").IsInCliffs = _isPlayerIntersectingCliffs && _cliffsRect.Encloses (_playerRect);

    _topLeft = _playerArea.GlobalPosition - _playerExtents;
    _bottomRight = _playerArea.GlobalPosition + _playerExtents;
    _topRight.x = _playerArea.GlobalPosition.x + _playerExtents.x;
    _topRight.y = _playerArea.GlobalPosition.y - _playerExtents.y;
    _bottomLeft.x = _playerArea.GlobalPosition.x - _playerExtents.x;
    _bottomLeft.y = _playerArea.GlobalPosition.y + _playerExtents.y;

    GetNode <Player> ("../Player").IsTouchingCliffIce = _iceTileMap.Visible &&
                                                        (IsIntersectingAnyTile (_topLeft, _iceTileMap) ||
                                                         IsIntersectingAnyTile (_bottomRight, _iceTileMap) ||
                                                         IsIntersectingAnyTile (_topRight, _iceTileMap) ||
                                                         IsIntersectingAnyTile (_bottomLeft, _iceTileMap));

    // @formatter:on
  }

  private void ToggleMusic() => _musicPlayer.Playing = !_musicPlayer.Playing;
}