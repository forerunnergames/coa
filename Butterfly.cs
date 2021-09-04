using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using static Tools;

// TODO Adjust idle position to synchronize with sprite animation for KinematicPerch.
// TODO Use nonlinear interpolation to accelerate / decelerate.

public class Butterfly : AnimatedSprite
{
  [Export] public string IdleAnimation = "butterfly_idle";
  [Export] public string EvadingAnimation = "butterfly_evading";
  [Export] public string PerchingAnimation = "butterfly_perching";
  [Export] public string FlyingAnimation = "butterfly_flying";
  [Export] public int FlyingSpeed = 300;
  [Export] public int EvadingSpeed = 400;
  [Export] public int PerchingSpeed = 200;
  [Export] public int MaxIdleTimeSeconds = 60;
  [Export] public int MinIdleTimeSeconds = 2;
  [Export] public float IdleStartDelayTimeSeconds = 0.75f;
  [Export] public int OscillationWidth = 5;
  [Export] public int MaxOscillationHeight = 10;
  [Export] public int MinOscillationHeight = 1;
  [Export] public int MaxOscillationAngleVariationDegrees = 30;
  [Export] public float NearestPerchFrequency = 0.8f;
  [Export] public float PositionEpsilon = 0.01f;
  [Export] public Log.Level LogLevel = Log.Level.Off;

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

  [Export]
  public bool DrawPerchableAreas
  {
    get => _perchableDrawPrefs.DrawAreas;
    set
    {
      _perchableDrawPrefs.DrawAreas = value;
      if (_perch != null) _perch.DrawPrefs.DrawAreas = _perchableDrawPrefs.DrawAreas;
    }
  }

  [Export]
  public Color PerchableAreaColor
  {
    get => _perchableDrawPrefs.AreasColor;
    set
    {
      _perchableDrawPrefs.AreasColor = value;
      if (_perch != null) _perch.DrawPrefs.AreasColor = _perchableDrawPrefs.AreasColor;
    }
  }

  [Export]
  public bool DrawPerchableAreasFilled
  {
    get => _perchableDrawPrefs.AreasFilled;
    set
    {
      _perchableDrawPrefs.AreasFilled = value;
      if (_perch != null) _perch.DrawPrefs.AreasFilled = _perchableDrawPrefs.AreasFilled;
    }
  }

  [Export]
  public bool DrawPerchPoint
  {
    get => _perchableDrawPrefs.DrawPerchPoint;
    set
    {
      _perchableDrawPrefs.DrawPerchPoint = value;
      if (_perch != null) _perch.DrawPrefs.DrawPerchPoint = _perchableDrawPrefs.DrawPerchPoint;
    }
  }

  [Export]
  public Color PerchPointColor
  {
    get => _perchableDrawPrefs.PerchPointColor;
    set
    {
      _perchableDrawPrefs.PerchPointColor = value;
      if (_perch != null) _perch.DrawPrefs.PerchPointColor = _perchableDrawPrefs.PerchPointColor;
    }
  }

  [Export]
  public bool DrawPerchPointFilled
  {
    get => _perchableDrawPrefs.PerchPointFilled;
    set
    {
      _perchableDrawPrefs.PerchPointFilled = value;
      if (_perch != null) _perch.DrawPrefs.PerchPointFilled = _perchableDrawPrefs.PerchPointFilled;
    }
  }

  private Color _flightPathColor = Colors.Yellow;
  private bool _drawFlightPath;
  private CatmullRom _flightPathCurve;
  private Timer _idleTimer = null!;
  private IStateMachine <State> _stateMachine = null!;
  private Vector2 _lastPosition;
  private float _lerp;
  private DrawPrimitive _drawPrimitive;
  private DrawRect _drawRect;
  private IPerchable _perch;
  private Vector2 _perchPoint;
  private Node _perchingBody;
  private float _maxOscillationAngleVariation;
  private readonly RandomNumberGenerator _rng = new();
  private readonly List <Vector2> _path = new();
  private readonly float _ninety = Mathf.Deg2Rad (90);
  private readonly List <IPerchable> _perches = new();
  private readonly List <IPerchable> _perchesUnvisited = new();
  private readonly List <Node2D> _perchableNodes = new();
  private readonly PerchableDrawPrefs _perchableDrawPrefs = new();
  private Log _log;

  private enum State
  {
    Idle,
    Flying,
    Evading,
    Perching
  }

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.Idle, new[] { State.Flying, State.Evading } },
    { State.Flying, new[] { State.Perching, State.Evading } },
    { State.Evading, new[] { State.Flying } },
    { State.Perching, new[] { State.Idle, State.Flying, State.Evading } }
  };

  [SuppressMessage ("ReSharper", "ExplicitCallerInfoArgument")]
  public override void _Ready()
  {
    _log = new Log (Name) { CurrentLevel = LogLevel };
    _rng.Randomize();
    Animation = FlyingAnimation;
    _maxOscillationAngleVariation = Mathf.Deg2Rad (MaxOscillationAngleVariationDegrees);
    _drawPrimitive = delegate (Vector2[] points, Color[] colors, Vector2[] uvs) { DrawPrimitive (points, colors, uvs); };
    _drawRect = delegate (Rect2 rect, Color color, bool filled) { DrawRect (rect, color, filled); };
    _idleTimer = GetNode <Timer> ("IdleTimer");
    _lastPosition = Position;
    _stateMachine = new StateMachine <State> (TransitionTable, State.Flying, Name);
    _stateMachine.OnTransitionTo (State.Perching, () => Animation = PerchingAnimation);
    _stateMachine.OnTransitionTo (State.Evading, () => Animation = EvadingAnimation);
    _stateMachine.OnTransitionTo (State.Flying, () => Animation = FlyingAnimation);
    _stateMachine.OnTransitionTo (State.Idle, StopMoving);
    _stateMachine.OnTransitionFrom (State.Idle, _idleTimer.Stop);
    _stateMachine.OnTransition (State.Idle, State.Evading, StartMoving);
    _stateMachine.OnTransition (State.Idle, State.Flying, StartMoving);
    _stateMachine.AddTrigger (State.Idle, State.Flying, () => _idleTimer.IsStopped());
    _stateMachine.AddTrigger (State.Flying, State.Perching, ArrivedAtPerch);
    InitializePerches();
    StartMoving();
    Play();
  }

  public override void _Process (float delta)
  {
    if (!Visible) return;

    foreach (Node2D node in GetTree().GetNodesInGroup ("Perchable")) _perchableNodes.Add (node);

    foreach (var node in _perchableNodes)
    {
      if (node is not KinematicBody2D) continue;

      foreach (var perch in _perches.Where (x => x.Name == node.Name))
      {
        if (!AreAlmostEqual (perch.GlobalOrigin, node.Position, PositionEpsilon))
        {
          _log.Debug ($"Updating outdated perch position from: {perch.GlobalOrigin} to  {node.Position}");
          perch.GlobalOrigin = node.Position;
        }

        // x & y are intentionally reversed here, see: https://github.com/godotengine/godot/issues/17405
        if (Mathf.Sign (node.Scale.y) == -1 && !perch.FlippedHorizontally)
        {
          perch.FlipHorizontally();
        }

        if (Mathf.Sign (node.Scale.x) == -1 && !perch.FlippedVertically) perch.FlipVertically();
        if (Mathf.Sign (node.Scale.y) == 1 && perch.FlippedHorizontally) perch.FlipHorizontally();
        if (Mathf.Sign (node.Scale.x) == 1 && perch.FlippedVertically) perch.FlipVertically();

        if (!perch.Disabled) continue;

        _log.Debug ($"Re-enabling perchable perch, node: {node}, perch: {perch}");
        perch.Disabled = false;
      }
    }

    foreach (var perch in _perches.Where (x =>
      _perchableNodes.All (y => x.Name != y.Name && !x.HasParentName (y.Name)) && !x.Disabled))
    {
      _log.Debug ($"Disabling un-perchable perch: {perch}");
      _log.Debug ($"Perchable nodes: {_perchableNodes.Count}.");
      perch.Disabled = true;

      if (_perch.Name != perch.Name) continue;

      var previousState = _stateMachine.GetState();
      _stateMachine.To (State.Flying);
      if (previousState != State.Idle) StartMoving();
    }

    _perchableNodes.Clear();
    if (_stateMachine.Is (State.Flying)) Fly (delta);
    if (_stateMachine.Is (State.Evading)) Evade (delta);
    if (_stateMachine.Is (State.Perching)) Perch (delta);
    _stateMachine.Update();
    Update();
  }

  public override void _Draw()
  {
    _flightPathCurve.Draw (_drawPrimitive, () => ToLocal);
    _perch.Draw (_drawRect, _perchPoint, () => ToLocal, () => GlobalScale);
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnPerchingColliderEntered (Node body)
  {
    if (!body.IsInGroup ("Perchable")) return;

    _log.All ($"Entering perch: {NameOf (body, Position)}.");
    _perchingBody = body;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnObstacleColliderEntered (Node body)
  {
    if (body is StaticBody2D || body.IsInGroup ("Perchable") || body.IsInGroup ("Ground")) return;

    _log.Debug ($"Encountered obstacle: {NameOf (body, Position)}.");
    _stateMachine.To (State.Evading);
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnObstacleColliderExited (Node body)
  {
    if (body is StaticBody2D || body.IsInGroup ("Perchable") || body.IsInGroup ("Ground")) return;

    _log.Debug ($"Evaded obstacle: {NameOf (body, Position)}.");
    _stateMachine.To (State.Flying);
  }

  private void Fly (float delta)
  {
    Move (FlyingSpeed, delta);

    if (!ReachedDestination()) return;

    ChangeDestination();
  }

  private void Evade (float delta)
  {
    Move (EvadingSpeed, delta);

    if (!ReachedDestination()) return;

    ChangeDestination();
  }

  private void Perch (float delta)
  {
    if (!_perch.Contains (Position) || !AreAlmostEqual (Position, _perchPoint, PositionEpsilon))
    {
      _log.All (
        $"Found correct perch: {NameOf (_perchingBody, Position)}. Continuing perching along same path from position {Position} to reach perch point {_perchPoint}.");

      Move (PerchingSpeed, delta);

      return;
    }

    _stateMachine.To (State.Idle);
    _perchesUnvisited.Remove (_perch);
    if (ReachedDestination()) _log.Debug ($"Reached destination! Position: {Position}, perch point: {_perchPoint}.");
    _log.Debug ($"Perched on {_perch.Name} at position {Position}.");
    _log.Debug ($"Removed visited perch {_perch.Name}.");
    _log.Debug ($"{_perchesUnvisited.Count (x => !x.Disabled)} / {_perches.Count} unvisited, enabled perches remaining.");
    _log.Debug ($"Perch details:\n{_perch}");
  }

  private void StartMoving()
  {
    _lerp = 0;
    _lastPosition = Position;
    _perch = FindPerch();
    _perchPoint = _perch.RandomPoint (_rng, () => GlobalScale);
    CreatePathFromTo (ToLocal (Position), ToLocal (_perchPoint));
  }

  private void StopMoving()
  {
    Animation = IdleAnimation;
    _idleTimer.WaitTime = _rng.RandiRange (MinIdleTimeSeconds, MaxIdleTimeSeconds);
    _idleTimer.Start();
  }

  private void Move (float speed, float delta)
  {
    Position = _lastPosition;
    _lerp += speed * delta;
    Position = _flightPathCurve.GetSplinePoint (_lerp);
  }

  private bool ArrivedAtPerch()
  {
    if (_perchingBody == null) return false;

    IPerchable perch = null;
    var position = Vector2.Zero;
    var cell = Vector2.Zero;

    switch (_perchingBody)
    {
      case TileMap tileMap:
      {
        cell = GetIntersectingTileCell (Position, tileMap);
        position = GetIntersectingTileCellGlobalPosition (Position, tileMap);
        perch = _perchesUnvisited.FirstOrDefault (x => x.Is (GetIntersectingTileName (Position, tileMap), position));

        break;
      }
      case KinematicBody2D body:
      {
        position = body.Position;
        perch = _perchesUnvisited.FirstOrDefault (x => x.Is (_perchingBody.Name, position));

        break;
      }
    }

    if (_perch.Equals (perch)) return true;

    _log.All ($"Found wrong perch: {NameOf (_perchingBody, Position)} at position {position}, cell {cell}.");
    _log.All ($"_perch: {_perch}, perch: {perch}, _perchingBody: {_perchingBody}");
    _log.Debug ($"Continuing flying along same path toward correct perch:\n{_perch}.");
    _perchingBody = null;

    return false;
  }

  private bool ReachedDestination() => _lerp >= _flightPathCurve.Length;
  private bool AreUnvisitedPerchesDepleted() => _perchesUnvisited.All (x => x.Disabled);
  private int UnvisitedPerchCount() => _perchesUnvisited.Count (x => !x.Disabled);

  private IPerchable FindPerch()
  {
    IPerchable perch = null;
    if (AreUnvisitedPerchesDepleted()) ReplenishUnvisitedPerches();

    if (_rng.Randf() <= NearestPerchFrequency)
    {
      perch = _perchesUnvisited.Where (x => !x.Disabled).OrderBy (x => x.DistanceFrom (Position))
        .FirstOrDefault (x => !x.Contains (Position));
    }

    perch ??= _perchesUnvisited.Where (x => !x.Disabled).ElementAt (_rng.RandiRange (0, UnvisitedPerchCount() - 1));
    _log.Debug ($"Found perch {perch.Name} at distance [{Position.DistanceTo (perch.GlobalOrigin)}], details: \n    {perch}");

    return perch;
  }

  private void ReplenishUnvisitedPerches()
  {
    if (!AreUnvisitedPerchesDepleted()) return;

    _perchesUnvisited.AddRange (_perches.Where (x => !x.Disabled && !x.Contains (Position)));
    if (AreUnvisitedPerchesDepleted()) _perchesUnvisited.AddRange (_perches.Where (x => !x.Disabled));

    if (AreUnvisitedPerchesDepleted())
    {
      var perch = _perchesUnvisited.FirstOrDefault (x => x.Name == "Default") ?? _perches.First (x => x.Name == "Default");
      perch.Disabled = false;
      if (_perchesUnvisited.All (x => x.Name != "Default")) _perchesUnvisited.Add (perch);
      _log.Warn ($"No enabled perches found. Using default perch: {perch}");
    }

    _log.Debug ($"Replenished {_perchesUnvisited.Count (x => !x.Disabled)} unvisited perches.");
  }

  private void InitializePerches()
  {
    _perches.Clear();
    _perchesUnvisited.Clear();

    foreach (Node2D node in GetTree().GetNodesInGroup ("Perchable"))
    {
      _perches.AddRange (PerchablesFactory.Create (node, _perchableDrawPrefs, PositionEpsilon));
    }

    var perch = PerchablesFactory.CreateDefault (this, _perchableDrawPrefs, PositionEpsilon);
    perch.Disabled = true;
    _perches.Add (perch);
    ReplenishUnvisitedPerches();
    _log.Debug ($"Found {UnvisitedPerchCount()} enabled, unvisited perches.");
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

    if (_path.Count > 1) _path.RemoveAt (_path.Count - 1);
    _path.Add (to);
    _flightPathCurve = new CatmullRom (_path, ToGlobal) { DrawPath = DrawFlightPath, DrawColor = FlightPathColor };
  }

  private void ChangeDestination()
  {
    _log.Debug ($"Changing destination from perch: {_perch}");
    _perchesUnvisited.Remove (_perch);
    StartMoving();
  }
}