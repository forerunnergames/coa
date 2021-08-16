using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using static Tools;
using Color = Godot.Color;

public class Butterfly : AnimatedSprite
{
  [Export] public string IdleAnimation = "butterfly_idle";
  [Export] public string FlyingAnimation = "butterfly_flying";
  [Export] public int FlyingSpeed = 300;
  [Export] public int MaxIdleTimeSeconds = 60;
  [Export] public int MinIdleTimeSeconds = 2;
  [Export] public float IdleStartDelayTimeSeconds = 0.75f;
  [Export] public int OscillationWidth = 5;
  [Export] public int MaxOscillationHeight = 10;
  [Export] public int MinOscillationHeight = 1;
  [Export] public int MaxOscillationAngleVariationDegrees = 30;

  [Export]
  public bool DrawFlightPath
  {
    get => _drawFlightPath;
    set
    {
      _drawFlightPath = value;
      if (_flightPathCurve != null) _flightPathCurve.DrawPath = _drawFlightPath;
    }
  }

  [Export]
  public Color FlightPathColor
  {
    get => _flightPathColor;
    set
    {
      _flightPathColor = value;
      if (_flightPathCurve != null) _flightPathCurve.DrawColor = _flightPathColor;
    }
  }

  private Color _flightPathColor = Colors.Yellow;
  private bool _drawFlightPath;
  private CatmullRom _flightPathCurve;
  private Timer _idleTimer = null!;
  private Timer _flightTimer = null!;
  private IStateMachine <State> _stateMachine = null!;
  private Vector2 _lastPosition;
  private float _lerp;
  private CatmullRom.Draw _draw;
  private bool _isOnFlower;
  private bool _wasDisturbed;
  private readonly RandomNumberGenerator _rng = new();
  private readonly List <Vector2> _path = new();
  private readonly float _ninety = Mathf.Deg2Rad (90);
  private float _maxOscillationAngleVariation;
  private readonly List <Vector2> _flowers = new();
  private readonly List <Vector2> _flowersUnvisited = new();
  private Log _log;

  private enum State
  {
    Idle,
    Flying
  }

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.Idle, new[] { State.Flying } }, { State.Flying, new[] { State.Idle } }
  };

  [SuppressMessage ("ReSharper", "ExplicitCallerInfoArgument")]
  public override void _Ready()
  {
    _log = new Log (Name);
    _rng.Randomize();
    _maxOscillationAngleVariation = Mathf.Deg2Rad (MaxOscillationAngleVariationDegrees);
    _draw = delegate (Vector2[] points, Color[] colors, Vector2[] uvs) { DrawPrimitive (points, colors, uvs); };
    _idleTimer = GetNode <Timer> ("IdleTimer");
    _flightTimer = GetNode <Timer> ("FlightTimer");
    _lastPosition = Position;
    _stateMachine = new StateMachine <State> (TransitionTable, State.Flying, Name);
    _stateMachine.OnTransitionTo (State.Idle, StartIdling);
    _stateMachine.OnTransitionTo (State.Flying, StartFlying);
    _stateMachine.OnTransitionFrom (State.Idle, _idleTimer.Stop);
    _stateMachine.OnTransitionFrom (State.Flying, _flightTimer.Stop);
    _stateMachine.AddTrigger (State.Idle, State.Flying, () => _idleTimer.IsStopped() || _wasDisturbed);
    _stateMachine.AddTrigger (State.Flying, State.Idle, () => _isOnFlower || ReachedDestination());
    InitializeFlowers();
    StartFlying();
    Play();
  }

  private void CreatePathFromTo (Vector2 from, Vector2 to)
  {
    _path.Clear();
    _path.Add (from);
    var oscillations = Mathf.CeilToInt (from.DistanceTo (to) / OscillationWidth);
    var delta = to - from;
    var deltaLerp = delta / oscillations;
    var oscillationOrigin = from;
    var angleOfStraightPath = Mathf.Atan2 (delta.y, delta.x);

    for (var i = 0; i < oscillations; ++i)
    {
      oscillationOrigin += deltaLerp;
      var oscillationRadius = _rng.RandfRange (MinOscillationHeight, MaxOscillationHeight);
      var angleOffset = _rng.RandfRange (-_maxOscillationAngleVariation, _maxOscillationAngleVariation);
      var angle1Offset = angleOfStraightPath - _ninety + angleOffset;
      var angle2Offset = angleOfStraightPath + _ninety + angleOffset;
      var rCosAngle1 = oscillationRadius * Mathf.Cos (angle1Offset);
      var rCosAngle2 = oscillationRadius * Mathf.Cos (angle2Offset);
      var rSinAngle1 = oscillationRadius * Mathf.Sin (angle1Offset);
      var rSinAngle2 = oscillationRadius * Mathf.Sin (angle2Offset);
      _path.Add (new Vector2 (oscillationOrigin.x + rCosAngle1, oscillationOrigin.y + rSinAngle1));
      _path.Add (new Vector2 (oscillationOrigin.x + rCosAngle2, oscillationOrigin.y + rSinAngle2));
    }

    _path.RemoveAt (_path.Count - 1);
    _path.Add (to);
    _flightPathCurve = new CatmullRom (_path, ToGlobal) { DrawPath = DrawFlightPath };
  }

  // TODO Secondarily, land in grass if there are no flowers.
  private Vector2 FindFlower()
  {
    if (_flowers.Count == 0) return Position;

    if (_flowersUnvisited.Count == 0)
    {
      _flowersUnvisited.AddRange (_flowers.Where (x => Position.DistanceTo (x) > 0));
      _log.Debug ($"Replenished {_flowersUnvisited.Count} unvisited flowers");
    }

    var flower = _rng.Randf() > 0.2f
      ? _flowersUnvisited.OrderBy (x => Position.DistanceTo (x)).First()
      : _flowersUnvisited[_rng.RandiRange (0, _flowersUnvisited.Count - 1)];

    _log.Debug ($"Found flower: {flower}, distance: {Position.DistanceTo (flower)}");

    return flower;
  }

  // ReSharper disable once UnusedMember.Global
  public void _on_LandingCollider_body_entered (Node body)
  {
    if (body is not TileMap { Name: "Flowers" } tileMap) return;

    var flower = GetIntersectingTileCellGlobalPosition (Position, tileMap) + GetCellCenterOffset (tileMap);
    var wasRemoved = _flowersUnvisited.Remove (flower);

    if (wasRemoved)
    {
      _log.Debug ($"Removed visited flower {flower} at cell {GetIntersectingTileCell (Position, tileMap)}.");
      _log.Debug ($"{_flowersUnvisited.Count} / {_flowers.Count} unvisited flowers remaining.");
    }

    _isOnFlower = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _on_LandingCollider_body_exited (Node body)
  {
    if (body.Name != "Flowers") return;

    _isOnFlower = false;
  }

  // ReSharper disable once UnusedMember.Global
  public void _on_ObstacleCollider_body_entered (Node body)
  {
    if (body.IsInGroup ("Ground") || body.IsInGroup ("Foliage")) return;

    _wasDisturbed = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _on_ObstacleCollider_body_exited (Node body)
  {
    if (body.IsInGroup ("Ground") || body.IsInGroup ("Foliage")) return;

    _wasDisturbed = false;
  }

  public override void _Draw() => _flightPathCurve.DrawPoints (_draw, () => ToLocal);

  public override void _Process (float delta)
  {
    if (!Visible) return;

    if (_stateMachine.Is (State.Flying)) Fly (delta);
    if (_stateMachine.Is (State.Idle)) Idle();
    _stateMachine.Update();
    Update();
  }

  private void Fly (float delta)
  {
    Position = _lastPosition;
    _lerp += FlyingSpeed * delta; // TODO Use nonlinear interpolation to accelerate / decelerate.
    Position = _flightPathCurve.GetSplinePoint (_lerp);
  }

  private void Idle()
  {
    if (_idleTimer.TimeLeft > MaxIdleTimeSeconds - IdleStartDelayTimeSeconds || Animation == IdleAnimation) return;

    Animation = IdleAnimation;
  }

  private void StartFlying()
  {
    Animation = FlyingAnimation;
    _isOnFlower = false;
    CreatePathFromTo (ToLocal (Position), ToLocal (FindFlower()));
    _lastPosition = Position;
    _lerp = 0;
  }

  private void InitializeFlowers()
  {
    _flowers.Clear();
    _flowersUnvisited.Clear();

    foreach (TileMap tileMap in GetTree().GetNodesInGroup ("Foliage"))
    {
      if (tileMap is not { Name: "Flowers" }) continue;

      foreach (Vector2 cell in tileMap.GetUsedCells())
      {
        _flowers.Add (GetTileCellGlobalPosition (cell, tileMap) + new Vector2 (64, 64));
      }
    }

    _flowersUnvisited.AddRange (_flowers);
    _log.Debug ($"Found {_flowers.Count} flowers.");
  }

  private void StartIdling()
  {
    _idleTimer.WaitTime = _rng.RandiRange (MinIdleTimeSeconds, MaxIdleTimeSeconds);
    _idleTimer.Start();
  }

  private bool ReachedDestination() => _lerp >= _flightPathCurve.Length;
}