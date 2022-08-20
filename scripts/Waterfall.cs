using System;
using System.Collections.Generic;
using Godot;
using static Tools;

public class Waterfall : Area2D
{
  [Export] public Log.Level LogLevel = Log.Level.Info;
  public bool IsInWaterfall { get; private set; }
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
  private readonly Dictionary <Cliffs.Season, int> _zIndex = new();
  private readonly Dictionary <Cliffs.Season, int> _poolZIndex = new();
  private readonly Dictionary <Cliffs.Season, int> _wavesZIndex = new();
  private readonly Dictionary <Cliffs.Season, int> _innerMistsZIndex = new();
  private readonly Dictionary <Cliffs.Season, int> _outerMistsZIndex = new();

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
    _zIndex.Add (Cliffs.Season.Summer, 33);
    _zIndex.Add (Cliffs.Season.Winter, 1);
    _poolZIndex.Add (Cliffs.Season.Summer, -32);
    _poolZIndex.Add (Cliffs.Season.Winter, -1);
    _wavesZIndex.Add (Cliffs.Season.Summer, -32);
    _wavesZIndex.Add (Cliffs.Season.Winter, 1);
    _outerMistsZIndex.Add (Cliffs.Season.Summer, 2);
    _outerMistsZIndex.Add (Cliffs.Season.Winter, 33);
    _innerMistsZIndex.Add (Cliffs.Season.Summer, 1);
    _innerMistsZIndex.Add (Cliffs.Season.Winter, 33);
    // @formatter:on
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnWaterfallEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"Player entered {Name}.");
    IsInWaterfall = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnWaterfallExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"Player exited {Name}.");
    IsInWaterfall = false;
  }

  public void OnSeasonChange (Cliffs.Season season)
  {
    Visible = true;
    ZIndex = _zIndex[season];
    _pool.ZIndex = _poolZIndex[season];
    _waves.ZIndex = _wavesZIndex[season];
    _innerMists.ForEach (x => x.ZIndex = _innerMistsZIndex[season]);
    _outerMists.ForEach (x => x.ZIndex = _outerMistsZIndex[season]);
    var isWinter = season == Cliffs.Season.Winter;
    var isSummer = season == Cliffs.Season.Summer;

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

  private void UpdateAudio (Cliffs.Season season) =>
    _audioPlayers.ForEach (x =>
    {
      LoopAudio (x.Stream);
      x.Playing = season != Cliffs.Season.Winter;
    });
}