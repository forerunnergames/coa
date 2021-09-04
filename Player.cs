using System.Collections.Generic;
using Godot;
using static Tools;
using Input = Tools.Input;

// TODO Bug: Climbing to cliff gap above waterfall still thinks IsFullyIntersectingCliffs is true.
// TODO Stop polling and have event-based input state changes only.
// TODO Check if item being used for cliff arresting is pickaxe. For now it's the default and only item.
// TODO Create _isPreparingToCliffArrest, which is true when attempting cliff arresting, but falling hasn't
// TODO   gotten fast enough yet to activate it (i.e., _velocity.y < CliffArrestingActivationVelocity).
// TODO   In this case show an animation of pulling out pickaxe and swinging it into the cliff, timing it
// TODO   so that the pickaxe sinks into the cliff when CliffArrestingActivationVelocity is reached.
// TODO   The cliff arresting sound effect will need to be adjusted as well to not begin until
// TODO   _isPreparingToCliffArrest is complete (may resolve itself automatically).
// TODO Climbing down? Or leave out?
// Climbing terminology:
// Cliff arresting: Using pickaxe to slow a cliff fall
// Cliffhanging: Attached to a cliff above the ground, not moving
// Traversing: Climbing horizontally
// Free falling: Moving down, without cliff arresting, can also be coming down from a jump.
public class Player : KinematicBody2D
{
  // @formatter:off
  // Godot-configurable options
  [Export] public float HorizontalWalkingSpeed = 20.0f;
  [Export] public float HorizontalRunningSpeed = 40.0f;
  [Export] public float TraverseSpeed = 200.0f;
  [Export] public float VerticalClimbingSpeed = 200.0f;
  [Export] public float HorizontalRunJumpFriction = 0.9f;
  [Export] public float HorizontalRunJumpStoppingFriction = 0.6f;
  [Export] public float TraverseFriction = 0.9f;
  [Export] public float HorizontalClimbStoppingFriction = 0.6f;
  [Export] public float HorizontalFreeFallingSpeed = 10.0f;
  [Export] public float CliffArrestingSpeed = 40.0f;
  [Export] public float CliffArrestingActivationVelocity = 800.0f;
  [Export] public float VelocityEpsilon = 1.0f;
  [Export] public float JumpPower = 800.0f;
  [Export] public float Gravity = 30.0f;
  [Export] public int MaxEnergy = 20;
  [Export] public string IdleLeftAnimation = "player_idle_left";
  [Export] public string IdleBackAnimation = "player_idle_back";
  [Export] public string ClimbingPrepAnimation = "player_idle_back";
  [Export] public string FreeFallingAnimation = "player_falling";
  [Export] public string ClimbingUpAnimation = "player_climbing_up";
  [Export] public string CliffHangingAnimation = "player_cliff_hanging";
  [Export] public string TraversingAnimation = "player_traversing";
  [Export] public string CliffArrestingAnimation = "player_cliff_arresting";
  [Export] public string WalkAnimation = "player_walking";
  [Export] public string RunAnimation = "player_running";
  [Export] public string CliffArrestingSoundFile = "res://cliff_arresting.wav";
  [Export] public bool CliffArrestingSoundLooping = true;
  [Export] public float CliffArrestingSoundLoopBeginSeconds = 0.5f;
  [Export] public float CliffArrestingSoundLoopEndSeconds = 3.8f;
  [Export] public float CliffArrestingSoundVelocityPitchScaleModulation = 4.0f;
  [Export] public float CliffArrestingSoundMinPitchScale = 2.0f;
  [Export] public State InitialState = State.Idle;
  // @formatter:on

  // Field must be publicly accessible from Cliffs.cs
  // ReSharper disable once MemberCanBePrivate.Global
  // ReSharper disable once UnassignedField.Global
  public bool IsInCliffs;

  // Field must be publicly accessible from Cliffs.cs
  // ReSharper disable once MemberCanBePrivate.Global
  // ReSharper disable once UnassignedField.Global
  public bool IsInGround;

  // Field must be publicly accessible from Cliffs.cs
  // ReSharper disable once MemberCanBePrivate.Global
  // ReSharper disable once UnassignedField.Global
  public bool IsTouchingCliffIce;

  // Field must be publicly accessible from Cliffs.cs
  // ReSharper disable once MemberCanBePrivate.Global
  // ReSharper disable once UnassignedField.Global
  public bool IsInFrozenWaterfall;

  public enum State
  {
    Idle,
    ReadingSign,
    Walking,
    Running,
    Jumping,
    ClimbingPrep,
    ClimbingUp,
    CliffHanging,
    Traversing,
    CliffArresting,
    FreeFalling,
  }

  private Vector2 _velocity;
  private RichTextLabel _label = null!;
  private AnimatedSprite _sprite = null!;
  private TextureProgress _energyMeter = null!;
  private AudioStreamPlayer _audio = null!;
  private Timer _energyTimer = null!;
  private Timer _climbingPrepTimer = null!;
  private int _energy;
  private bool _isFlippedHorizontally;
  private bool _wasFlippedHorizontally;
  private readonly List <string> _printLines = new();
  private IStateMachine <State> _stateMachine = null!;
  private uint _fallingStartTimeMs;
  private uint _elapsedFallingTimeMs;
  private uint _lastTotalFallingTimeMs;
  private float _highestVerticalVelocity;
  private bool _isResting;
  private bool _wasRunning;
  private Camera2D _camera;
  private RayCast2D _rayChest;
  private RayCast2D _rayFeet;
  private Sprite _readableSign;
  private TileMap _signsTileMap;
  private bool _isInSign;
  private float _delta;
  private Log _log;

  // @formatter:off

  private static readonly Dictionary <string, int> TileNamesToDropdownFrames = new()
  {
    { "cliffs-sign", 16 },
    { "cliffs-sign-arrow-right", 17 },
    { "cliffs-sign-arrow-left", 17 }
  };

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.Idle, new[] { State.Walking, State.Running, State.Jumping, State.ClimbingPrep, State.ClimbingUp, State.FreeFalling, State.ReadingSign } },
    { State.Walking, new[] { State.Idle, State.Running, State.Jumping, State.FreeFalling, State.ClimbingPrep, State.ReadingSign } },
    { State.Running, new[] { State.Idle, State.Walking, State.Jumping, State.FreeFalling, State.ClimbingPrep, State.ReadingSign } },
    { State.Jumping, new[] { State.Idle, State.FreeFalling, State.Walking } },
    { State.ClimbingPrep, new[] { State.ClimbingUp, State.Idle, State.Walking, State.Jumping } },
    { State.ClimbingUp, new[] { State.FreeFalling } },
    { State.CliffHanging, new[] { State.ClimbingUp, State.Traversing, State.FreeFalling } },
    { State.Traversing, new[] { State.CliffHanging, State.FreeFalling } },
    { State.CliffArresting, new[] { State.CliffHanging, State.FreeFalling, State.Idle } },
    { State.FreeFalling, new[] { State.CliffArresting, State.Idle, State.Walking, State.Running, State.Jumping } },
    { State.ReadingSign, new[] { State.Idle, State.Walking, State.Running } }
  };

  // @formatter:on

  public override void _Ready()
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (Name);
    _camera = GetNode <Camera2D> ("Camera2D");
    _rayChest = GetNode <RayCast2D> ("Chest");
    _rayFeet = GetNode <RayCast2D> ("Feet");
    _audio = GetNode <AudioStreamPlayer> ("PlayerSoundEffectsPlayer");
    _audio.Stream = ResourceLoader.Load <AudioStream> (CliffArrestingSoundFile);
    LoopAudio (_audio.Stream, CliffArrestingSoundLoopBeginSeconds, CliffArrestingSoundLoopEndSeconds);
    _label = GetNode <RichTextLabel> ("../UI/Control/Debugging Text");
    _label.Visible = false;
    _sprite = GetNode <AnimatedSprite> ("AnimatedSprite");
    _energyMeter = GetNode <TextureProgress> ("../UI/Control/Energy Meter");
    _energyMeter.Value = MaxEnergy;
    _energyTimer = GetNode <Timer> ("EnergyTimer");
    _climbingPrepTimer = GetNode <Timer> ("ClimbingReadyTimer");
    _energy = MaxEnergy;
    _sprite.Animation = IdleLeftAnimation;
    _sprite.Play();
    InitializeStateMachine();
  }

  public override void _PhysicsProcess (float delta)
  {
    _delta = delta;
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

  // ReSharper disable once UnusedMember.Global
  public void _OnEnergyTimerTimeout()
  {
    if (!_stateMachine.Is (State.Running) && !_stateMachine.Is (State.ClimbingUp) && _energy == MaxEnergy)
    {
      _energyTimer.Stop();

      return;
    }

    if (_stateMachine.Is (State.Running) || _stateMachine.Is (State.ClimbingUp) && _energy > 0)
    {
      _energy -= 1;
      _energyMeter.Value = _energy;
      _energyTimer.WaitTime = _stateMachine.Is (State.ClimbingUp) ? 1.0f / GetClimbingSpeedBoost() : _energyTimer.WaitTime;

      return;
    }

    if (_stateMachine.Is (State.Running) || _stateMachine.Is (State.ClimbingUp) || _energy == MaxEnergy) return;

    _energy += 1;
    _energyMeter.Value = _energy;
  }

  // Godot input callback
  public override void _UnhandledInput (InputEvent @event)
  {
    if (IsReleased (Input.Text, @event)) _label.Visible = !_label.Visible;
    if (IsReleased (Input.Respawn, @event)) Respawn();
    if (IsDownArrowPressed()) CheckDropDownThrough();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCliffsGroundExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    RestAfterClimbingToNewLevel();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnPlayerAreaColliderEntered (Node body)
  {
    if (body is not TileMap { Name: "Signs" } tileMap) return;

    _isInSign = true;
    _signsTileMap = tileMap;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnPlayerAreaColliderExited (Node body)
  {
    if (body is not TileMap { Name: "Signs" }) return;

    _isInSign = false;
    if (_stateMachine.Is (State.ReadingSign)) _stateMachine.To (State.Idle);
  }

  private void CheckDropDownThrough()
  {
    for (var i = 0; i < GetSlideCount(); ++i)
    {
      var collision = GetSlideCollision (i);

      if (_stateMachine.Is (State.ReadingSign) || !WasDownArrowPressedOnce() || collision.Collider is not Node2D node ||
          !node.IsInGroup ("Dropdownable")) continue;

      switch (collision.Collider)
      {
        case PhysicsBody2D body:
        {
          DropDownThrough (body);

          break;
        }
        case TileMap tileMap:
        {
          DropDownThrough (tileMap, collision.Position);

          break;
        }
      }
    }
  }

  private async void RestAfterClimbingToNewLevel()
  {
    _isResting = true;
    await ToSignal (GetTree().CreateTimer (1.0f, false), "timeout");
    _isResting = false;
  }

  private async void DropDownThrough (PhysicsBody2D body)
  {
    body.SetCollisionMaskBit (0, false);
    SetCollisionMaskBit (1, false);
    await ToSignal (GetTree().CreateTimer (_delta * 2, false), "timeout");
    body.SetCollisionMaskBit (0, true);
    SetCollisionMaskBit (1, true);
  }

  private async void DropDownThrough (TileMap tileMap, Vector2 collisionPosition)
  {
    tileMap.SetCollisionMaskBit (0, false);
    SetCollisionMaskBit (1, false);
    await ToSignal (GetTree().CreateTimer (SecondsToDropDownThroughTile (collisionPosition, tileMap), false), "timeout");
    tileMap.SetCollisionMaskBit (0, true);
    SetCollisionMaskBit (1, true);
  }

  private float SecondsToDropDownThroughTile (Vector2 collisionPosition, TileMap tileMap) =>
    TileNamesToDropdownFrames[GetIntersectingTileName (collisionPosition, tileMap)] * _delta;

  private bool SignExists()
  {
    if (_signsTileMap == null || !IsIntersectingAnyTile (Position, _signsTileMap)) return false;

    var cell = GetIntersectingTileCell (Position, _signsTileMap);
    var name = "(" + cell.x + "," + cell.y + ")";

    return _signsTileMap.HasNode ("../" + name);
  }

  private void ReadSign()
  {
    var cell = GetIntersectingTileCell (Position, _signsTileMap);
    var name = "(" + cell.x + "," + cell.y + ")";

    if (!_signsTileMap.HasNode ("../" + name))
    {
      _log.Warn ($"Attempting to read non-existent sign: {name}.");
      _stateMachine.To (State.Idle);

      return;
    }

    var readableSign = _signsTileMap.GetNode <Sprite> ("../" + name);
    GetNode <AudioStreamPlayer2D> ("../Upper Cliffs/Waterfall/waterfall 1/AudioStreamPlayer2D").Attenuation = 4.0f;
    GetNode <AudioStreamPlayer2D> ("../Upper Cliffs/Waterfall/waterfall 2/AudioStreamPlayer2D").Attenuation = 4.0f;

    for (var i = 1; i < 4; ++i)
    {
      var mist = GetNode <AnimatedSprite> ("../Upper Cliffs/Waterfall/waterfall mist " + i);
      mist.ZIndex = 4;
      mist.Modulate = new Color (Modulate.r, Modulate.g, Modulate.b, Modulate.a * 0.2f);
    }

    _signsTileMap.Visible = false;
    _signsTileMap.GetNode <TileMap> ("../Signs Winter Layer").Visible = false;
    readableSign.Visible = true;
    _readableSign = readableSign;
    _velocity = Vector2.Zero;
    _camera.Zoom = Vector2.One / 10;
    _readableSign.Position = _camera.GetCameraScreenCenter();
    Visible = false;
    _sprite.Animation = IdleBackAnimation;
  }

  private void StopReadingSign()
  {
    if (_readableSign == null) return;

    Visible = true;
    _readableSign.Visible = false;
    _signsTileMap.Visible = true;

    _signsTileMap.GetNode <TileMap> ("../Signs Winter Layer").Visible =
      GetNode <Cliffs> ("../Upper Cliffs").CurrentSeason == Cliffs.Season.Winter;

    _camera.Zoom = Vector2.One;
    _camera.Position = new Vector2 (0, -355);
    _camera.ForceUpdateScroll();
    _camera.Position = new Vector2 (0, 0);
    GetNode <AudioStreamPlayer2D> ("../Upper Cliffs/Waterfall/waterfall 1/AudioStreamPlayer2D").Attenuation = 8.28f;
    GetNode <AudioStreamPlayer2D> ("../Upper Cliffs/Waterfall/waterfall 2/AudioStreamPlayer2D").Attenuation = 8.28f;

    for (var i = 1; i < 4; ++i)
    {
      var mist = GetNode <AnimatedSprite> ("../Upper Cliffs/Waterfall/waterfall mist " + i);
      mist.ZIndex = 1;
      mist.Modulate = new Color (Modulate.r, Modulate.g, Modulate.b);
    }
  }

  private void Respawn()
  {
    _stateMachine.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    GlobalPosition = new Vector2 (952, -4032);
  }

  private bool IsMoving() => IsMovingHorizontally() || IsMovingVertically();
  private bool IsMovingVertically() => Mathf.Abs (_velocity.y) > VelocityEpsilon;
  private bool IsMovingHorizontally() => Mathf.Abs (_velocity.x) > VelocityEpsilon;
  private bool IsMovingUp() => _velocity.y + VelocityEpsilon < 0.0f;
  private bool IsMovingDown() => _velocity.y - VelocityEpsilon > 0.0f;

  private void HorizontalVelocity()
  {
    if (_stateMachine.Is (State.Walking) && IsExclusivelyActiveUnless (Input.Left, _stateMachine.Is (State.Walking)))
    {
      _velocity.x -= HorizontalWalkingSpeed;
      FlipHorizontally (false);
    }

    if (_stateMachine.Is (State.Running) && IsExclusivelyActiveUnless (Input.Left, _stateMachine.Is (State.Running)))
    {
      _velocity.x -= HorizontalRunningSpeed;
      FlipHorizontally (false);
    }

    if (_stateMachine.Is (State.Walking) && IsExclusivelyActiveUnless (Input.Right, _stateMachine.Is (State.Walking)))
    {
      _velocity.x += HorizontalWalkingSpeed;
      FlipHorizontally (true);
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
      _velocity.x -= _wasRunning ? HorizontalRunningSpeed : HorizontalWalkingSpeed;
      FlipHorizontally (false);
    }

    if (_stateMachine.Is (State.Jumping) && IsExclusivelyActiveUnless (Input.Right, _stateMachine.Is (State.Jumping)))
    {
      _velocity.x += _wasRunning ? HorizontalRunningSpeed : HorizontalWalkingSpeed;
      FlipHorizontally (true);
    }

    if (_stateMachine.Is (State.FreeFalling) && IsExclusivelyActiveUnless (Input.Left, _stateMachine.Is (State.FreeFalling)))
    {
      _velocity.x -= _wasRunning ? HorizontalRunningSpeed : HorizontalWalkingSpeed;
      FlipHorizontally (false);
    }

    if (_stateMachine.Is (State.FreeFalling) && IsExclusivelyActiveUnless (Input.Right, _stateMachine.Is (State.FreeFalling)))
    {
      _velocity.x += _wasRunning ? HorizontalRunningSpeed : HorizontalWalkingSpeed;
      FlipHorizontally (true);
    }

    // TODO Check if walking/running or jumping
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
    _velocity.y += Gravity;

    // TODO For jumping, traversing, and cliffhanging, subtract gravity and round to 0.
    // Makes jumps less high when releasing jump button early.
    // (Holding down jump continuously allows gravity to take over.)
    // @formatter:off
    if (_stateMachine.Is (State.Jumping) && WasJumpKeyReleased() && IsMovingUp()) _velocity.y = 0.0f;
    if (_stateMachine.Is (State.ClimbingUp)) _velocity.y = _sprite.Frame is 3 or 8 ? -VerticalClimbingSpeed * GetClimbingSpeedBoost() : 0.0f;
    if (_stateMachine.Is (State.Traversing)) _velocity.y = 0.0f;
    if (_stateMachine.Is (State.CliffHanging)) _velocity.y = 0.0f;
    // @formatter:on

    if (_stateMachine.Is (State.CliffArresting))
    {
      _velocity.y -= CliffArrestingSpeed;
      _velocity.y = SafelyClampMin (_velocity.y, 0.0f);
    }
  }

  private static int GetClimbingSpeedBoost() => IsEnergyKeyPressed() ? 2 : 1;

  private bool IsHittingWall()
  {
    return _rayChest.GetCollider() is StaticBody2D collider1 && collider1.IsInGroup ("Walls") ||
           _rayFeet.GetCollider() is StaticBody2D collider2 && collider2.IsInGroup ("Walls") ||
           _rayChest.GetCollider() is TileMap collider3 && collider3.IsInGroup ("Walls") ||
           _rayFeet.GetCollider() is TileMap collider4 && collider4.IsInGroup ("Walls");
  }

  private void FlipHorizontally (bool flip)
  {
    if (_isFlippedHorizontally == flip) return;

    _isFlippedHorizontally = flip;
    var scale = Scale;
    scale.x = -scale.x;
    Scale = scale;
  }

  private void SoundEffects()
  {
    if (_stateMachine.Is (State.CliffArresting))
    {
      _audio.PitchScale = CliffArrestingSoundMinPitchScale +
                          _velocity.y / CliffArrestingActivationVelocity / CliffArrestingSoundVelocityPitchScaleModulation;
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

  private async void AnimationDelay (string toAnimation, State toState)
  {
    await ToSignal (GetTree().CreateTimer (0.5f, false), "timeout");
    if (_stateMachine.Is (toState)) _sprite.Animation = toAnimation;
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
    "\nState: " + _stateMachine.GetState() +
    "\nSeason: " + GetNode <Cliffs>("../Upper Cliffs").CurrentSeason +
    "\nIdle: " + _stateMachine.Is (State.Idle) +
    "\nWalking: " + _stateMachine.Is (State.Walking) +
    "\nRunning: " + _stateMachine.Is (State.Running) +
    "\nEnergy: " + _energy +
    "\nEnergy timer: " + _energyTimer.TimeLeft +
    "\nJumping: " + _stateMachine.Is (State.Jumping) +
    "\nClimbing prep: " + _stateMachine.Is (State.ClimbingPrep) +
    "\nClimbing prep timer: " + _climbingPrepTimer.TimeLeft +
    "\nClimbing up: " + _stateMachine.Is (State.ClimbingUp) +
    "\nIsTouchingCliffIce: " + IsTouchingCliffIce +
    "\nIsInFrozenWaterfall: " + IsInFrozenWaterfall +
    "\nCliff arresting: " + _stateMachine.Is (State.CliffArresting) +
    "\nCliff hanging: " + _stateMachine.Is (State.CliffHanging) +
    "\nClimbing Traversing: " + _stateMachine.Is (State.Traversing) +
    "\nFree falling: " + _stateMachine.Is (State.FreeFalling) +
    "\nIsOnFloor(): " + IsOnFloor() +
    "\nIsHittingWall(): " + IsHittingWall() +
    "\nIsInCliffs: " + IsInCliffs +
    "\nIsInGround: " + IsInGround +
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
    "\nIsFlippedHorizontally: " + _isFlippedHorizontally +
    "\nPosition: " + Position +
    "\nScale: " + Scale +
    "\nVelocity: " + _velocity +
    "\nVertical velocity (mph): " + _velocity.y * 0.028334573333333 +
    "\nHighest vertical velocity: " + _highestVerticalVelocity +
    "\nHighest vertical velocity (mph): " + _highestVerticalVelocity * 0.028334573333333 +
    "\nFalling time (sec): " + GetFallingTimeSeconds();
  // @formatter:on

  private void InitializeStateMachine()
  {
    _stateMachine = new StateMachine <State> (TransitionTable, InitialState);
    _stateMachine.OnTransitionTo (State.CliffHanging, () => _sprite.Animation = CliffHangingAnimation);
    _stateMachine.OnTransitionTo (State.FreeFalling, () => _sprite.Animation = FreeFallingAnimation);
    _stateMachine.OnTransitionFrom (State.ClimbingPrep, () => _climbingPrepTimer.Stop());
    _stateMachine.OnTransition (State.ClimbingPrep, State.Idle, () => FlipHorizontally (_wasFlippedHorizontally));
    _stateMachine.OnTransitionTo (State.Traversing, () => _sprite.Animation = TraversingAnimation);
    _stateMachine.OnTransition (State.Walking, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransition (State.Running, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransition (State.CliffArresting, State.Idle, () => _sprite.Animation = IdleLeftAnimation);

    _stateMachine.OnTransitionFrom (State.Idle, () =>
    {
      if (IsInGroup ("Perchable")) RemoveFromGroup ("Perchable");
    });

    _stateMachine.OnTransitionTo (State.Idle, () =>
    {
      if (!IsInGroup ("Perchable")) AddToGroup ("Perchable");
      _wasRunning = false;
    });

    _stateMachine.OnTransitionTo (State.Walking, () =>
    {
      _sprite.Animation = WalkAnimation;
      _wasRunning = false;
    });

    _stateMachine.OnTransitionTo (State.Jumping, () =>
    {
      _sprite.Animation = IdleLeftAnimation;
      _velocity.y -= JumpPower;
    });

    _stateMachine.OnTransitionFrom (State.CliffArresting, () =>
    {
      if (!_audio.Playing) return;

      _audio.Stop();
      PrintLine ("Sound effects: Stopped cliff arresting sound.");
    });

    _stateMachine.OnTransitionTo (State.CliffArresting, () =>
    {
      _sprite.Animation = CliffArrestingAnimation;

      if (_audio.Playing) return;

      _audio.Play();
      PrintLine ("Sound effects: Playing cliff arresting sound.");
    });

    _stateMachine.OnTransitionTo (State.ClimbingPrep, () =>
    {
      _sprite.Animation = ClimbingPrepAnimation;
      _wasFlippedHorizontally = _isFlippedHorizontally;
      FlipHorizontally (false);
      _climbingPrepTimer.Start();
    });

    _stateMachine.OnTransitionFrom (State.Running, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (0.5f);
    });

    _stateMachine.OnTransitionTo (State.Running, () =>
    {
      _sprite.Animation = RunAnimation;
      _energyTimer.Start (0.15f);
      _wasRunning = true;
    });

    _stateMachine.OnTransitionFrom (State.ClimbingUp, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (0.5f);
    });

    _stateMachine.OnTransitionTo (State.ClimbingUp, () =>
    {
      _sprite.Animation = ClimbingUpAnimation;
      _energyTimer.Start (1.0f / GetClimbingSpeedBoost());
      FlipHorizontally (false);
    });

    _stateMachine.OnTransitionTo (State.ReadingSign, ReadSign);
    _stateMachine.OnTransitionFrom (State.ReadingSign, StopReadingSign);
    _stateMachine.OnTransition (State.ReadingSign, State.Idle, () => AnimationDelay (IdleLeftAnimation, State.Idle));

    // TODO Move conditions into state machine conditions, leaving only input for triggers.
    // @formatter:off
    _stateMachine.AddTrigger (State.Idle, State.Walking, () => IsOneActiveOf (Input.Horizontal) && !IsEnergyKeyPressed());
    _stateMachine.AddTrigger (State.Idle, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsEnergyKeyPressed() && _energy > 0 && !IsHittingWall());
    _stateMachine.AddTrigger (State.Idle, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Idle, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs && !_isInSign && !_isResting);
    _stateMachine.AddTrigger (State.Idle, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.Idle, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && SignExists());
    _stateMachine.AddTrigger (State.Walking, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.Walking, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsEnergyKeyPressed() && _energy > 0 && IsMovingHorizontally());
    _stateMachine.AddTrigger (State.Walking, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Walking, State.FreeFalling, () => IsMovingDown() && !IsOnFloor() && !IsHittingWall());
    _stateMachine.AddTrigger (State.Walking, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs && !_isInSign);
    _stateMachine.AddTrigger (State.Walking, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && SignExists());
    _stateMachine.AddTrigger (State.Running, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.Running, State.Walking, () => IsOneActiveOf (Input.Horizontal) && (!IsEnergyKeyPressed() || _energy == 0));
    _stateMachine.AddTrigger (State.Running, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Running, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.Running, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs && !_isInSign);
    _stateMachine.AddTrigger (State.Running, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && SignExists());
    _stateMachine.AddTrigger (State.Jumping, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.ClimbingPrep, State.Idle, WasUpArrowReleased);
    _stateMachine.AddTrigger (State.ClimbingPrep, State.ClimbingUp, () => IsUpArrowPressed() && _climbingPrepTimer.TimeLeft == 0 && !IsTouchingCliffIce && !IsInFrozenWaterfall);
    _stateMachine.AddTrigger (State.ClimbingUp, State.FreeFalling, () => WasUpArrowReleased() || !IsInCliffs && !IsInGround || IsTouchingCliffIce || IsInFrozenWaterfall || _isResting || _energy == 0);
    _stateMachine.AddTrigger (State.FreeFalling, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && IsOnFloor() && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.FreeFalling, State.Walking, () => IsOneActiveOf (Input.Horizontal) && !IsEnergyKeyPressed() && IsOnFloor());
    _stateMachine.AddTrigger (State.FreeFalling, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsEnergyKeyPressed() && _energy > 0 && IsOnFloor());
    _stateMachine.AddTrigger (State.FreeFalling, State.CliffArresting, () => IsItemKeyPressed() && IsInCliffs && _velocity.y >= CliffArrestingActivationVelocity);
    _stateMachine.AddTrigger (State.CliffHanging, State.ClimbingUp, IsUpArrowPressed);
    _stateMachine.AddTrigger (State.CliffHanging, State.FreeFalling, WasDownArrowPressedOnce);
    _stateMachine.AddTrigger (State.CliffHanging, State.Traversing, () => IsOneActiveOf (Input.Horizontal));
    _stateMachine.AddTrigger (State.Traversing, State.FreeFalling, () => WasDownArrowPressedOnce() || !IsInCliffs || IsTouchingCliffIce || IsInFrozenWaterfall);
    _stateMachine.AddTrigger (State.Traversing, State.CliffHanging, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.CliffArresting, State.FreeFalling, WasItemKeyReleased);
    _stateMachine.AddTrigger (State.CliffArresting, State.CliffHanging, () => !IsOnFloor() && !IsMoving() && IsInCliffs);
    _stateMachine.AddTrigger (State.CliffArresting, State.Idle, IsOnFloor);
    _stateMachine.AddTrigger (State.ReadingSign, State.Idle, ()=> IsDownArrowPressed() && !IsUpArrowPressed() && _isInSign);
    _stateMachine.AddTrigger (State.ReadingSign, State.Walking, ()=> IsOneActiveOf (Input.Horizontal) && !IsAnyActiveOf (Input.Vertical) && !IsEnergyKeyPressed() && _isInSign);
    _stateMachine.AddTrigger (State.ReadingSign, State.Running, ()=> IsOneActiveOf (Input.Horizontal) && !IsAnyActiveOf (Input.Vertical) && IsEnergyKeyPressed() && _energy > 0 && _isInSign);
    // @formatter:on
  }
}