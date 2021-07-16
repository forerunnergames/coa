using System;
using System.Collections.Generic;
using Godot;

public class Butterfly : AnimatedSprite
{
  [Export] public string IdleAnimation = "butterfly_idle";
  [Export] public string FlyingAnimation = "butterfly_flying";
  [Export] public int FlyingSpeed = 50;
  [Export] public int IdleTimeSeconds = 5;
  private CatmullRom _flightPathCurve;
  private Timer _idleTimer = null!;
  private Timer _flightTimer = null!;
  private Vector2 _globalFlightPathStart = Vector2.Zero;
  private Vector2 _globalFlightPathDest = Vector2.Zero;

  // private float _globalFlightDistance;
  private Line2D _localFlightPath;

  // private float _totalFlightDurationSeconds;
  private float _lerp;
  private float _endLerp;

  private enum State
  {
    Idle,
    Flying
  }

  private RayCast2D _ray;
  private IStateMachine <State> _stateMachine = null!;

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.Idle, new[] { State.Flying } },
    { State.Flying, new[] { State.Idle } }
  };

  public override void _Ready()
  {
    _flightPathCurve = new CatmullRom();
    _idleTimer = GetNode <Timer> ("IdleTimer");
    _flightTimer = GetNode <Timer> ("FlightTimer");
    _localFlightPath = GetNode <Line2D> ("FlightPath");
    _localFlightPath.AddPoint (_localFlightPath.Points[0], 0);
    _localFlightPath.AddPoint (_localFlightPath.Points[_localFlightPath.GetPointCount() - 1]);
    _ray = GetNode <RayCast2D> ("RayCast2D");
    _idleTimer.WaitTime = IdleTimeSeconds;
    _stateMachine = new StateMachine <State> (TransitionTable, State.Flying);
    _stateMachine.OnTransitionTo (State.Idle, Idle);
    _stateMachine.OnTransitionTo (State.Flying, Fly);
    _stateMachine.OnTransitionFrom (State.Idle, _idleTimer.Stop);
    _stateMachine.OnTransitionFrom (State.Flying, _flightTimer.Stop);
    _stateMachine.AddTrigger (State.Idle, State.Flying, _idleTimer.IsStopped);
    // _stateMachine.AddTrigger (State.Flying, State.Idle, _flightTimer.IsStopped);
    Fly();
    Play();
  }

  public override void _Process (float delta)
  {
    //GD.Print (_flightTimer.WaitTime, " secs, FLIGHT REMAINING: ", _flightTimer.TimeLeft, " secs, ", _flightTimer.IsStopped(), ", IDLE REMAINING: ", ", ", _idleTimer.TimeLeft, " secs, ", GlobalPosition, ", ", _stateMachine.GetState());

    if (_stateMachine.Is (State.Flying))
    {
      //GD.Print (_globalFlightPathStart.LinearInterpolate (_globalFlightPathDest, _lerp));
      //GD.Print (_flightPathCurve.GetSplinePoint (_localFlightPath, _lerp));
      // _lerp += delta / _totalFlightDurationSeconds;
      _lerp += FlyingSpeed / 100.0f * delta;
      _endLerp = _localFlightPath.GetPointCount() - 3;
      // GD.Print (_lerp, ", ", _endLerp);

      if (_lerp < _endLerp)
      {
        // for (var i = 0; i < _localFlightPath.GetPointCount(); ++i)
        // {
        //   _localFlightPath.Points[i] = ToGlobal (_localFlightPath.Points[i]);
        // }
        var point = _flightPathCurve.GetSplinePoint (_localFlightPath, _lerp);
        GlobalPosition = (point * GlobalScale) + new Vector2(128, 0);

        // GD.Print (string.Join (",", _localFlightPath.Points));
        //
        GD.Print (ToGlobal (_flightPathCurve.GetSplinePoint (_localFlightPath, _lerp)));
        // ToGlobal (_flightPathCurve.GetSplinePoint (_localFlightPath, _lerp)), _globalFlightPathStart, _globalFlightPathDest);
      }
      else
      {
        GlobalPosition = _globalFlightPathDest;
        _stateMachine.To (State.Idle);
      }

      // GlobalPosition = _lerp < _endLerp
      //   ? ToGlobal (_flightPathCurve.GetSplinePoint (_localFlightPath, _lerp))
      //   : _globalFlightPathDest;

      // GlobalPosition = !_flightTimer.IsStopped()
      //   ? _globalFlightPathStart.LinearInterpolate (_globalFlightPathDest, _lerp)
      //   : _globalFlightPathDest;
    }

    //GD.Print (_ray.IsColliding ());
    _stateMachine.Update();
  }

  private void Fly()
  {
    Animation = FlyingAnimation;
    _globalFlightPathStart = ToGlobal (_localFlightPath.Points[0]);
    _globalFlightPathDest = ToGlobal (_localFlightPath.Points[_localFlightPath.GetPointCount() - 1]);
    // _globalFlightDistance = _globalFlightPathStart.DistanceTo (_globalFlightPathDest);
    // _totalFlightDurationSeconds = _globalFlightDistance / FlyingSpeed;
    // _flightTimer.WaitTime = _totalFlightDurationSeconds;
    // _flightTimer.Start();
    _lerp = 0.0f;
  }

  private void Idle()
  {
    Animation = IdleAnimation;
    _idleTimer.Start();
  }
}