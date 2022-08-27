using System.Collections.Generic;
using Godot;
using static Seasons;
using static Tools;

public class Waterfall : Area2D
{
  [Export] public Log.Level LogLevel = Log.Level.Info;
  public bool IsPlayerInWaterfall { get; private set; }
  public bool IsPlayerInFrozenWaterfall { get; private set; }
  private Log _log;
  private Node2D _pool;
  private Node2D _waves;
  private List <Node2D> _innerMists;
  private List <Node2D> _outerMists;
  private List <AnimatedSprite> _animations;
  private List <CollisionObject2D> _winterGrounds;
  private List <CollisionObject2D> _summerGrounds;
  private List <AudioStreamPlayer2D> _audioPlayers;
  private List <AudioStreamPlayer2D> _attenuateables;
  private readonly Dictionary <Season, int> _zIndex = new();
  private readonly Dictionary <Season, int> _poolZIndex = new();
  private readonly Dictionary <Season, int> _wavesZIndex = new();
  private readonly Dictionary <Season, int> _innerMistsZIndex = new();
  private readonly Dictionary <Season, int> _outerMistsZIndex = new();
  private Season _currentSeason;

  public override void _Ready()
  {
    // @formatter:off
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (Name) { CurrentLevel = LogLevel };
    _animations = GetNodesInGroupWithParent <AnimatedSprite> (Name, GetTree(), "Waterfall");
    _innerMists = GetNodesInGroupsWithParent <Node2D> (Name, GetTree(), "Waterfall", "Inner Mist");
    _outerMists = GetNodesInGroupsWithParent <Node2D> (Name, GetTree(), "Waterfall", "Outer Mist");
    _audioPlayers = GetNodesInGroupsWithParent <AudioStreamPlayer2D> (Name, GetTree(), "Waterfall", "Audio");
    _winterGrounds = GetNodesInGroupsWithAnyOfParents <CollisionObject2D> (new[] { Name, "Cliffs" }, GetTree(), "Waterfall", "Ground", "Winter");
    _summerGrounds = GetNodesInGroupsWithAnyOfParents <CollisionObject2D> (new[] { Name, "Cliffs" }, GetTree(), "Waterfall", "Ground", "Summer");
    _attenuateables = GetNodesInGroupsWithParent <AudioStreamPlayer2D> (Name, GetTree(), "Waterfall", "Audio", "Attenuateable");
    _pool = GetNode <Node2D> ("Pool");
    _waves = GetNode <Node2D> ("Waves");
    _zIndex.Add (Season.Summer, 33);
    _zIndex.Add (Season.Winter, 1);
    _poolZIndex.Add (Season.Summer, -32);
    _poolZIndex.Add (Season.Winter, -1);
    _wavesZIndex.Add (Season.Summer, -32);
    _wavesZIndex.Add (Season.Winter, 1);
    _outerMistsZIndex.Add (Season.Summer, 2);
    _outerMistsZIndex.Add (Season.Winter, 33);
    _innerMistsZIndex.Add (Season.Summer, 1);
    _innerMistsZIndex.Add (Season.Winter, 33);
    // @formatter:on
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnWaterfallEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"Player entered {Name}.");
    IsPlayerInWaterfall = true;
    IsPlayerInFrozenWaterfall = _currentSeason == Season.Winter;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnWaterfallExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"Player exited {Name}.");
    IsPlayerInWaterfall = false;
    IsPlayerInFrozenWaterfall = false;
  }

  public void OnSeason (Season season)
  {
    _currentSeason = season;
    Visible = true;
    ZIndex = _zIndex[season];
    _pool.ZIndex = _poolZIndex[season];
    _waves.ZIndex = _wavesZIndex[season];
    _innerMists.ForEach (x => x.ZIndex = _innerMistsZIndex[season]);
    _outerMists.ForEach (x => x.ZIndex = _outerMistsZIndex[season]);
    var isWinter = season == Season.Winter;
    var isSummer = season == Season.Summer;
    IsPlayerInFrozenWaterfall = IsPlayerInWaterfall && isWinter;

    _animations.ForEach (x =>
    {
      x.Playing = !isWinter;
      x.Visible = true;
    });

    _winterGrounds.ForEach (x =>
    {
      x.SetCollisionMaskBit (0, isWinter);
      x.SetCollisionLayerBit (1, isWinter);
    });

    _summerGrounds.ForEach (x =>
    {
      x.SetCollisionMaskBit (0, isSummer);
      x.SetCollisionLayerBit (1, isSummer);
    });

    UpdateAudio (season);
  }

  public void ChangeSettings (float soundAttenuation, int outerMistsZIndex, float outerMistsAlphaModulation = 1.0f)
  {
    _attenuateables.ForEach (x => x.Attenuation = soundAttenuation);

    _outerMists.ForEach (x =>
    {
      x.ZIndex = outerMistsZIndex;
      x.Modulate = new Color (Modulate.r, Modulate.g, Modulate.b, outerMistsAlphaModulation);
    });
  }

  private void UpdateAudio (Season season) =>
    _audioPlayers.ForEach (x =>
    {
      LoopAudio (x.Stream);
      x.Playing = season != Season.Winter;
    });
}