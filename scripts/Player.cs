using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using static PlayerStateMachine;
using static Seasons;
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
//   Cliff arresting: Using pickaxe to slow a cliff fall
//   Cliffhanging: Attached to a cliff above the ground, not moving
//   Traversing: Climbing horizontally
//   Free falling: Moving down, without cliff arresting, can also be coming down from a jump.
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
  [Export] public float CameraSmoothingDeactivationVelocity = 800.0f;
  [Export] public float VelocityEpsilon = 1.0f;
  [Export] public float JumpPower = 800.0f;
  [Export] public float Gravity = 30.0f;
  [Export] public int MaxEnergy = 20;
  [Export] public string IdleLeftAnimation = "player_idle_left";
  [Export] public string IdleBackAnimation = "player_idle_back";
  [Export] public string ClimbingPrepAnimation = "player_idle_back";
  [Export] public string FreeFallingAnimation = "player_free_falling";
  [Export] public string ClimbingUpAnimation = "player_climbing_up";
  [Export] public string CliffHangingAnimation = "player_cliff_hanging";
  [Export] public string TraversingAnimation = "player_cliff_hanging";
  [Export] public string CliffArrestingAnimation = "player_cliff_arresting";
  [Export] public string WalkLeftAnimation = "player_walking_left";
  [Export] public string RunLeftAnimation = "player_running_left";
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
  [Export] public Log.Level LogLevel = Log.Level.Info;
  private readonly RandomNumberGenerator _rng = new();
  private Vector2 _velocity;
  private RichTextLabel _label = null!;
  private AnimationPlayer _primaryAnimator = null!;
  private Area2D _animationColliderParent = null!;
  private TextureProgress _energyMeter = null!;
  private AudioStreamPlayer _audio = null!;
  private Timer _energyTimer = null!;
  private Timer _climbingPrepTimer = null!;
  private Timer _cameraSmoothingTimer = null!;
  private int _energy;
  private float _energyMeterReplenishRatePerUnit;
  private float _energyMeterDepletionRatePerUnit;
  private bool _isFlippedHorizontally;
  private bool _wasFlippedHorizontally;
  private readonly List <string> _printLinesOnce = new();
  private readonly List <string> _printLinesContinuously = new();
  private IStateMachine <State> _stateMachine = null!;
  private PlayerClothing _clothing;
  private ulong _fallingStartTimeMs;
  private ulong _elapsedFallingTimeMs;
  private ulong _lastTotalFallingTimeMs;
  private float _highestVerticalVelocity;
  private bool _isInGround;
  private bool _wasRunning;
  private bool _isInSign;
  private bool _wasInCliffEdge;
  private bool _justRespawned;
  private Camera2D _camera;
  private Sprite _readableSign;
  private TileMap _signsTileMap;
  private Cliffs _cliffs;
  private Weapon _weapon;
  private IDropdownable _dropdown;
  private volatile string _currentAnimation;
  private volatile bool _isResting;
  private List <Area2D> _groundAreas;
  private List <RayCast2D> _groundDetectors;
  private List <RayCast2D> _wallDetectors;
  private List <Waterfall> _waterfalls;
  private Log _log;

  [SuppressMessage ("ReSharper", "ExplicitCallerInfoArgument")]
  public override void _Ready()
  {
    _log = new Log (Name) { CurrentLevel = LogLevel };
    _rng.Randomize();
    _clothing = new PlayerClothing (GetNode ("Animations/Sprites"), LogLevel, Name);
    _weapon = new Weapon (GetNode <Node2D> ("Animations"), LogLevel);
    _camera = GetNode <Camera2D> ("Camera");
    _groundDetectors = GetNodesInGroups <RayCast2D> (GetTree(), "Ground Detectors", "Player");
    _wallDetectors = GetNodesInGroups <RayCast2D> (GetTree(), "Wall Detectors", "Player");
    _waterfalls = GetNodesInGroups <Waterfall> (GetTree(), "Waterfall", "Parent");
    _groundAreas = GetNodesInGroup <Area2D> (GetTree(), "Ground");
    _dropdown = new Dropdown (this, _groundDetectors, Name);
    _cliffs = GetNode <Cliffs> ("../Cliffs");
    _audio = GetNode <AudioStreamPlayer> ("Audio Players/Sound Effects");
    _audio.Stream = ResourceLoader.Load <AudioStream> (CliffArrestingSoundFile);
    _label = GetNode <RichTextLabel> ("../UI/Control/Debugging Text");
    _label.Visible = false;
    _animationColliderParent = GetNode <Area2D> ("Animations/Area Colliders");
    _energyMeter = GetNode <TextureProgress> ("../UI/Control/Energy Meter");
    _energyMeter.Value = MaxEnergy;
    _energy = MaxEnergy;
    _energyTimer = GetNode <Timer> ("Timers/Energy");
    _climbingPrepTimer = GetNode <Timer> ("Timers/ClimbingPrep");
    _cameraSmoothingTimer = GetNode <Timer> ("Camera/SmoothingTimer");
    _energyMeterReplenishRatePerUnit = (float)EnergyMeterReplenishTimeSeconds / EnergyMeterUnits;
    _energyMeterDepletionRatePerUnit = (float)EnergyMeterDepletionTimeSeconds / EnergyMeterUnits;
    _primaryAnimator = GetNode <AnimationPlayer> ("Animations/Players/Primary");
    _primaryAnimator.Play (IdleLeftAnimation);
    InitializeStateMachine();
    LoopAudio (_audio.Stream, CliffArrestingSoundLoopBeginSeconds, CliffArrestingSoundLoopEndSeconds);
  }

  public override void _PhysicsProcess (float delta)
  {
    _stateMachine.Update();
    _weapon.Update (delta);
    HorizontalVelocity();
    VerticalVelocity();
    Animations();
    SoundEffects();
    if (Mathf.Abs (_velocity.x) < VelocityEpsilon) _velocity.x = 0.0f;
    if (Mathf.Abs (_velocity.y) < VelocityEpsilon) _velocity.y = 0.0f;
    _velocity = MoveAndSlide (_velocity, Vector2.Up);
    CalculateFallingStats();
    PrintLineOnce (DumpState());
    Print();

    // @formatter:off

    if (AreAlmostEqual (_camera.GetCameraScreenCenter().x, GlobalPosition.x, 1.0f)) StopCameraSmoothing();

    if (Mathf.Abs (_velocity.x) >= CameraSmoothingDeactivationVelocity ||
        Mathf.Abs (_velocity.y) >= CameraSmoothingDeactivationVelocity)
    {
      StopCameraSmoothing();
    }

    if ((_stateMachine.Is (State.ClimbingUp) || _stateMachine.Is (State.ClimbingDown)) && IsDepletingEnergy() &&
        !Mathf.IsEqualApprox (_energyTimer.WaitTime, _energyMeterDepletionRatePerUnit))
    {
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
    }

    if ((_stateMachine.Is (State.ClimbingUp) || _stateMachine.Is (State.ClimbingDown)) && IsReplenishingEnergy() &&
        !Mathf.IsEqualApprox (_energyTimer.WaitTime, _energyMeterReplenishRatePerUnit))
    {
      _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    }

    // @formatter:on
  }

  public override async void _UnhandledInput (InputEvent @event)
  {
    if (IsReleased (Input.Text, @event)) _label.Visible = !_label.Visible;
    if (IsReleased (Input.Respawn, @event)) Respawn();
    if (WasMouseLeftClicked (@event)) _clothing.UpdateAll (_primaryAnimator.CurrentAnimation);

    // This async call must be the last line in this method.
    if (IsDownArrowPressed() && !_stateMachine.Is (State.ReadingSign)) await _dropdown.Drop();
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
  public void _OnCameraSmoothingTimerTimeout()
  {
    if (IsMoving()) return;

    StartCameraSmoothing();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnGroundEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Debug ("Player entered ground.");
    _isInGround = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnGroundExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Debug ("Player exited ground.");
    _isInGround = false;
    if (_stateMachine.Is (State.ClimbingUp)) RestAfterClimbingUp();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCliffEdgeExited (Node body)
  {
    if (body is not KinematicBody2D || !body.IsInGroup ("Player") || !_stateMachine.Is (State.FreeFalling) || _justRespawned) return;

    _log.Debug ("Player exited cliff edge.");
    _wasInCliffEdge = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnPlayerAreaColliderBodyEntered (Node body)
  {
    if (body is not TileMap { Name: "Signs" } tileMap) return;

    _log.Debug ("Player entered sign.");
    _isInSign = true;
    _signsTileMap = tileMap;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnPlayerAreaColliderBodyExited (Node body)
  {
    if (body is not TileMap { Name: "Signs" }) return;

    _log.Debug ("Player exited sign.");
    _isInSign = false;
    if (_stateMachine.Is (State.ReadingSign)) _stateMachine.To (State.Idle);
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnAnimationStarted (string animationName)
  {
    _clothing.OnAnimation (animationName);

    switch (animationName)
    {
      case "player_equipping_left":
      case "player_equipping_back":
      {
        _weapon.OnEquipAnimationStarted();

        break;
      }
      case "player_unequipping_left":
      case "player_unequipping_back":
      {
        _weapon.OnUnequipAnimationStarted();

        break;
      }
    }

    _clothing.UpdateSecondary();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnAnimationFinished (string animationName)
  {
    switch (animationName)
    {
      case "player_equipping_left":
      case "player_equipping_back":
      {
        _weapon.OnEquipAnimationFinished();

        break;
      }
      case "player_unequipping_left":
      case "player_unequipping_back":
      {
        _weapon.OnUnequipAnimationFinished();

        break;
      }
    }

    _clothing.UpdateSecondary();
  }

  private bool IsInWaterfall() => _waterfalls.Any (x => x.IsPlayerInWaterfall);
  private bool IsInFrozenWaterfall() => _waterfalls.Any (x => x.IsPlayerInFrozenWaterfall);

  private async void RestAfterClimbingUp()
  {
    _isResting = true;
    await ToSignal (GetTree().CreateTimer (ClimbingUpToNewLevelRestTimeSeconds, false), "timeout");
    _isResting = false;
  }

  private void ReadSign()
  {
    var cell = GetTileCellAtCenterOf (_animationColliderParent, _primaryAnimator.CurrentAnimation, _signsTileMap);
    var name = GetReadableSignName (cell);

    if (!HasReadableSign (name))
    {
      _log.Warn ($"Attempting to read non-existent sign: {name}.");
      _stateMachine.To (State.Idle);

      return;
    }

    var readableSign = GetReadableSign (name);
    _waterfalls.ForEach (x => x.ChangeSettings (4.0f, 2, Modulate.a * 0.2f));
    _signsTileMap.Visible = false;
    _signsTileMap.GetNode <TileMap> ("Winter Layer").Visible = false;
    readableSign.Visible = true;
    _readableSign = readableSign;
    _velocity = Vector2.Zero;
    StopCameraSmoothing();
    _camera.Zoom = Vector2.One / 10;
    _readableSign.Position = _camera.GetCameraScreenCenter();
    Visible = false;
  }

  private void StopReadingSign()
  {
    if (_readableSign == null) return;

    Visible = true;
    _readableSign.Visible = false;
    _signsTileMap.Visible = true;
    _signsTileMap.GetNode <TileMap> ("Winter Layer").Visible = _cliffs.CurrentSeasonIs (Season.Winter);
    _camera.Zoom = Vector2.One;
    _camera.Position = new Vector2 (0, -355);
    _camera.ForceUpdateScroll();
    _camera.Position = new Vector2 (0, 0);
    _waterfalls.ForEach (x => x.ChangeSettings (8.8f, 33));
  }

  private void Respawn()
  {
    _stateMachine.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    StopCameraSmoothing();
    GlobalPosition = new Vector2 (952, -4032);
    _primaryAnimator.Play (IdleLeftAnimation);
    _justRespawned = true;
    _log.Info ("Player respawned.");
  }

  private void StartCameraSmoothing()
  {
    _camera.SmoothingEnabled = true;
    _camera.DragMarginHEnabled = false;
  }

  private void StopCameraSmoothing()
  {
    _camera.SmoothingEnabled = false;
    _camera.DragMarginHEnabled = true;
  }

  // @formatter:off
  private bool IsInCliffs() => _cliffs.Encloses (GetCurrentAnimationColliderRect);
  private bool IsTouchingCliffIce() => _cliffs.IsTouchingIce (GetCurrentAnimationColliderRect);
  private Rect2 GetCurrentAnimationColliderRect => GetColliderRect (_animationColliderParent, GetCurrentAnimationCollider());
  private CollisionShape2D GetCurrentAnimationCollider() => _animationColliderParent.GetNode <CollisionShape2D> (_primaryAnimator.CurrentAnimation);
  private bool HasReadableSign() => HasReadableSign (GetReadableSignName());
  private bool HasReadableSign (string name) => _signsTileMap?.HasNode ("../" + name) ?? false;
  private string GetReadableSignName() => GetReadableSignName (GetIntersectingTileCell (_animationColliderParent, _primaryAnimator.CurrentAnimation, _signsTileMap));
  private static string GetReadableSignName (Vector2 tileSignCell) => "Readable Sign (" + tileSignCell.x + ", " + tileSignCell.y + ")";
  private Sprite GetReadableSign (string name) => _signsTileMap?.GetNode <Sprite> ("../" + name);
  private bool IsSpeedClimbing() => (_stateMachine.Is (State.ClimbingUp) || _stateMachine.Is (State.ClimbingDown)) && IsEnergyKeyPressed();
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
  private void PrintLineOnce (string line) => _printLinesOnce.Add (line);
  private void PrintLineContinuously (string line) => _printLinesContinuously.Add (line);
  private void StopPrintingContinuousLine (string line) => _printLinesContinuously.Remove (line);
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
    if (_stateMachine.Is (State.ClimbingUp)) _velocity.y = AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 4 * 0.2f, 0.1f) || AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 9 * 0.2f, 0.1f) ? -VerticalClimbingSpeed * GetClimbingSpeedBoost() : 0.0f;
    if (_stateMachine.Is (State.ClimbingDown)) _velocity.y = AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 6 * 0.2f, 0.1f) || AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 1 * 0.2f, 0.1f) ? VerticalClimbingSpeed * GetClimbingSpeedBoost() : 0.0f;
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

  private bool IsTouchingWall() =>
    _wallDetectors.Any (x =>
      x.GetCollider() is StaticBody2D collider && collider.IsInGroup ("Walls") ||
      x.GetCollider() is TileMap collider2 && collider2.IsInGroup ("Walls"));

  private bool IsTouchingGround() =>
    _groundDetectors.Any (x =>
      x.GetCollider() is StaticBody2D collider && collider.IsInGroup ("Ground") ||
      x.GetCollider() is TileMap collider2 && collider2.IsInGroup ("Ground"));

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
    if (_currentAnimation == _primaryAnimator.CurrentAnimation) return;

    _currentAnimation = _primaryAnimator.CurrentAnimation;

    // Workaround for https://github.com/godotengine/godot/issues/14578 "Changing node parent produces Area2D/3D signal duplicates"
    // @formatter:off
    _groundAreas.ForEach (x => x.SetBlockSignals (true));
    _animationColliderParent.SetBlockSignals (true);
    _waterfalls.ForEach (x => x.SetBlockSignals (true));
    // @formatter:on
    // End workaround

    foreach (var node in _animationColliderParent.GetChildren())
    {
      if (node is not CollisionShape2D collider) continue;

      collider.Disabled = collider.Name != _primaryAnimator.CurrentAnimation;
    }

    // Workaround for https://github.com/godotengine/godot/issues/14578 "Changing node parent produces Area2D/3D signal duplicates"
    // @formatter:off
    await ToSignal (GetTree(), "idle_frame");
    _groundAreas.ForEach (x => x.SetBlockSignals (false));
    _waterfalls.ForEach (x => x.SetBlockSignals (false));
    await ToSignal (GetTree(), "idle_frame");
    _animationColliderParent.SetBlockSignals (false);
    // @formatter:on
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
      _fallingStartTimeMs = Time.GetTicksMsec();
    }
    else if (IsMovingDown())
    {
      _elapsedFallingTimeMs = Time.GetTicksMsec() - _fallingStartTimeMs;
    }
    else if (!IsMovingDown() && _elapsedFallingTimeMs > 0)
    {
      _lastTotalFallingTimeMs = Time.GetTicksMsec() - _fallingStartTimeMs;
      _fallingStartTimeMs = 0;
      _elapsedFallingTimeMs = 0;
    }
  }

  private void Print()
  {
    _label.Text = "";
    _label.BbcodeText = "";
    _label.GetVScroll().Visible = false;
    foreach (var line in _printLinesContinuously) _label.AddText (line + "\n");
    foreach (var line in _printLinesOnce) _label.AddText (line + "\n");
    _printLinesOnce.Clear();
  }

  // @formatter:off

  private string DumpState() =>
    "\nState: " + _stateMachine.GetState() +
    "\nWeapon state: " + _weapon.GetState() +
    "\nAnimation: " + _primaryAnimator.CurrentAnimation +
    "\nSeason: " + _cliffs.GetCurrentSeason() +
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
    "\nIsInWaterfall: " +  IsInWaterfall() +
    "\nIsInFrozenWaterfall: " + IsInFrozenWaterfall() +
    "\nIsTouchingCliffIce: " + IsTouchingCliffIce() +
    "\nCliff arresting: " + _stateMachine.Is (State.CliffArresting) +
    "\nCliff hanging: " + _stateMachine.Is (State.CliffHanging) +
    "\nClimbing Traversing: " + _stateMachine.Is (State.Traversing) +
    "\nFree falling: " + _stateMachine.Is (State.FreeFalling) +
    "\nDropping down: " + _dropdown.IsDropping() +
    "\nIsOnFloor(): " + IsOnFloor() +
    "\nIsTouchingGround(): " + IsTouchingGround() +
    "\nIsTouchingWall(): " + IsTouchingWall() +
    "\nIsInCliffs: " + IsInCliffs() +
    "\nIsInGround: " + _isInGround +
    "\n_isInSign: " + _isInSign +
    "\n_isResting: " + _isResting +
    "\n_wasRunning: " + _wasRunning +
    "\n_wasInCliffEdge: " + _wasInCliffEdge +
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
    "\nScale: " + Scale +
    "\nVelocity: " + _velocity +
    "\nVertical velocity (mph): " + _velocity.y * 0.028334573333333 +
    "\nHighest vertical velocity: " + _highestVerticalVelocity +
    "\nHighest vertical velocity (mph): " + _highestVerticalVelocity * 0.028334573333333 +
    "\nFalling time (sec): " + GetFallingTimeSeconds() +
    "\nCamera Smoothing: " + _camera.SmoothingEnabled +
    "\nCamera Smoothing Timer: " + _cameraSmoothingTimer.TimeLeft +
    "\nCamera Drag Margin (Horizontal): " + _camera.DragMarginHEnabled +
    "\nCamera screen center: " + _camera.GetCameraScreenCenter() +
    "\nPosition: " + Position +
    "\nGlobal position: " + GlobalPosition;

  // @formatter:on

  private void InitializeStateMachine()
  {
    // @formatter:off
    // ReSharper disable once ExplicitCallerInfoArgument
    _stateMachine = new PlayerStateMachine (InitialState, LogLevel, Name);
    _stateMachine.OnTransitionFrom (State.ReadingSign, StopReadingSign);
    _stateMachine.OnTransitionTo (State.ReadingSign, ReadSign);
    _stateMachine.OnTransitionTo (State.Traversing, () => _primaryAnimator.Play (TraversingAnimation));
    _stateMachine.OnTransitionTo (State.FreeFalling, () => _primaryAnimator.Play (FreeFallingAnimation));
    _stateMachine.OnTransitionFrom (State.ClimbingPrep, () => _climbingPrepTimer.Stop());
    _stateMachine.OnTransition (State.ReadingSign, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _stateMachine.OnTransition (State.CliffArresting, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _stateMachine.OnTransition (State.ClimbingDown, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _stateMachine.OnTransition (State.FreeFalling, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    // @formatter:on

    _stateMachine.OnTransitionFrom (State.Idle, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
      _cameraSmoothingTimer.Stop();
    });

    _stateMachine.OnTransition (State.Running, State.Idle, () =>
    {
      _primaryAnimator.Play (IdleLeftAnimation);
      _cameraSmoothingTimer.Start();
    });

    _stateMachine.OnTransition (State.Walking, State.Idle, () =>
    {
      _primaryAnimator.Play (IdleLeftAnimation);
      _cameraSmoothingTimer.Start();
    });

    _stateMachine.OnTransitionTo (State.Idle, () =>
    {
      if (!IsInGroup ("Perchable Parent")) AddToGroup ("Perchable Parent");
      _wasRunning = false;
    });

    _stateMachine.OnTransitionFrom (State.CliffHanging, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
    });

    _stateMachine.OnTransitionTo (State.CliffHanging, () =>
    {
      _primaryAnimator.Play (CliffHangingAnimation);
      _weapon.Unequip();
      if (!IsInGroup ("Perchable Parent")) AddToGroup ("Perchable Parent");
    });

    _stateMachine.OnTransitionTo (State.Walking, () =>
    {
      _primaryAnimator.Play (WalkLeftAnimation);
      _wasRunning = false;
    });

    _stateMachine.OnTransitionTo (State.Jumping, () =>
    {
      _primaryAnimator.Play (IdleLeftAnimation);
      _velocity.y -= JumpPower;
    });

    _stateMachine.OnTransitionFrom (State.CliffArresting, () =>
    {
      if (!_audio.Playing) return;

      _audio.Stop();
      StopPrintingContinuousLine ("Sound effects: Playing cliff arresting sound.");
      PrintLineContinuously ("Sound effects: Stopped cliff arresting sound.");
    });

    _stateMachine.OnTransitionTo (State.CliffArresting, () =>
    {
      _primaryAnimator.Play (CliffArrestingAnimation);
      _weapon.Unequip();

      if (_audio.Playing) return;

      _audio.Play();
      StopPrintingContinuousLine ("Sound effects: Stopped cliff arresting sound.");
      PrintLineContinuously ("Sound effects: Playing cliff arresting sound.");
    });

    _stateMachine.OnTransition (State.ClimbingPrep, State.Idle, () =>
    {
      _primaryAnimator.Play (IdleLeftAnimation);
      FlipHorizontally (_wasFlippedHorizontally);
    });

    _stateMachine.OnTransitionTo (State.ClimbingPrep, () =>
    {
      _primaryAnimator.Play (ClimbingPrepAnimation);
      _weapon.Unequip();
      _wasFlippedHorizontally = _isFlippedHorizontally;
      FlipHorizontally (false);
      _climbingPrepTimer.Start();
    });

    // TODO Add state machine method OnTransitionExceptFrom (State.ReadingSign, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
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
      _primaryAnimator.Play (RunLeftAnimation);
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
      _wasRunning = true;
    });

    _stateMachine.OnTransitionFrom (State.ClimbingUp, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _stateMachine.OnTransitionFrom (State.ClimbingDown, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _stateMachine.OnTransitionTo (State.ClimbingUp, () =>
    {
      _weapon.Unequip();
      var startFrame = new[] { 1, 6 }[_rng.RandiRange (0, 1)];
      var startTime = startFrame * _primaryAnimator.GetAnimation (ClimbingUpAnimation).Step;
      _primaryAnimator.Play (ClimbingUpAnimation);
      _primaryAnimator.Seek (startTime);
      FlipHorizontally (false);
    });

    _stateMachine.OnTransitionTo (State.ClimbingDown, () =>
    {
      _weapon.Unequip();
      var startFrame = new[] { 3, 8 }[_rng.RandiRange (0, 1)];
      var startTime = startFrame * _primaryAnimator.GetAnimation (ClimbingUpAnimation).Step;
      _primaryAnimator.PlayBackwards (ClimbingUpAnimation);
      _primaryAnimator.Seek (startTime);
      FlipHorizontally (false);
    });

    _stateMachine.OnTransitionFrom (State.FreeFalling, () =>
    {
      _wasInCliffEdge = false;
      _justRespawned = false;
    });

    // @formatter:off

    // TODO Move conditions into state machine conditions, leaving only input for triggers.
    _stateMachine.AddTrigger (State.Idle, State.Walking, () => IsOneActiveOf (Input.Horizontal) && !IsDepletingEnergy() && IsOnFloor());
    _stateMachine.AddTrigger (State.Idle, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsDepletingEnergy() && !IsTouchingWall() && IsOnFloor());
    _stateMachine.AddTrigger (State.Idle, State.Jumping, () => WasJumpKeyPressed() && IsOnFloor());
    _stateMachine.AddTrigger (State.Idle, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs() && !_isInSign && !_isResting);
    _stateMachine.AddTrigger (State.Idle, State.ClimbingDown, () => IsDownArrowPressed() && IsMovingDown() && !IsOnFloor() && IsInCliffs() && !_dropdown.IsDropping() && !IsOneActiveOf (Input.Horizontal));
    _stateMachine.AddTrigger (State.Idle, State.FreeFalling, () => !IsDownArrowPressed() && IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.Idle, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && IsTouchingGround() && HasReadableSign() && !_isResting);
    _stateMachine.AddTrigger (State.Walking, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally() && !(_isInSign && IsUpArrowPressed()));
    _stateMachine.AddTrigger (State.Walking, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsMovingHorizontally() && IsDepletingEnergy());
    _stateMachine.AddTrigger (State.Walking, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Walking, State.FreeFalling, () => IsMovingDown() && !IsOnFloor() && !IsTouchingWall());
    _stateMachine.AddTrigger (State.Walking, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs() && !_isInSign);
    _stateMachine.AddTrigger (State.Walking, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && IsTouchingGround() && HasReadableSign());
    _stateMachine.AddTrigger (State.Running, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.Running, State.Walking, () => IsOneActiveOf (Input.Horizontal) && !IsDepletingEnergy() || JustDepletedAllEnergy());
    _stateMachine.AddTrigger (State.Running, State.Jumping, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.Running, State.FreeFalling, () => IsMovingDown() && !IsOnFloor() && !IsTouchingWall());
    _stateMachine.AddTrigger (State.Running, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs() && !_isInSign);
    _stateMachine.AddTrigger (State.Running, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && IsTouchingGround() && HasReadableSign());
    _stateMachine.AddTrigger (State.Jumping, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _stateMachine.AddTrigger (State.ClimbingPrep, State.Idle, WasUpArrowReleased);
    _stateMachine.AddTrigger (State.ClimbingPrep, State.ClimbingUp, () => IsUpArrowPressed() && _climbingPrepTimer.TimeLeft == 0 && !IsTouchingCliffIce() && !IsInFrozenWaterfall());
    _stateMachine.AddTrigger (State.ClimbingUp, State.FreeFalling, () => (WasJumpKeyPressed() || !IsInCliffs() && !_isInGround || IsTouchingCliffIce() || IsInFrozenWaterfall() || JustDepletedAllEnergy()) && !_isResting);
    _stateMachine.AddTrigger (State.ClimbingUp, State.Idle, () => _isResting);
    _stateMachine.AddTrigger (State.ClimbingUp, State.CliffHanging, () => WasUpArrowReleased() && !IsDownArrowPressed() && (IsInCliffs() || _isInGround));
    _stateMachine.AddTrigger (State.ClimbingUp, State.ClimbingDown, () => WasUpArrowReleased() && IsDownArrowPressed() && (IsInCliffs() || _isInGround));
    _stateMachine.AddTrigger (State.ClimbingDown, State.CliffHanging, () => WasDownArrowReleased() && !IsUpArrowPressed() && (IsInCliffs() || _isInGround));
    _stateMachine.AddTrigger (State.ClimbingDown, State.Idle, IsOnFloor);
    _stateMachine.AddTrigger (State.ClimbingDown, State.ClimbingUp, () => WasDownArrowReleased() && IsUpArrowPressed() && (IsInCliffs() || _isInGround));
    _stateMachine.AddTrigger (State.ClimbingDown, State.FreeFalling, () => WasJumpKeyPressed() || !IsInCliffs() && !_isInGround || IsTouchingCliffIce() || IsInFrozenWaterfall() || _isResting);
    _stateMachine.AddTrigger (State.FreeFalling, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && IsOnFloor() && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.FreeFalling, State.Walking, () => IsOneActiveOf (Input.Horizontal) && IsOnFloor() && !IsDepletingEnergy() );
    _stateMachine.AddTrigger (State.FreeFalling, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsOnFloor() && IsDepletingEnergy());
    _stateMachine.AddTrigger (State.FreeFalling, State.CliffArresting, () => IsItemKeyPressed() && IsInCliffs() && _velocity.y >= CliffArrestingActivationVelocity);
    _stateMachine.AddTrigger (State.FreeFalling, State.CliffHanging, () => _wasInCliffEdge && _isInGround && !IsTouchingCliffIce() && !IsInFrozenWaterfall());
    _stateMachine.AddTrigger (State.CliffHanging, State.ClimbingUp, IsUpArrowPressed);
    _stateMachine.AddTrigger (State.CliffHanging, State.ClimbingDown, () => IsDownArrowPressed() && !IsOneActiveOf (Input.Horizontal));
    _stateMachine.AddTrigger (State.CliffHanging, State.FreeFalling, WasJumpKeyPressed);
    _stateMachine.AddTrigger (State.CliffHanging, State.Traversing, () => IsOneActiveOf (Input.Horizontal));
    _stateMachine.AddTrigger (State.Traversing, State.FreeFalling, () => WasJumpKeyPressed() || !IsInCliffs() && !_isInGround || IsTouchingCliffIce() || IsInFrozenWaterfall());
    _stateMachine.AddTrigger (State.Traversing, State.CliffHanging, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _stateMachine.AddTrigger (State.CliffArresting, State.FreeFalling, WasItemKeyReleased);
    _stateMachine.AddTrigger (State.CliffArresting, State.CliffHanging, () => !IsOnFloor() && !IsMoving() && IsInCliffs());
    _stateMachine.AddTrigger (State.CliffArresting, State.Idle, IsOnFloor);
    _stateMachine.AddTrigger (State.ReadingSign, State.Idle, ()=> IsDownArrowPressed() && !IsUpArrowPressed() && _isInSign);
    _stateMachine.AddTrigger (State.ReadingSign, State.Walking, ()=> IsOneActiveOf (Input.Horizontal) && !IsAnyActiveOf (Input.Vertical) && !IsDepletingEnergy() && _isInSign);
    _stateMachine.AddTrigger (State.ReadingSign, State.Running, ()=> IsOneActiveOf (Input.Horizontal) && !IsAnyActiveOf (Input.Vertical) && IsDepletingEnergy() && _isInSign);
  }

  // @formatter:on
}