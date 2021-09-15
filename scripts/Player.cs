using System.Collections.Generic;
using Godot;
using static Tools;
using Input = Tools.Input;

// TODO Stop polling and have event-based input state changes only.
// TODO Check if item being used for cliff arresting is pickaxe. For now it's the default and only item.
// TODO Create _isPreparingToCliffArrest, which is true when attempting cliff arresting, but falling hasn't
// TODO   gotten fast enough yet to activate it (i.e., _velocity.y < CliffArrestingActivationVelocity).
// TODO   In this case show an animation of pulling out pickaxe and swinging it into the cliff, timing it
// TODO   so that the pickaxe sinks into the cliff when CliffArrestingActivationVelocity is reached.
// TODO   The cliff arresting sound effect will need to be adjusted as well to not begin until
// TODO   _isPreparingToCliffArrest is complete (may resolve itself automatically).
// Climbing terminology:
// Cliff arresting: Using pickaxe to slow a cliff fall
// Cliffhanging: Attached to a cliff above the ground, not moving
// Traversing: Climbing horizontally
// Free falling: Moving down, without cliff arresting, can also be coming down from a jump.
public class Player : KinematicBody2D
{
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
  [Export] public string ClimbingDownAnimation = "player_climbing_down";
  [Export] public string CliffHangingAnimation = "player_cliff_hanging";
  [Export] public string TraversingAnimation = "player_traversing";
  [Export] public string CliffArrestingAnimation = "player_cliff_arresting";
  [Export] public string WalkAnimation = "player_walking";
  [Export] public string RunAnimation = "player_running";
  [Export] public string CliffArrestingSoundFile = "res://assets/sounds/cliff_arresting.wav";
  [Export] public bool CliffArrestingSoundLooping = true;
  [Export] public float CliffArrestingSoundLoopBeginSeconds = 0.5f;
  [Export] public float CliffArrestingSoundLoopEndSeconds = 3.8f;
  [Export] public float CliffArrestingSoundVelocityPitchScaleModulation = 4.0f;
  [Export] public float CliffArrestingSoundMinPitchScale = 2.0f;
  [Export] public float ClimbingUpToNewLevelRestTimeSeconds = 1.0f;
  [Export] public int EnergyMeterUnits = 20;
  [Export] public int EnergyMeterReplenishTimeSeconds = 10;
  [Export] public int EnergyMeterDepletionTimeSeconds = 3;
  [Export] public State InitialState = State.Idle;

  // Field must be publicly accessible from Cliffs.cs
  public bool IsInCliffs;

  // Field must be publicly accessible from Cliffs.cs
  public bool IsTouchingCliffIce;

  // Field must be publicly accessible from Cliffs.cs
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
    ClimbingDown,
    CliffHanging,
    Traversing,
    CliffArresting,
    FreeFalling,
  }

  private Vector2 _velocity;
  private RichTextLabel _label = null!;
  private AnimatedSprite _sprite = null!;
  private Area2D _area = null!;
  private TextureProgress _energyMeter = null!;
  private AudioStreamPlayer _audio = null!;
  private Timer _energyTimer = null!;
  private Timer _climbingPrepTimer = null!;
  private int _energy;
  private float _energyMeterReplenishRatePerUnit;
  private float _energyMeterDepletionRatePerUnit;
  private bool _isFlippedHorizontally;
  private bool _wasFlippedHorizontally;
  private readonly List <string> _printLines = new();
  private IStateMachine <State> _stateMachine = null!;
  private uint _fallingStartTimeMs;
  private uint _elapsedFallingTimeMs;
  private uint _lastTotalFallingTimeMs;
  private float _highestVerticalVelocity;
  private bool _isInGround;
  private bool _wasRunning;
  private bool _isInSign;
  private bool _wasInCliffEdge;
  private bool _justRespawned;
  private Camera2D _camera;
  private RayCast2D _rayChest;
  private RayCast2D _rayFeet;
  private Sprite _readableSign;
  private TileMap _signsTileMap;
  private Cliffs _cliffs;
  private Area2D _waterfall;
  private volatile string _currentAnimation;
  private volatile bool _isDroppingDown;
  private volatile bool _isResting;
  private Log _log;

  // @formatter:off

  private static readonly Dictionary <string, int> TileNamesToDropdownFrames = new()
  {
    { "sign", 16 },
    { "sign-arrow-right", 17 },
    { "sign-arrow-left", 17 }
  };

  private static readonly Dictionary <State, State[]> TransitionTable = new()
  {
    { State.Idle, new[] { State.Walking, State.Running, State.Jumping, State.ClimbingPrep, State.ClimbingUp, State.ClimbingDown, State.FreeFalling, State.ReadingSign }},
    { State.Walking, new[] { State.Idle, State.Running, State.Jumping, State.FreeFalling, State.ClimbingPrep, State.ReadingSign }},
    { State.Running, new[] { State.Idle, State.Walking, State.Jumping, State.FreeFalling, State.ClimbingPrep, State.ReadingSign }},
    { State.Jumping, new[] { State.Idle, State.FreeFalling, State.Walking }},
    { State.ClimbingPrep, new[] { State.ClimbingUp, State.Idle, State.Walking, State.Jumping }},
    { State.ClimbingUp, new[] { State.ClimbingDown, State.CliffHanging, State.FreeFalling, State.Idle }},
    { State.ClimbingDown, new[] { State.ClimbingUp, State.CliffHanging, State.FreeFalling, State.Idle }},
    { State.CliffHanging, new[] { State.ClimbingUp, State.ClimbingDown, State.Traversing, State.FreeFalling }},
    { State.Traversing, new[] { State.CliffHanging, State.FreeFalling }},
    { State.CliffArresting, new[] { State.CliffHanging, State.FreeFalling, State.Idle }},
    { State.FreeFalling, new[] { State.CliffArresting, State.CliffHanging, State.Idle, State.Walking, State.Running, State.Jumping }},
    { State.ReadingSign, new[] { State.Idle, State.Walking, State.Running }}
  };

  // @formatter:on

  public override void _Ready()
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (Name);
    _camera = GetNode <Camera2D> ("Camera2D");
    _rayChest = GetNode <RayCast2D> ("Chest");
    _rayFeet = GetNode <RayCast2D> ("Feet");
    _cliffs = GetNode <Cliffs> ("../Cliffs");
    _waterfall = _cliffs.GetNode <Area2D> ("Waterfall");
    _audio = GetNode <AudioStreamPlayer> ("PlayerSoundEffectsPlayer");
    _audio.Stream = ResourceLoader.Load <AudioStream> (CliffArrestingSoundFile);
    _label = GetNode <RichTextLabel> ("../UI/Control/Debugging Text");
    _label.Visible = false;
    _sprite = GetNode <AnimatedSprite> ("AnimatedSprite");
    _area = _sprite.GetNode <Area2D> ("Area2D");
    _energyMeter = GetNode <TextureProgress> ("../UI/Control/Energy Meter");
    _energyMeter.Value = MaxEnergy;
    _energy = MaxEnergy;
    _energyTimer = GetNode <Timer> ("EnergyTimer");
    _climbingPrepTimer = GetNode <Timer> ("ClimbingReadyTimer");
    _energyMeterReplenishRatePerUnit = (float)EnergyMeterReplenishTimeSeconds / EnergyMeterUnits;
    _energyMeterDepletionRatePerUnit = (float)EnergyMeterDepletionTimeSeconds / EnergyMeterUnits;
    _sprite.Animation = IdleLeftAnimation;
    _sprite.Play();
    InitializeStateMachine();
    LoopAudio (_audio.Stream, CliffArrestingSoundLoopBeginSeconds, CliffArrestingSoundLoopEndSeconds);
  }

  public override void _PhysicsProcess (float delta)
  {
    _stateMachine.Update();
    HorizontalVelocity();
    VerticalVelocity();
    Animations();
    SoundEffects();
    if (Mathf.Abs (_velocity.x) < VelocityEpsilon) _velocity.x = 0.0f;
    if (Mathf.Abs (_velocity.y) < VelocityEpsilon) _velocity.y = 0.0f;
    _velocity = MoveAndSlide (_velocity, Vector2.Up);
    CalculateFallingStats();
    PrintLine (DumpState());
    Print();

    if (_stateMachine.Is (State.ClimbingUp) && IsDepletingEnergy() &&
        !Mathf.IsEqualApprox (_energyTimer.WaitTime, _energyMeterDepletionRatePerUnit))
    {
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
    }

    if (_stateMachine.Is (State.ClimbingUp) && IsReplenishingEnergy() &&
        !Mathf.IsEqualApprox (_energyTimer.WaitTime, _energyMeterReplenishRatePerUnit))
    {
      _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    }
  }

  public override void _UnhandledInput (InputEvent @event)
  {
    if (IsReleased (Input.Text, @event)) _label.Visible = !_label.Visible;
    if (IsReleased (Input.Respawn, @event)) Respawn();
    if (IsDownArrowPressed()) CheckDropDownThrough();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnEnergyTimerTimeout()
  {
    if (!_stateMachine.Is (State.Running) && !IsSpeedClimbing() && _energy == MaxEnergy)
    {
      _energyTimer.Stop();

      return;
    }

    if (IsDepletingEnergy() && (_stateMachine.Is (State.Running) || _stateMachine.Is (State.Jumping) || IsSpeedClimbing()))
    {
      _energy -= 1;
      _energyMeter.Value = _energy;

      return;
    }

    if (_energy == MaxEnergy && (_stateMachine.Is (State.Running) || IsSpeedClimbing())) return;

    _energy += 1;
    _energyMeter.Value = _energy;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnGroundEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Debug ($"Player entered ground.");
    _isInGround = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnGroundExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Debug ($"Player exited ground.");
    _isInGround = false;
    if (_stateMachine.Is (State.ClimbingUp)) RestAfterClimbingUp();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCliffEdgeExited (Node body)
  {
    if (!body.IsInGroup ("Player") || !_stateMachine.Is (State.FreeFalling) || _justRespawned) return;

    _log.Debug ("Player exited cliff edge.");
    _wasInCliffEdge = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnPlayerAreaColliderBodyEntered (Node body)
  {
    if (body is not TileMap { Name: "Signs" } tileMap) return;

    _log.Debug ($"Player entered sign.");
    _isInSign = true;
    _signsTileMap = tileMap;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnPlayerAreaColliderBodyExited (Node body)
  {
    if (body is not TileMap { Name: "Signs" }) return;

    _log.Debug ($"Player exited sign.");
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
          var tileName = GetIntersectingTileName (_area, _sprite.Animation, tileMap);

          if (tileName.Empty()) continue;

          DropDownThrough (tileName, tileMap);

          break;
        }
      }
    }
  }

  private async void DropDownThrough (PhysicsBody2D body)
  {
    _isDroppingDown = true;
    body.SetCollisionMaskBit (0, false);
    SetCollisionMaskBit (1, false);
    await ToSignal (GetTree(), "idle_frame");
    body.SetCollisionMaskBit (0, true);
    SetCollisionMaskBit (1, true);
    _isDroppingDown = false;
  }

  private async void DropDownThrough (string tileName, TileMap tileMap)
  {
    _isDroppingDown = true;
    tileMap.SetCollisionMaskBit (0, false);
    SetCollisionMaskBit (1, false);
    for (var i = 0; i < TileNamesToDropdownFrames[tileName]; ++i) await ToSignal (GetTree(), "idle_frame");
    tileMap.SetCollisionMaskBit (0, true);
    SetCollisionMaskBit (1, true);
    _isDroppingDown = false;
  }

  private async void RestAfterClimbingUp()
  {
    _isResting = true;
    await ToSignal (GetTree().CreateTimer (ClimbingUpToNewLevelRestTimeSeconds, false), "timeout");
    _isResting = false;
  }

  private void ReadSign()
  {
    var cell = GetTileCellAtCenterOf (_area, _sprite.Animation, _signsTileMap);
    var name = GetReadableSignName (cell);

    if (!HasReadableSign (name))
    {
      _log.Warn ($"Attempting to read non-existent sign: {name}.");
      _stateMachine.To (State.Idle);

      return;
    }

    var readableSign = GetReadableSign (name);
    _waterfall.GetNode <AudioStreamPlayer2D> ("Water 9/AudioStreamPlayer2D").Attenuation = 4.0f;
    _waterfall.GetNode <AudioStreamPlayer2D> ("Water 10/AudioStreamPlayer2D").Attenuation = 4.0f;

    for (var i = 1; i <= 3; ++i)
    {
      var mist = _waterfall.GetNode <AnimatedSprite> ("Mist " + i);
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
    _signsTileMap.GetNode <TileMap> ("../Signs Winter Layer").Visible = _cliffs.CurrentSeason == Cliffs.Season.Winter;
    _camera.Zoom = Vector2.One;
    _camera.Position = new Vector2 (0, -355);
    _camera.ForceUpdateScroll();
    _camera.Position = new Vector2 (0, 0);
    _waterfall.GetNode <AudioStreamPlayer2D> ("Water 9/AudioStreamPlayer2D").Attenuation = 8.28f;
    _waterfall.GetNode <AudioStreamPlayer2D> ("Water 10/AudioStreamPlayer2D").Attenuation = 8.28f;

    for (var i = 1; i <= 3; ++i)
    {
      var mist = _waterfall.GetNode <AnimatedSprite> ("Mist " + i);
      mist.ZIndex = 1;
      mist.Modulate = new Color (Modulate.r, Modulate.g, Modulate.b);
    }
  }

  private void Respawn()
  {
    _stateMachine.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    GlobalPosition = new Vector2 (952, -4032);
    _sprite.Animation = IdleLeftAnimation;
    _justRespawned = true;
  }

  // @formatter:off
  private bool HasReadableSign() => HasReadableSign (GetReadableSignName());
  private bool HasReadableSign (string name) => _signsTileMap?.HasNode ("../" + name) ?? false;
  private string GetReadableSignName() => GetReadableSignName (GetIntersectingTileCell (_area, _sprite.Animation, _signsTileMap));
  private static string GetReadableSignName (Vector2 tileSignCell) => "Readable Sign (" + tileSignCell.x + ", " + tileSignCell.y + ")";
  private Sprite GetReadableSign (string name) => _signsTileMap?.GetNode <Sprite> ("../" + name);
  private bool IsSpeedClimbing() => _stateMachine.Is (State.ClimbingUp) && IsEnergyKeyPressed();
  private static int GetClimbingSpeedBoost() => IsEnergyKeyPressed() ? 2 : 1;
  private bool IsDepletingEnergy() => _energy > 0 && IsEnergyKeyPressed();
  private bool JustDepletedAllEnergy() => _energy == 0 && IsEnergyKeyPressed();
  private bool IsReplenishingEnergy() => _energy < MaxEnergy && !IsEnergyKeyPressed();
  private bool IsMoving() => IsMovingHorizontally() || IsMovingVertically();
  private bool IsMovingVertically() => Mathf.Abs (_velocity.y) > VelocityEpsilon;
  private bool IsMovingHorizontally() => Mathf.Abs (_velocity.x) > VelocityEpsilon;
  private bool IsMovingUp() => _velocity.y + VelocityEpsilon < 0.0f;
  private bool IsMovingDown() => _velocity.y - VelocityEpsilon > 0.0f;
  private float GetFallingTimeSeconds() => _elapsedFallingTimeMs > 0 ? _elapsedFallingTimeMs / 1000.0f : _lastTotalFallingTimeMs / 1000.0f;
  private void PrintLine (string line) => _printLines.Add (line);
  // @formatter:on

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
    if (!IsAnyHorizontalArrowPressed()) _velocity.x *= HorizontalRunJumpStoppingFriction;
    else if (_stateMachine.Is (State.Traversing)) _velocity.x *= TraverseFriction;
    else _velocity.x *= HorizontalRunJumpFriction;
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
    if (_stateMachine.Is (State.ClimbingDown)) _velocity.y = _sprite.Frame is 3 or 8 ? VerticalClimbingSpeed * GetClimbingSpeedBoost() : 0.0f;
    if (_stateMachine.Is (State.Traversing)) _velocity.y = 0.0f;
    if (_stateMachine.Is (State.CliffHanging)) _velocity.y = 0.0f;
    // @formatter:on

    // ReSharper disable once InvertIf
    if (_stateMachine.Is (State.CliffArresting))
    {
      _velocity.y -= CliffArrestingSpeed;
      _velocity.y = SafelyClampMin (_velocity.y, 0.0f);
    }
  }

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

  private async void Animations()
  {
    if (_currentAnimation == _sprite.Animation) return;

    _currentAnimation = _sprite.Animation;

    // Workaround for https://github.com/godotengine/godot/issues/14578 "Changing node parent produces Area2D/3D signal duplicates"
    await ToSignal (GetTree(), "idle_frame");
    for (var i = 1; i <= 6; ++i) _cliffs.GetNode <Area2D> ("Ground " + i + "/Area2D").SetBlockSignals (true);
    await ToSignal (GetTree(), "idle_frame");
    _sprite.GetNode <Area2D> ("Area2D").SetBlockSignals (true);
    _cliffs.SetBlockSignals (true);
    _waterfall.SetBlockSignals (true);
    // End workaround

    foreach (var node in _sprite.GetNode <Area2D> ("Area2D").GetChildren())
    {
      if (node is not CollisionShape2D collider) continue;

      collider.Disabled = collider.Name != _sprite.Animation;
    }

    // Workaround for https://github.com/godotengine/godot/issues/14578 "Changing node parent produces Area2D/3D signal duplicates"
    await ToSignal (GetTree(), "idle_frame");
    for (var i = 1; i <= 6; ++i) _cliffs.GetNode <Area2D> ("Ground " + i + "/Area2D").SetBlockSignals (false);
    await ToSignal (GetTree(), "idle_frame");
    _sprite.GetNode <Area2D> ("Area2D").SetBlockSignals (false);
    _cliffs.SetBlockSignals (false);
    _waterfall.SetBlockSignals (false);
    // End workaround
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

  private void Print()
  {
    _label.Text = "";
    _label.BbcodeText = "";
    foreach (var line in _printLines) _label.AddText (line + "\n");
    _printLines.Clear();
  }

  // @formatter:off

  private string DumpState() =>
    "\nState: " + _stateMachine.GetState() +
    "\nSeason: " + _cliffs.CurrentSeason +
    "\nIdle: " + _stateMachine.Is (State.Idle) +
    "\nWalking: " + _stateMachine.Is (State.Walking) +
    "\nRunning: " + _stateMachine.Is (State.Running) +
    "\nEnergy: " + _energy +
    "\nEnergy timer: " + _energyTimer.TimeLeft +
    "\nJumping: " + _stateMachine.Is (State.Jumping) +
    "\nClimbing prep: " + _stateMachine.Is (State.ClimbingPrep) +
    "\nClimbing prep timer: " + _climbingPrepTimer.TimeLeft +
    "\nClimbing up: " + _stateMachine.Is (State.ClimbingUp) +
    "\nClimbing down: " + _stateMachine.Is (State.ClimbingDown) +
    "\nIsSpeedClimbing: " + IsSpeedClimbing() +
    "\nIsTouchingCliffIce: " + IsTouchingCliffIce +
    "\nIsInFrozenWaterfall: " + IsInFrozenWaterfall +
    "\nCliff arresting: " + _stateMachine.Is (State.CliffArresting) +
    "\nCliff hanging: " + _stateMachine.Is (State.CliffHanging) +
    "\nClimbing Traversing: " + _stateMachine.Is (State.Traversing) +
    "\nFree falling: " + _stateMachine.Is (State.FreeFalling) +
    "\nIsOnFloor(): " + IsOnFloor() +
    "\nIsHittingWall(): " + IsHittingWall() +
    "\nIsInCliffs: " + IsInCliffs +
    "\nIsInGround: " + _isInGround +
    "\n_isInSign: " + _isInSign +
    "\n_isResting: " + _isResting +
    "\n_wasRunning: " + _wasRunning +
    "\n_wasInCliffEdge: " + _wasInCliffEdge +
    "\n_isDroppingDown: " + _isDroppingDown +
    "\n_justRespawned: " + _justRespawned +
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
    // @formatter:off
    _stateMachine = new StateMachine <State> (TransitionTable, InitialState);
    _stateMachine.OnTransition (State.Walking, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransition (State.Running, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransition (State.CliffArresting, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransition (State.ClimbingDown, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransition (State.FreeFalling, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransition (State.ReadingSign, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    _stateMachine.OnTransitionFrom (State.ReadingSign, StopReadingSign);
    _stateMachine.OnTransitionTo (State.ReadingSign, ReadSign);
    _stateMachine.OnTransitionFrom (State.ClimbingPrep, () => _climbingPrepTimer.Stop());
    _stateMachine.OnTransitionTo (State.Traversing, () => _sprite.Animation = TraversingAnimation);
    _stateMachine.OnTransitionTo (State.FreeFalling, () => _sprite.Animation = FreeFallingAnimation);
    // @formatter:on

    _stateMachine.OnTransitionFrom (State.Idle, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
      if (_sprite.IsInGroup ("Perchable")) _sprite.RemoveFromGroup ("Perchable");
    });

    _stateMachine.OnTransitionTo (State.Idle, () =>
    {
      if (!IsInGroup ("Perchable Parent")) AddToGroup ("Perchable Parent");
      if (!_sprite.IsInGroup ("Perchable")) _sprite.AddToGroup ("Perchable");
      _wasRunning = false;
    });

    _stateMachine.OnTransitionFrom (State.CliffHanging, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
      if (_sprite.IsInGroup ("Perchable")) _sprite.RemoveFromGroup ("Perchable");
    });

    _stateMachine.OnTransitionTo (State.CliffHanging, () =>
    {
      _sprite.Animation = CliffHangingAnimation;
      if (!IsInGroup ("Perchable Parent")) AddToGroup ("Perchable Parent");
      if (!_sprite.IsInGroup ("Perchable")) _sprite.AddToGroup ("Perchable");
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

    _stateMachine.OnTransition (State.ClimbingPrep, State.Idle, () =>
    {
      _sprite.Animation = IdleLeftAnimation;
      FlipHorizontally (_wasFlippedHorizontally);
    });

    _stateMachine.OnTransitionTo (State.ClimbingPrep, () =>
    {
      _sprite.Animation = ClimbingPrepAnimation;
      _wasFlippedHorizontally = _isFlippedHorizontally;
      FlipHorizontally (false);
      _climbingPrepTimer.Start();
    });

    // TODO Add state machine method OnTransitionExceptFrom (State.ReadingSign, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    // TODO This eliminates transition repetition when an exception is needed (all transitions to a state, except from a specific state).
    // TODO Catch-all OnTransitionFrom (State.Running) replenishes the energy meter before the running => jumping transition has a chance to start depleting it.
    // TODO Running and jumping uses energy energy meter while jumping.
    _stateMachine.OnTransition (State.Running, State.Jumping, () => { _energyTimer.Start (_energyMeterDepletionRatePerUnit); });

    _stateMachine.OnTransitionFrom (State.Running, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _stateMachine.OnTransitionTo (State.Running, () =>
    {
      _sprite.Animation = RunAnimation;
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
      _wasRunning = true;
    });

    _stateMachine.OnTransitionFrom (State.ClimbingUp, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _stateMachine.OnTransitionTo (State.ClimbingUp, () =>
    {
      _sprite.Animation = ClimbingUpAnimation;
      FlipHorizontally (false);
    });

    _stateMachine.OnTransitionTo (State.ClimbingDown, () =>
    {
      _sprite.Animation = ClimbingDownAnimation;
      FlipHorizontally (false);
    });

    _stateMachine.OnTransitionFrom (State.FreeFalling, () =>
    {
      _wasInCliffEdge = false;
      _justRespawned = false;
    });

    // @formatter:off

    // TODO Move conditions into state machine conditions, leaving only input for triggers.
    _stateMachine.AddTrigger (State.Idle, State.Walking, () => IsOneActiveOf (Input.Horizontal) && !IsDepletingEnergy());
    _stateMachine.AddTrigger (State.Idle, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsDepletingEnergy() && !IsHittingWall());
    _stateMachine.AddTrigger (State.Idle, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Idle, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs && !_isInSign && !_isResting);
    _stateMachine.AddTrigger (State.Idle, State.ClimbingDown, () => IsDownArrowPressed() && IsMovingDown() && !IsOnFloor() && IsInCliffs && !_isDroppingDown);
    _stateMachine.AddTrigger (State.Idle, State.FreeFalling, () => !IsDownArrowPressed() && IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.Idle, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && HasReadableSign() && !_isResting);
    _stateMachine.AddTrigger (State.Walking, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally() && !(_isInSign && IsUpArrowPressed()));
    _stateMachine.AddTrigger (State.Walking, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsMovingHorizontally() && IsDepletingEnergy());
    _stateMachine.AddTrigger (State.Walking, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Walking, State.FreeFalling, () => IsMovingDown() && !IsOnFloor() && !IsHittingWall());
    _stateMachine.AddTrigger (State.Walking, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs && !_isInSign);
    _stateMachine.AddTrigger (State.Walking, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && HasReadableSign());
    _stateMachine.AddTrigger (State.Running, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.Running, State.Walking, () => IsOneActiveOf (Input.Horizontal) && !IsDepletingEnergy() || JustDepletedAllEnergy());
    _stateMachine.AddTrigger (State.Running, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Running, State.FreeFalling, () => IsMovingDown() && !IsOnFloor() && !IsHittingWall());
    _stateMachine.AddTrigger (State.Running, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs && !_isInSign);
    _stateMachine.AddTrigger (State.Running, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && HasReadableSign());
    _stateMachine.AddTrigger (State.Jumping, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.ClimbingPrep, State.Idle, WasUpArrowReleased);
    _stateMachine.AddTrigger (State.ClimbingPrep, State.ClimbingUp, () => IsUpArrowPressed() && _climbingPrepTimer.TimeLeft == 0 && !IsTouchingCliffIce && !IsInFrozenWaterfall);
    _stateMachine.AddTrigger (State.ClimbingUp, State.FreeFalling, () => (WasJumpKeyPressed() || !IsInCliffs && !_isInGround || IsTouchingCliffIce || IsInFrozenWaterfall || JustDepletedAllEnergy()) && !_isResting);
    _stateMachine.AddTrigger (State.ClimbingUp, State.Idle, () => _isResting);
    _stateMachine.AddTrigger (State.ClimbingUp, State.CliffHanging, () => WasUpArrowReleased() && !IsDownArrowPressed() && (IsInCliffs || _isInGround));
    _stateMachine.AddTrigger (State.ClimbingUp, State.ClimbingDown, () => WasUpArrowReleased() && IsDownArrowPressed() && (IsInCliffs || _isInGround));
    _stateMachine.AddTrigger (State.ClimbingDown, State.CliffHanging, () => WasDownArrowReleased() && !IsUpArrowPressed() && (IsInCliffs || _isInGround));
    _stateMachine.AddTrigger (State.ClimbingDown, State.Idle, IsOnFloor);
    _stateMachine.AddTrigger (State.ClimbingDown, State.ClimbingUp, () => WasDownArrowReleased() && IsUpArrowPressed() && (IsInCliffs || _isInGround));
    _stateMachine.AddTrigger (State.ClimbingDown, State.FreeFalling, () => WasJumpKeyPressed() || !IsInCliffs && !_isInGround || IsTouchingCliffIce || IsInFrozenWaterfall || _isResting);
    _stateMachine.AddTrigger (State.FreeFalling, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && IsOnFloor() && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.FreeFalling, State.Walking, () => IsOneActiveOf (Input.Horizontal) && IsOnFloor() && !IsDepletingEnergy() );
    _stateMachine.AddTrigger (State.FreeFalling, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsOnFloor() && IsDepletingEnergy());
    _stateMachine.AddTrigger (State.FreeFalling, State.CliffArresting, () => IsItemKeyPressed() && IsInCliffs && _velocity.y >= CliffArrestingActivationVelocity);
    _stateMachine.AddTrigger (State.FreeFalling, State.CliffHanging, () => _wasInCliffEdge && (IsInCliffs || _isInGround));
    _stateMachine.AddTrigger (State.CliffHanging, State.ClimbingUp, IsUpArrowPressed);
    _stateMachine.AddTrigger (State.CliffHanging, State.ClimbingDown, IsDownArrowPressed);
    _stateMachine.AddTrigger (State.CliffHanging, State.FreeFalling, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.CliffHanging, State.Traversing, () => IsOneActiveOf (Input.Horizontal));
    _stateMachine.AddTrigger (State.Traversing, State.FreeFalling, () => WasJumpKeyPressed() || !IsInCliffs && !_isInGround || IsTouchingCliffIce || IsInFrozenWaterfall);
    _stateMachine.AddTrigger (State.Traversing, State.CliffHanging, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.CliffArresting, State.FreeFalling, WasItemKeyReleased);
    _stateMachine.AddTrigger (State.CliffArresting, State.CliffHanging, () => !IsOnFloor() && !IsMoving() && IsInCliffs);
    _stateMachine.AddTrigger (State.CliffArresting, State.Idle, IsOnFloor);
    _stateMachine.AddTrigger (State.ReadingSign, State.Idle, ()=> IsDownArrowPressed() && !IsUpArrowPressed() && _isInSign);
    _stateMachine.AddTrigger (State.ReadingSign, State.Walking, ()=> IsOneActiveOf (Input.Horizontal) && !IsAnyActiveOf (Input.Vertical) && !IsDepletingEnergy() && _isInSign);
    _stateMachine.AddTrigger (State.ReadingSign, State.Running, ()=> IsOneActiveOf (Input.Horizontal) && !IsAnyActiveOf (Input.Vertical) && IsDepletingEnergy() && _isInSign);

    // @formatter:on
  }
}