using System;
using System.Collections.Generic;
using Godot;
using static Tools;
using Input = Tools.Input;

// TODO Bug: Running / preparing to climb with up arrow and single horizontal arrow alternates states in a loop.
// TODO Bug: Running / climbing horizontally against a wall alternates in a loop between idle and running.
// TODO Bug: Climbing to cliff gap above waterfall still thinks IsFullyIntersectingCliffs is true.
// TODO Stop polling and have event-based input state changes only.
// TODO Check if item being used for scraping is pickaxe. For now it's the default and only item.
// TODO Create _isPreparingToScrapeCliff, which is true when attempting scraping, but falling hasn't
// TODO   gotten fast enough yet to activate it (i.e., _velocity.y < CliffScrapingActivationVelocity).
// TODO   In this case show an animation of pulling out pickaxe and swinging it into the cliff, timing it
// TODO   so that the pickaxe sinks into the cliff when CliffScrapingActivationVelocity is reached.
// TODO   The cliff scraping sound effect will need to be adjusted as well to not begin until
// TODO   _isPreparingToScrapeCliff is complete (may resolve itself automatically).
// TODO Climbing down? Or leave out?
// Climbing terminology:
// Scraping: Using pickaxe to slow a cliff fall
// Cliffhanging: Attached to a cliff above the ground, not moving
// Traversing: Climbing horizontally
// Free falling: Moving down, without scraping, can also be coming down from a jump.
public class Player : KinematicBody2D
{
  // @formatter:off
  // Godot-configurable options
  [Export] public float HorizontalRunningSpeed = 50.0f;
  [Export] public float TraverseSpeed = 200.0f;
  [Export] public float VerticalClimbingSpeed = 200.0f;
  [Export] public float HorizontalRunJumpFriction = 0.9f;
  [Export] public float HorizontalRunJumpStoppingFriction = 0.6f;
  [Export] public float TraverseFriction = 0.9f;
  [Export] public float HorizontalClimbStoppingFriction = 0.6f;
  [Export] public float HorizontalFreeFallingSpeed = 10.0f;
  [Export] public float CliffScrapingSpeed = 40.0f;
  [Export] public float CliffScrapingActivationVelocity = 800.0f;
  [Export] public float VelocityEpsilon = 1.0f;
  [Export] public float JumpPower = 800.0f;
  // ReSharper disable once InconsistentNaming
  [Export] public float Gravity_ = 30.0f;
  [Export] public string IdleLeftAnimation = "player_idle_left";
  [Export] public string ClimbingPrepAnimation = "player_idle_back";
  [Export] public string FreeFallingAnimation = "player_falling";
  [Export] public string ClimbingUpAnimation = "player_climbing_up";
  [Export] public string CliffHangingAnimation = "player_cliff_hanging";
  [Export] public string TraversingAnimation = "player_traversing";
  [Export] public string ScrapingAnimation = "player_scraping";
  [Export] public string RunAnimation = "player_running";
  [Export] public string CliffScrapingSoundFile = "res://cliff_scrape.wav";
  [Export] public bool CliffScrapingSoundLooping = true;
  // ReSharper disable once RedundantDefaultMemberInitializer
  [Export] public float CliffScrapingSoundLoopBeginSeconds = 0.0f;
  [Export] public float CliffScrapingSoundLoopEndSeconds = 4.5f;
  [Export] public float CliffScrapingSoundVelocityPitchScaleModulation = 4.0f;
  [Export] public float CliffScrapingSoundMinPitchScale = 2.0f;
  [Export] public State InitialState = State.Idle;
  [Export] public Log.Level LogLevel = Log.Level.Debug;
  // @formatter:on

  // Field must be publicly accessible from Cliffs.cs
  // ReSharper disable once MemberCanBePrivate.Global
  // ReSharper disable once UnassignedField.Global
  public bool IsInCliffs;

  public enum State
  {
    Idle,
    Running,
    Jumping,
    ClimbingPrep,
    ClimbingUp,
    CliffHanging,
    Traversing,
    Scraping,
    FreeFalling
  }

  private Vector2 _velocity;
  private RichTextLabel _label = null!;
  private AnimatedSprite _sprite = null!;
  private AudioStreamPlayer _audio = null!;
  private Timer _climbingPrepTimer = null!;
  private bool _isFlippedHorizontally;
  private bool _wasFlippedHorizontally;
  private readonly List <string> _printLines = new();
  private IStateMachine <State> _stateMachine = null!;
  private uint _fallingStartTimeMs;
  private uint _elapsedFallingTimeMs;
  private uint _lastTotalFallingTimeMs;
  private float _highestVerticalVelocity;
  private readonly List <CollisionShape2D> _disabledFloors = new();

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.Idle, new[] { State.Running, State.Jumping, State.ClimbingPrep, State.ClimbingUp, State.FreeFalling } },
    { State.Running, new[] { State.Idle, State.Jumping, State.FreeFalling, State.ClimbingPrep } },
    { State.Jumping, new[] { State.Idle, State.FreeFalling, State.Running } },
    { State.ClimbingPrep, new[] { State.ClimbingUp, State.Idle, State.Running, State.Jumping } },
    { State.ClimbingUp, new[] { State.FreeFalling } },
    { State.CliffHanging, new[] { State.ClimbingUp, State.Traversing, State.FreeFalling } },
    { State.Traversing, new[] { State.CliffHanging, State.FreeFalling } },
    { State.Scraping, new[] { State.CliffHanging, State.FreeFalling, State.Idle } },
    { State.FreeFalling, new[] { State.Scraping, State.Idle, State.Running, State.Jumping } }
  };

  public override void _Ready()
  {
    _audio = GetNode <AudioStreamPlayer> ("AudioStreamPlayer");
    _audio.Stream = ResourceLoader.Load <AudioStream> (CliffScrapingSoundFile);
    LoopAudio (_audio.Stream, CliffScrapingSoundLoopBeginSeconds, CliffScrapingSoundLoopEndSeconds);
    _label = GetNode <RichTextLabel> ("DebuggingText");
    _sprite = GetNode <AnimatedSprite> ("AnimatedSprite");
    _climbingPrepTimer = GetNode <Timer> ("ClimbingReadyTimer");
    _sprite.Animation = IdleLeftAnimation;
    _sprite.Play();
    InitializeStateMachine();
  }

  public override void _PhysicsProcess (float delta)
  {
    if (ShouldDisableFloor()) DisableFloor(); // TODO Create a separate state machine for the dropdownable floors.
    _stateMachine.Update();
    HorizontalVelocity();
    VerticalVelocity();
    SoundEffects();
    if (Mathf.Abs (_velocity.x) < VelocityEpsilon) _velocity.x = 0.0f;
    if (Mathf.Abs (_velocity.y) < VelocityEpsilon) _velocity.y = 0.0f;
    _velocity = MoveAndSlide (_velocity, Vector2.Up);
    CalculateFallingStats();
    PrintLine (DumpState());
    Print();
  }

  // Godot input callback
  public override void _UnhandledInput (InputEvent @event)
  {
    if (IsReleased (Input.Text, @event)) _label.Visible = !_label.Visible;
    if (IsReleased (Input.Respawn, @event)) Respawn();
  }

  private void Respawn()
  {
    _stateMachine.Reset();
    GlobalPosition = new Vector2 (952, -4032);
    EnableFloors();
  }

  private void EnableFloors()
  {
    _disabledFloors.ForEach (x => x.Disabled = false);
    _disabledFloors.Clear();
  }

  private bool ShouldDisableFloor() => IsOnFloor() && WasDownArrowPressedOnce();
  private bool IsMoving() => IsMovingHorizontally() || IsMovingVertically();
  private bool IsMovingVertically() => Mathf.Abs (_velocity.y) > VelocityEpsilon;
  private bool IsMovingHorizontally() => Mathf.Abs (_velocity.x) > VelocityEpsilon;
  private bool IsMovingUp() => _velocity.y + VelocityEpsilon < 0.0f;
  private bool IsMovingDown() => _velocity.y - VelocityEpsilon > 0.0f;
  private bool IsMovingLeft() => _velocity.x + VelocityEpsilon < 0.0f;
  private bool IsMovingRight() => _velocity.x + VelocityEpsilon < 0.0f;

  // Only for ground/floor that has climbable cliffs below it
  private void DisableFloor()
  {
    for (var i = 0; i < GetSlideCount(); ++i)
    {
      var collision = GetSlideCollision (i);
      var collider = collision.Collider as StaticBody2D;

      if (collider == null) continue;
      if (!collider.IsInGroup ("Cliffs")) continue;
      if (!collider.IsInGroup ("Ground")) continue;
      if (!collider.IsInGroup ("Dropdownable")) continue;

      var colliderShape = collision.ColliderShape as CollisionShape2D;

      if (colliderShape == null) continue;

      colliderShape.Disabled = true;
      _disabledFloors.Add (colliderShape);
    }
  }

  private void HorizontalVelocity()
  {
    if (_stateMachine.Is (State.Running) && IsExclusivelyActiveUnless (Input.Left, _stateMachine.Is (State.Running)))
    {
      _velocity.x -= HorizontalRunningSpeed;
      FlipHorizontally (false);
    }

    if (_stateMachine.Is (State.Running) && IsExclusivelyActiveUnless (Input.Right, _stateMachine.Is (State.Running)))
    {
      _velocity.x += HorizontalRunningSpeed;
      FlipHorizontally (true);
    }

    if (_stateMachine.Is (State.Traversing))
    {
      var left = IsLeftArrowPressed();
      var right = IsRightArrowPressed();
      var leftOnly = left && !right;
      var rightOnly = right && !left;
      _velocity.x += TraverseSpeed * (leftOnly ? -1 : rightOnly ? 1 : 0.0f);
      _velocity.x = SafelyClamp (_velocity.x, -TraverseSpeed, TraverseSpeed);
    }

    if (_stateMachine.Is (State.Jumping) && IsExclusivelyActiveUnless (Input.Left, _stateMachine.Is (State.Jumping)))
    {
      _velocity.x -= HorizontalRunningSpeed;
      FlipHorizontally (false);
    }

    if (_stateMachine.Is (State.Jumping) && IsExclusivelyActiveUnless (Input.Right, _stateMachine.Is (State.Jumping)))
    {
      _velocity.x += HorizontalRunningSpeed;
      FlipHorizontally (true);
    }

    if (_stateMachine.Is (State.FreeFalling) && IsExclusivelyActiveUnless (Input.Left, _stateMachine.Is (State.FreeFalling)))
    {
      _velocity.x -= HorizontalRunningSpeed;
      FlipHorizontally (false);
    }

    if (_stateMachine.Is (State.FreeFalling) && IsExclusivelyActiveUnless (Input.Right, _stateMachine.Is (State.FreeFalling)))
    {
      _velocity.x += HorizontalRunningSpeed;
      FlipHorizontally (true);
    }

    // TODO Check if running or jumping
    // TODO Get rid of else if
    // Friction
    // @formatter:off
    if (!IsAnyHorizontalArrowPressed()) _velocity.x *= HorizontalRunJumpStoppingFriction;
    else if (_stateMachine.Is (State.Traversing)) _velocity.x *= TraverseFriction;
    else _velocity.x *= HorizontalRunJumpFriction;
    // @formatter:on
  }

  private void VerticalVelocity()
  {
    _velocity.y += Gravity_;

    // TODO Remove else if's, order shouldn't matter.
    // TODO Make relative by subtracting gravity from _velocity.y and round to 0 within a couple decimal places (or epsilon).
    if (_stateMachine.Is (State.Jumping))
    {
      // Makes jumps less high when releasing jump button early.
      // (Holding down jump continuously allows gravity to take over.)
      if (WasJumpKeyReleased() && IsMovingUp()) _velocity.y = 0.0f; // TODO Subtract gravity and round to 0.
    }
    else if (_stateMachine.Is (State.ClimbingUp))
    {
      _velocity.y -= Gravity_ + VerticalClimbingSpeed * 0.01f;
      _velocity.y = SafelyClampMin (_velocity.y, -VerticalClimbingSpeed);
    }
    else if (_stateMachine.Is (State.Traversing))
    {
      _velocity.y = 0.0f; // TODO Subtract gravity and round to 0.
    }
    else if (_stateMachine.Is (State.CliffHanging))
    {
      _velocity.y = 0.0f; // TODO Subtract gravity and round to 0.
    }
    else if (_stateMachine.Is (State.Scraping))
    {
      _velocity.y -= CliffScrapingSpeed;
      _velocity.y = SafelyClampMin (_velocity.y, 0.0f);
    }
  }

  // Flips everything except RichTextLabel
  private void FlipHorizontally (bool flip)
  {
    if (_isFlippedHorizontally == flip) return;

    _isFlippedHorizontally = flip;

    // Flip entire parent node and all children.
    var scale = Scale;
    scale.x = -scale.x;
    Scale = scale;

    // Bugfix: Undo inadvertent label flip (reverses text).
    var labelRectScale = _label.RectScale;
    labelRectScale.x = -labelRectScale.x;
    _label.RectScale = labelRectScale;

    // Bugfix: Move label over instead of reversing text.
    var labelRectPosition = _label.RectPosition;
    labelRectPosition.x += _isFlippedHorizontally ? 160 : -160;
    _label.RectPosition = labelRectPosition;
  }

  private void SoundEffects()
  {
    if (_stateMachine.Is (State.Scraping))
    {
      _audio.PitchScale = CliffScrapingSoundMinPitchScale +
                          _velocity.y / CliffScrapingActivationVelocity / CliffScrapingSoundVelocityPitchScaleModulation;
    }
  }

  private void CalculateFallingStats()
  {
    if (_velocity.y > _highestVerticalVelocity) _highestVerticalVelocity = _velocity.y;

    if (IsMovingDown() && _fallingStartTimeMs == 0)
    {
      _fallingStartTimeMs = OS.GetTicksMsec();
    }
    else if (IsMovingDown())
    {
      _elapsedFallingTimeMs = OS.GetTicksMsec() - _fallingStartTimeMs;
    }
    else if (!IsMovingDown() && _elapsedFallingTimeMs > 0)
    {
      _lastTotalFallingTimeMs = OS.GetTicksMsec() - _fallingStartTimeMs;
      _fallingStartTimeMs = 0;
      _elapsedFallingTimeMs = 0;
    }
  }

  private void PrintLine (string line) => _printLines.Add (line);

  private void Print()
  {
    _label.Text = "";
    _label.BbcodeText = "";
    foreach (var line in _printLines) _label.AddText (line + "\n");
    _printLines.Clear();
  }

  private float GetFallingTimeSeconds() =>
    _elapsedFallingTimeMs > 0 ? _elapsedFallingTimeMs / 1000.0f : _lastTotalFallingTimeMs / 1000.0f;

  // @formatter:off
  private string DumpState() =>
    "\nIdle: " + _stateMachine.Is (State.Idle) +
    "\nRunning: " + _stateMachine.Is (State.Running) +
    "\nJumping: " + _stateMachine.Is (State.Jumping) +
    "\nClimbing prep: " + _stateMachine.Is (State.ClimbingPrep) +
    "\nClimbing prep timer: " + _climbingPrepTimer.TimeLeft +
    "\nClimbing up: " + _stateMachine.Is (State.ClimbingUp) +
    "\nScraping cliff: " + _stateMachine.Is (State.Scraping) +
    "\nCliff hanging: " + _stateMachine.Is (State.CliffHanging) +
    "\nClimbing Horizontally: " + _stateMachine.Is (State.Traversing) +
    "\nFree falling: " + _stateMachine.Is (State.FreeFalling) +
    "\nIsOnFloor(): " + IsOnFloor() +
    "\nIsFullyIntersectingCliffs: " + IsInCliffs +
    "\nIsInMotion(): " + IsMoving() +
    "\nIsMovingDown(): " + IsMovingDown() +
    "\nIsMovingUp(): " + IsMovingUp() +
    "\nIsInMotionHorizontally(): " + IsMovingHorizontally() +
    "\nIsInMotionVertically(): " + IsMovingVertically() +
    "\nIsRightArrowPressed(): " + IsRightArrowPressed() +
    "\nIsLeftArrowPressed(): " + IsLeftArrowPressed() +
    "\nIsUpArrowPressed(): " + IsUpArrowPressed() +
    "\nIsDownArrowPressed(): " + IsDownArrowPressed() +
    "\nIsAnyHorizontalArrowPressed(): " + IsAnyHorizontalArrowPressed() +
    "\nIsAnyVerticalArrowPressed(): " + IsAnyVerticalArrowPressed() +
    "\n_velocity: " + _velocity +
    "\nVertical velocity (mph): " + _velocity.y * 0.028334573333333 +
    "\nHighest vertical velocity: " + _highestVerticalVelocity +
    "\nHighest vertical velocity (mph): " + _highestVerticalVelocity * 0.028334573333333 +
    "\nFalling time (sec): " + GetFallingTimeSeconds();
  // @formatter:on

  private void InitializeStateMachine()
  {
    _stateMachine = new StateMachine <State> (TransitionTable, InitialState);
    _stateMachine.OnTransitionTo (State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransitionTo (State.Running, () => _sprite.Animation = RunAnimation);
    _stateMachine.OnTransitionTo (State.CliffHanging, () => _sprite.Animation = CliffHangingAnimation);
    _stateMachine.OnTransitionTo (State.FreeFalling, () => _sprite.Animation = FreeFallingAnimation);
    _stateMachine.OnTransitionFrom (State.ClimbingPrep, () => _climbingPrepTimer.Stop());
    _stateMachine.OnTransition (State.ClimbingPrep, State.Idle, () => FlipHorizontally (_wasFlippedHorizontally));
    _stateMachine.OnTransitionTo (State.Traversing, () => _sprite.Animation = TraversingAnimation);

    _stateMachine.OnTransitionTo (State.Jumping, () =>
    {
      _sprite.Animation = IdleLeftAnimation;
      _velocity.y -= JumpPower;
    });

    _stateMachine.OnTransitionTo (State.ClimbingUp, () =>
    {
      _sprite.Animation = ClimbingUpAnimation;
      FlipHorizontally (false);
    });

    _stateMachine.OnTransitionFrom (State.Scraping, () =>
    {
      if (!_audio.Playing) return;

      _audio.Stop();
      PrintLine ("Sound effects: Stopped cliff scraping sound.");
    });

    _stateMachine.OnTransitionTo (State.Scraping, () =>
    {
      _sprite.Animation = ScrapingAnimation;

      if (_audio.Playing) return;

      _audio.Play();
      PrintLine ("Sound effects: Playing cliff scraping sound.");
    });

    _stateMachine.OnTransitionTo (State.ClimbingPrep, () =>
    {
      _sprite.Animation = ClimbingPrepAnimation;
      _wasFlippedHorizontally = _isFlippedHorizontally;
      FlipHorizontally (false);
      _climbingPrepTimer.Start();
    });

    // TODO Move conditions into state machine conditions, leaving only input for triggers.
    // @formatter:off
    _stateMachine.AddTrigger (State.Idle, State.Running, () => IsOneActiveOf (Input.Horizontal));
    _stateMachine.AddTrigger (State.Idle, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Idle, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs);
    _stateMachine.AddTrigger (State.Idle, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.Running, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && ! IsMovingHorizontally());
    _stateMachine.AddTrigger (State.Running, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Running, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.Running, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs);
    _stateMachine.AddTrigger (State.Jumping, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.ClimbingPrep, State.Idle, WasUpArrowReleased);
    _stateMachine.AddTrigger (State.ClimbingPrep, State.ClimbingUp, () => IsUpArrowPressed() && _climbingPrepTimer.TimeLeft == 0);
    _stateMachine.AddTrigger (State.ClimbingUp, State.FreeFalling, () => WasUpArrowReleased() || !IsInCliffs);
    _stateMachine.AddTrigger (State.FreeFalling, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && IsOnFloor() && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.FreeFalling, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsOnFloor());
    _stateMachine.AddTrigger (State.FreeFalling, State.Scraping, () => IsItemKeyPressed() && IsInCliffs && _velocity.y >= CliffScrapingActivationVelocity);
    _stateMachine.AddTrigger (State.CliffHanging, State.ClimbingUp, IsUpArrowPressed);
    _stateMachine.AddTrigger (State.CliffHanging, State.FreeFalling, WasDownArrowPressedOnce);
    _stateMachine.AddTrigger (State.CliffHanging, State.Traversing, () => IsOneActiveOf (Input.Horizontal));
    _stateMachine.AddTrigger (State.Traversing, State.FreeFalling, () => WasDownArrowPressedOnce() || !IsInCliffs);
    _stateMachine.AddTrigger (State.Traversing, State.CliffHanging, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.Scraping, State.FreeFalling, WasItemKeyReleased);
    _stateMachine.AddTrigger (State.Scraping, State.CliffHanging, () => !IsOnFloor() && !IsMoving() && IsInCliffs);
    _stateMachine.AddTrigger (State.Scraping, State.Idle, IsOnFloor);
    // @formatter:on
  }
}