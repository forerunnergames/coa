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

  [Export]
  public bool MusicPlaying
  {
    get => _musicPlayer?.Playing ?? true;

    // ReSharper disable once ValueParameterNotUsed
    set
    {
      if (_musicPlayer == null) return;

      _musicPlayer.Playing = !_musicPlayer.Playing;
      _seasons?.ToggleMusic();
    }
  }

  private Seasons _seasons;
  private TileMap _iceTileMap;
  private List <Rect2> _cliffRects;
  private List <CollisionShape2D> _colliders;
  private AudioStreamPlayer _musicPlayer;

  public override void _Ready()
  {
    _iceTileMap = GetNode <TileMap> ("Ice");
    _colliders = GetNodesInGroup <CollisionShape2D> (GetTree(), "Extents");
    _musicPlayer = GetNode <AudioStreamPlayer> ("../Audio Players/Music");
    _seasons = new Seasons (GetTree(), InitialSeason, InitialClearColor, MusicPlaying, LogLevel);
    _cliffRects = new List <Rect2> (_colliders.Select (x => GetColliderRect (this, x)));
    if (MusicPlaying) _musicPlayer.Play();
  }

  public override void _Process (float delta)
  {
    Update();
    UpdateSeasons (delta);
  }

  public override void _UnhandledInput (InputEvent @event)
  {
    _seasons.OnInput (@event);
    if (IsReleased (Tools.Input.Music, @event)) ToggleMusic();
  }

  // @formatter:off
  public Season GetCurrentSeason() => _seasons.CurrentSeason;
  public bool CurrentSeasonIs (Season season) => _seasons.CurrentSeason == season;
  public bool Encloses (Rect2 rect) => IsEnclosedBy (rect, _cliffRects);
  public bool IsTouchingIce (Rect2 rect) => _iceTileMap.Visible && IsIntersectingAnyTile (rect, _iceTileMap);
  private void UpdateSeasons (float delta) => _seasons.Update (GetTree(), this, delta);
  // @formatter:on

  private void ToggleMusic()
  {
    _musicPlayer.Playing = !_musicPlayer.Playing;
    _seasons.ToggleMusic();
  }
}