using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using static Gravity;
using static Motions;
using static PlayerStateMachine;
using static Positionings;
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
  [Export] public int WalkingSpeed = 20;
  [Export] public int RunningSpeed = 40;
  [Export] public int TraversingSpeed = 200;
  [Export] public int VerticalClimbingSpeed = 200;
  [Export] public int HorizontalFreeFallingSpeed = 10;
  [Export] public int CliffArrestingSpeed = 40;
  [Export] public int CliffArrestingActivationVelocity = 800;

  [Export] public int MaxEnergy = 20;
  [Export] public int EnergyMeterUnits = 20;
  [Export] public int EnergyMeterReplenishTimeSeconds = 10;
  [Export] public int EnergyMeterDepletionTimeSeconds = 3;
  [Export] public int JumpPower = 800;
  [Export] public float HorizontalRunJumpFriction = 0.9f;
  [Export] public float HorizontalRunJumpStoppingFriction = 0.6f;
  [Export] public float TraverseFriction = 0.9f;
  [Export] public float HorizontalClimbStoppingFriction = 0.6f;
  [Export] public float CameraSmoothingDeactivationVelocity = 800.0f;
  [Export] public float VelocityEpsilon = 1.0f;
  [Export] public float Gravity = 30.0f;
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
  private IStateMachine <State> _sm = null!;
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
    StateMachine (delta);
    Collisions();
    _weapon.Update (delta);

    // HorizontalVelocity();
    // VerticalVelocity();
    Animations();
    SoundEffects();
    // if (Mathf.Abs (_velocity.x) < VelocityEpsilon) _velocity.x = 0.0f;
    // if (Mathf.Abs (_velocity.y) < VelocityEpsilon) _velocity.y = 0.0f;
    // _velocity = MoveAndSlide (_velocity, Vector2.Up);
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

    if ((_sm.Is (State.ClimbingUp) || _sm.Is (State.ClimbingDown)) && IsDepletingEnergy() &&
        !Mathf.IsEqualApprox (_energyTimer.WaitTime, _energyMeterDepletionRatePerUnit))
    {
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
    }

    if ((_sm.Is (State.ClimbingUp) || _sm.Is (State.ClimbingDown)) && IsReplenishingEnergy() &&
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
    if (IsDownArrowPressed() && !_sm.Is (State.ReadingSign)) await _dropdown.Drop();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnEnergyTimerTimeout()
  {
    if (!_sm.Is (State.Running) && !IsSpeedClimbing() && _energy == MaxEnergy)
    {
      _energyTimer.Stop();

      return;
    }

    if (IsDepletingEnergy() && (_sm.Is (State.Running) || _sm.Is (State.Jumping) || IsSpeedClimbing()))
    {
      _energy -= 1;
      _energyMeter.Value = _energy;

      return;
    }

    if (_energy == MaxEnergy && (_sm.Is (State.Running) || IsSpeedClimbing())) return;

    _energy += 1;
    _energyMeter.Value = _energy;
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
    if (_sm.Is (State.ClimbingUp)) RestAfterClimbingUp();
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCliffEdgeExited (Node body)
  {
    if (!body.IsInGroup ("Player") || !_sm.Is (State.FreeFalling) || _justRespawned) return;

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
    UpdateVelocity (_sm.ToIf (State.Idle, _sm.Is (State.ReadingSign)));
  }

  private void Respawn()
  {
    _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    StopCameraSmoothing();
    GlobalPosition = new Vector2 (952, -4032);
    _primaryAnimator.Play (IdleLeftAnimation);
    _justRespawned = true;
    _log.Info ("Player respawned.");
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
      _sm.To (State.Idle);

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

  // @formatter:off
  private void StateMachine (float delta) => UpdateVelocity (_sm.Update (this, Godot.Input.IsActionPressed, _velocity, delta));
  private void Collisions() => UpdateVelocity (MoveAndSlide (_velocity, Vector2.Up));
  private void UpdateVelocity (Vector2? velocity) => _velocity = velocity ?? _velocity;
  private bool IsInClimbableLocation() => IsInCliffs || _isInGround;
  private bool CanReadSign() => HasReadableSign() && _isInSign;
  private bool CanClimbPrep() => !CanReadSign() && IsInCliffs;
  private bool CanCliffArrest() => _velocity.y >= CliffArrestingActivationVelocity && IsInCliffs; // TODO Test InClimbingLocation()
  private bool HasReadableSign() => HasReadableSign (GetReadableSignName());
  private bool HasReadableSign (string name) => _signsTileMap?.HasNode ("../" + name) ?? false;
  private string GetReadableSignName() => GetReadableSignName (GetIntersectingTileCell (_animationColliderParent, _primaryAnimator.CurrentAnimation, _signsTileMap));
  private static string GetReadableSignName (Vector2 tileSignCell) => "Readable Sign (" + tileSignCell.x + ", " + tileSignCell.y + ")";
  private Sprite GetReadableSign (string name) => _signsTileMap?.GetNode <Sprite> ("../" + name);
  private bool IsSpeedClimbing() => (_sm.Is (State.ClimbingUp) || _sm.Is (State.ClimbingDown)) && IsEnergyKeyPressed();
  private static int GetClimbingSpeedBoost() => IsEnergyKeyPressed() ? 2 : 1;
  private bool IsDepletingEnergy() => _energy > 0 && IsEnergyKeyPressed();
  private bool JustDepletedAllEnergy() => _energy == 0 && IsEnergyKeyPressed();
  private bool IsReplenishingEnergy() => _energy < MaxEnergy && !IsEnergyKeyPressed();
  private float GetFallingTimeSeconds() => _elapsedFallingTimeMs > 0 ? _elapsedFallingTimeMs / 1000.0f : _lastTotalFallingTimeMs / 1000.0f;
  private void PrintLineOnce (string line) => _printLinesOnce.Add (line);
  private void PrintLineContinuously (string line) => _printLinesContinuously.Add (line);
  private void StopPrintingContinuousLine (string line) => _printLinesContinuously.Remove (line);
  private void ToggleDebuggingText() => _debuggingTextLabel.Visible = !_debuggingTextLabel.Visible;
  private bool IsInCliffs() => _cliffs.Encloses (GetCurrentAnimationColliderRect);
  private bool IsTouchingCliffIce() => _cliffs.IsTouchingIce (GetCurrentAnimationColliderRect);
  private Rect2 GetCurrentAnimationColliderRect => GetColliderRect (_animationColliderParent, GetCurrentAnimationCollider());
  private CollisionShape2D GetCurrentAnimationCollider() => _animationColliderParent.GetNode <CollisionShape2D> (_primaryAnimator.CurrentAnimation);
  // @formatter:on

  private Vector2 MoveHorizontally (Vector2 velocity, float delta) => MoveHorizontally (velocity, true);

  private Vector2 MoveHorizontally (Vector2 velocity, bool flippable)
  {
    var left = IsPressed (Input.Left);
    var right = IsPressed (Input.Right);
    var leftOnly = left && !right;
    var rightOnly = right && !left;
    velocity.x += _currentHorizontalSpeed * (leftOnly ? -1 : rightOnly ? 1 : 0.0f);
    velocity.x *= leftOnly || rightOnly ? HorizontalFriction : HorizontalStoppingFriction;
    if (flippable) FlipHorizontally (rightOnly || !leftOnly && _isFlippedHorizontally);
    if (Mathf.Abs (velocity.x) < VelocityEpsilon) velocity.x = 0;

    return velocity;
  }

  private bool IsTouchingWall() =>
    _wallDetectors.Any (x =>
      x.GetCollider() is StaticBody2D collider && collider.IsInGroup ("Walls") ||
      x.GetCollider() is TileMap collider2 && collider2.IsInGroup ("Walls"));

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

  private void Debugging()
  {
    CalculateDebuggingStats();
    PrintDebuggingText (GetDebuggingText());
  }

  private void CalculateDebuggingStats()
  {
    if (_velocity.y > _highestVerticalVelocity) _highestVerticalVelocity = _velocity.y;
    var physicsBodyData = new PhysicsBodyData (_velocity, GravityType.AfterApplied, GetPositioning (this));

    switch (Motion.Down.IsActive (ref physicsBodyData))
    {
      case true when _fallingStartTimeMs == 0:
        _fallingStartTimeMs = Time.GetTicksMsec();

        break;
      case true:
        _elapsedFallingTimeMs = Time.GetTicksMsec() - _fallingStartTimeMs;

        break;
      case false when _elapsedFallingTimeMs > 0:
        _lastTotalFallingTimeMs = Time.GetTicksMsec() - _fallingStartTimeMs;
        _fallingStartTimeMs = 0;
        _elapsedFallingTimeMs = 0;

        break;
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

  private void SoundEffects()
  {
    if (_sm.Is (State.CliffArresting))
    {
      _audio.PitchScale = CliffArrestingSoundMinPitchScale +
                          _velocity.y / CliffArrestingActivationVelocity / CliffArrestingSoundVelocityPitchScaleModulation;
    }
  }

  // @formatter:off

  private string DumpState() =>
    "\nState: " + _sm.GetState() +
    "\nWeapon state: " + _weapon.GetState() +
    "\nAnimation: " + _primaryAnimator.CurrentAnimation +
    "\nSeason: " + _cliffs.GetCurrentSeason() +
    "\nIdle: " + _sm.Is (State.Idle) +
    "\nWalking: " + _sm.Is (State.Walking) +
    "\nRunning: " + _sm.Is (State.Running) +
    "\nEnergy: " + _energy +
    "\nEnergy timer: " + _energyTimer.TimeLeft +
    "\nJumping: " + _sm.Is (State.Jumping) +
    "\nClimbing prep: " + _sm.Is (State.ClimbingPrep) +
    "\nClimbing prep timer: " + _climbingPrepTimer.TimeLeft +
    "\nClimbing up: " + _sm.Is (State.ClimbingUp) +
    "\nClimbing down: " + _sm.Is (State.ClimbingDown) +
    "\nIsSpeedClimbing: " + IsSpeedClimbing() +
    "\nIsInWaterfall: " +  IsInWaterfall() +
    "\nIsInFrozenWaterfall: " + IsInFrozenWaterfall() +
    "\nIsTouchingCliffIce: " + IsTouchingCliffIce() +
    "\nCliff arresting: " + _sm.Is (State.CliffArresting) +
    "\nCliff hanging: " + _sm.Is (State.CliffHanging) +
    "\nClimbing Traversing: " + _sm.Is (State.Traversing) +
    "\nFree falling: " + _sm.Is (State.FreeFalling) +
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
    "\nMoving: " + Motion.Any.IsActive (ref physicsBodyData) +
    "\nMoving Left: " + Motion.Left.IsActive (ref physicsBodyData) +
    "\nMoving Right: " + Motion.Right.IsActive (ref physicsBodyData) +
    "\nMoving Up: " + Motion.Up.IsActive (ref physicsBodyData) +
    "\nMoving Down: " + Motion.Down.IsActive (ref physicsBodyData) +
    "\nMoving Horizontally: " + Motion.Horizontal.IsActive (ref physicsBodyData) +
    "\nMoving Vertically: " + Motion.Vertical.IsActive (ref physicsBodyData) +
    "\nRight Arrow Pressed: " + IsPressed (Input.Right) +
    "\nLeft Arrow Pressed: " + IsPressed (Input.Left) +
    "\nUp Arrow Pressed: " + IsPressed (Input.Up) +
    "\nDown Arrow Pressed: " + IsPressed (Input.Down) +
    "\nAny Horizontal Arrow Pressed: " + IsPressed (Input.Horizontal) +
    "\nAny Vertical Arrow Pressed: " + IsPressed (Input.Vertical) +
    "\nJump Key Pressed: " + IsPressed (Input.Jump) +
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
    _sm = new PlayerStateMachine (InitialState, LogLevel, Name);
    _sm.OnTransitionFrom (State.ReadingSign, StopReadingSign);
    _sm.OnTransitionTo (State.ReadingSign, ReadSign);
    _sm.OnTransitionTo (State.Traversing, () => _primaryAnimator.Play (TraversingAnimation));
    _sm.OnTransitionTo (State.FreeFalling, () => _primaryAnimator.Play (FreeFallingAnimation));
    _sm.OnTransitionFrom (State.ClimbingPrep, () => _climbingPrepTimer.Stop());
    _sm.OnTransition (State.ReadingSign, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _sm.OnTransition (State.CliffArresting, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _sm.OnTransition (State.ClimbingDown, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _sm.OnTransition (State.FreeFalling, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    // @formatter:on

    _sm.OnTransitionFrom (State.Idle, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
      _cameraSmoothingTimer.Stop();
    });

    _sm.OnTransition (State.Running, State.Idle, () =>
    {
      _primaryAnimator.Play (IdleLeftAnimation);
      _cameraSmoothingTimer.Start();
    });

    _sm.OnTransition (State.Walking, State.Idle, () =>
    {
      _primaryAnimator.Play (IdleLeftAnimation);
      _cameraSmoothingTimer.Start();
    });

    _sm.OnTransitionTo (State.Idle, () =>
    {
      if (!IsInGroup ("Perchable Parent")) AddToGroup ("Perchable Parent");
      _wasRunning = false;
    });

    _sm.OnTransitionFrom (State.CliffHanging, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
    });

    _sm.OnTransitionTo (State.CliffHanging, () =>
    {
      _primaryAnimator.Play (CliffHangingAnimation);
      _weapon.Unequip();
      if (!IsInGroup ("Perchable Parent")) AddToGroup ("Perchable Parent");
    });

    _sm.OnTransitionTo (State.Walking, () =>
    {
      _primaryAnimator.Play (WalkLeftAnimation);
      _wasRunning = false;
    });

    _sm.OnTransitionTo (State.Jumping, () =>
    {
      _primaryAnimator.Play (IdleLeftAnimation);
      _velocity.y -= JumpPower;
    });

    _sm.OnTransitionFrom (State.CliffArresting, () =>
    {
      if (!_audio.Playing) return;

      _audio.Stop();
      StopPrintingContinuousLine ("Sound effects: Playing cliff arresting sound.");
      PrintLineContinuously ("Sound effects: Stopped cliff arresting sound.");
    });

    _sm.OnTransitionTo (State.CliffArresting, () =>
    {
      _primaryAnimator.Play (CliffArrestingAnimation);
      _weapon.Unequip();

      if (_audio.Playing) return;

      _audio.Play();
      StopPrintingContinuousLine ("Sound effects: Stopped cliff arresting sound.");
      PrintLineContinuously ("Sound effects: Playing cliff arresting sound.");
    });

    _sm.OnTransition (State.ClimbingPrep, State.Idle, () =>
    {
      _primaryAnimator.Play (IdleLeftAnimation);
      FlipHorizontally (_wasFlippedHorizontally);
    });

    _sm.OnTransitionTo (State.ClimbingPrep, () =>
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
    _sm.OnTransition (State.Running, State.Jumping, () => { _energyTimer.Start (_energyMeterDepletionRatePerUnit); });

    _sm.OnTransitionFrom (State.Running, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _sm.OnTransitionTo (State.Running, () =>
    {
      _primaryAnimator.Play (RunLeftAnimation);
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
      _wasRunning = true;
    });

    _sm.OnTransitionFrom (State.ClimbingUp, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _sm.OnTransitionFrom (State.ClimbingDown, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _sm.OnTransitionTo (State.ClimbingUp, () =>
    {
      _weapon.Unequip();
      var startFrame = new[] { 1, 6 }[_rng.RandiRange (0, 1)];
      var startTime = startFrame * _primaryAnimator.GetAnimation (ClimbingUpAnimation).Step;
      _primaryAnimator.Play (ClimbingUpAnimation);
      _primaryAnimator.Seek (startTime);
      FlipHorizontally (false);
    });

    _sm.OnTransitionTo (State.ClimbingDown, () =>
    {
      _weapon.Unequip();
      var startFrame = new[] { 3, 8 }[_rng.RandiRange (0, 1)];
      var startTime = startFrame * _primaryAnimator.GetAnimation (ClimbingUpAnimation).Step;
      _primaryAnimator.PlayBackwards (ClimbingUpAnimation);
      _primaryAnimator.Seek (startTime);
      FlipHorizontally (false);
    });

    _sm.OnTransitionFrom (State.FreeFalling, () =>
    {
      _wasInCliffEdge = false;
      _justRespawned = false;
    });

    // @formatter:off

    // TODO Move conditions into state machine conditions, leaving only input for triggers.
    _sm.AddTrigger (State.Idle, State.Walking, () => IsOneActiveOf (Input.Horizontal) && !IsDepletingEnergy() && IsOnFloor());
    _sm.AddTrigger (State.Idle, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsDepletingEnergy() && !IsTouchingWall() && IsOnFloor());
    _sm.AddTrigger (State.Idle, State.Jumping, () => WasJumpKeyPressed() && IsOnFloor());
    _sm.AddTrigger (State.Idle, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs() && !_isInSign && !_isResting);
    _sm.AddTrigger (State.Idle, State.ClimbingDown, () => IsDownArrowPressed() && IsMovingDown() && !IsOnFloor() && IsInCliffs() && !_dropdown.IsDropping() && !IsOneActiveOf (Input.Horizontal));
    _sm.AddTrigger (State.Idle, State.FreeFalling, () => !IsDownArrowPressed() && IsMovingDown() && !IsOnFloor());
    _sm.AddTrigger (State.Idle, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && IsTouchingGround() && HasReadableSign() && !_isResting);
    _sm.AddTrigger (State.Walking, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally() && !(_isInSign && IsUpArrowPressed()));
    _sm.AddTrigger (State.Walking, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsMovingHorizontally() && IsDepletingEnergy());
    _sm.AddTrigger (State.Walking, State.Jumping, WasJumpKeyPressed);
    _sm.AddTrigger (State.Walking, State.FreeFalling, () => IsMovingDown() && !IsOnFloor() && !IsTouchingWall());
    _sm.AddTrigger (State.Walking, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs() && !_isInSign);
    _sm.AddTrigger (State.Walking, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && IsTouchingGround() && HasReadableSign());
    _sm.AddTrigger (State.Running, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _sm.AddTrigger (State.Running, State.Walking, () => IsOneActiveOf (Input.Horizontal) && !IsDepletingEnergy() || JustDepletedAllEnergy());
    _sm.AddTrigger (State.Running, State.Jumping, WasJumpKeyPressed);
    _sm.AddTrigger (State.Running, State.FreeFalling, () => IsMovingDown() && !IsOnFloor() && !IsTouchingWall());
    _sm.AddTrigger (State.Running, State.ClimbingPrep, () => IsUpArrowPressed() && IsInCliffs() && !_isInSign);
    _sm.AddTrigger (State.Running, State.ReadingSign, ()=> IsUpArrowPressed() && _isInSign && IsTouchingGround() && HasReadableSign());
    _sm.AddTrigger (State.Jumping, State.FreeFalling, () => IsMovingDown() && !IsOnFloor());
    _sm.AddTrigger (State.ClimbingPrep, State.Idle, WasUpArrowReleased);
    _sm.AddTrigger (State.ClimbingPrep, State.ClimbingUp, () => IsUpArrowPressed() && _climbingPrepTimer.TimeLeft == 0 && !IsTouchingCliffIce() && !IsInFrozenWaterfall());
    _sm.AddTrigger (State.ClimbingUp, State.FreeFalling, () => (WasJumpKeyPressed() || !IsInCliffs() && !_isInGround || IsTouchingCliffIce() || IsInFrozenWaterfall() || JustDepletedAllEnergy()) && !_isResting);
    _sm.AddTrigger (State.ClimbingUp, State.Idle, () => _isResting);
    _sm.AddTrigger (State.ClimbingUp, State.CliffHanging, () => WasUpArrowReleased() && !IsDownArrowPressed() && (IsInCliffs() || _isInGround));
    _sm.AddTrigger (State.ClimbingUp, State.ClimbingDown, () => WasUpArrowReleased() && IsDownArrowPressed() && (IsInCliffs() || _isInGround));
    _sm.AddTrigger (State.ClimbingDown, State.CliffHanging, () => WasDownArrowReleased() && !IsUpArrowPressed() && (IsInCliffs() || _isInGround));
    _sm.AddTrigger (State.ClimbingDown, State.Idle, IsOnFloor);
    _sm.AddTrigger (State.ClimbingDown, State.ClimbingUp, () => WasDownArrowReleased() && IsUpArrowPressed() && (IsInCliffs() || _isInGround));
    _sm.AddTrigger (State.ClimbingDown, State.FreeFalling, () => WasJumpKeyPressed() || !IsInCliffs() && !_isInGround || IsTouchingCliffIce() || IsInFrozenWaterfall() || _isResting);
    _sm.AddTrigger (State.FreeFalling, State.Idle, () => !IsOneActiveOf (Input.Horizontal) && IsOnFloor() && !IsMovingHorizontally());
    _sm.AddTrigger (State.FreeFalling, State.Walking, () => IsOneActiveOf (Input.Horizontal) && IsOnFloor() && !IsDepletingEnergy() );
    _sm.AddTrigger (State.FreeFalling, State.Running, () => IsOneActiveOf (Input.Horizontal) && IsOnFloor() && IsDepletingEnergy());
    _sm.AddTrigger (State.FreeFalling, State.CliffArresting, () => IsItemKeyPressed() && IsInCliffs() && _velocity.y >= CliffArrestingActivationVelocity);
    _sm.AddTrigger (State.FreeFalling, State.CliffHanging, () => _wasInCliffEdge && _isInGround && !IsTouchingCliffIce() && !IsInFrozenWaterfall());
    _sm.AddTrigger (State.CliffHanging, State.ClimbingUp, IsUpArrowPressed);
    _sm.AddTrigger (State.CliffHanging, State.ClimbingDown, () => IsDownArrowPressed() && !IsOneActiveOf (Input.Horizontal));
    _sm.AddTrigger (State.CliffHanging, State.FreeFalling, WasJumpKeyPressed);
    _sm.AddTrigger (State.CliffHanging, State.Traversing, () => IsOneActiveOf (Input.Horizontal));
    _sm.AddTrigger (State.Traversing, State.FreeFalling, () => WasJumpKeyPressed() || !IsInCliffs() && !_isInGround || IsTouchingCliffIce() || IsInFrozenWaterfall());
    _sm.AddTrigger (State.Traversing, State.CliffHanging, () => !IsOneActiveOf (Input.Horizontal) && !IsMovingHorizontally());
    _sm.AddTrigger (State.CliffArresting, State.FreeFalling, WasItemKeyReleased);
    _sm.AddTrigger (State.CliffArresting, State.CliffHanging, () => !IsOnFloor() && !IsMoving() && IsInCliffs());
    _sm.AddTrigger (State.CliffArresting, State.Idle, IsOnFloor);
    _sm.AddTrigger (State.ReadingSign, State.Idle, ()=> IsDownArrowPressed() && !IsUpArrowPressed() && _isInSign);
    _sm.AddTrigger (State.ReadingSign, State.Walking, ()=> IsOneActiveOf (Input.Horizontal) && !IsAnyActiveOf (Input.Vertical) && !IsDepletingEnergy() && _isInSign);
    _sm.AddTrigger (State.ReadingSign, State.Running, ()=> IsOneActiveOf (Input.Horizontal) && !IsAnyActiveOf (Input.Vertical) && IsDepletingEnergy() && _isInSign);

  }

  // @formatter:on

  private void HorizontalVelocity()
  {
    if (_sm.Is (State.Walking) && IsExclusivelyActiveUnless (Input.Left, _sm.Is (State.Walking)))
    {
      _velocity.x -= WalkingSpeed;
      FlipHorizontally (false);
    }

    if (_sm.Is (State.Running) && IsExclusivelyActiveUnless (Input.Left, _sm.Is (State.Running)))
    {
      _velocity.x -= RunningSpeed;
      FlipHorizontally (false);
    }

    if (_sm.Is (State.Walking) && IsExclusivelyActiveUnless (Input.Right, _sm.Is (State.Walking)))
    {
      _velocity.x += WalkingSpeed;
      FlipHorizontally (true);
    }

    if (_sm.Is (State.Running) && IsExclusivelyActiveUnless (Input.Right, _sm.Is (State.Running)))
    {
      _velocity.x += RunningSpeed;
      FlipHorizontally (true);
    }

    if (_sm.Is (State.Traversing))
    {
      var left = IsLeftArrowPressed();
      var right = IsRightArrowPressed();
      var leftOnly = left && !right;
      var rightOnly = right && !left;
      _velocity.x += TraversingSpeed * (leftOnly ? -1 : rightOnly ? 1 : 0.0f);
      _velocity.x = SafelyClamp (_velocity.x, -TraversingSpeed, TraversingSpeed);
    }

    if (_sm.Is (State.Jumping) && IsExclusivelyActiveUnless (Input.Left, _sm.Is (State.Jumping)))
    {
      _velocity.x -= _wasRunning ? RunningSpeed : WalkingSpeed;
      FlipHorizontally (false);
    }

    if (_sm.Is (State.Jumping) && IsExclusivelyActiveUnless (Input.Right, _sm.Is (State.Jumping)))
    {
      _velocity.x += _wasRunning ? RunningSpeed : WalkingSpeed;
      FlipHorizontally (true);
    }

    if (_sm.Is (State.FreeFalling) && IsExclusivelyActiveUnless (Input.Left, _sm.Is (State.FreeFalling)))
    {
      _velocity.x -= _wasRunning ? RunningSpeed : WalkingSpeed;
      FlipHorizontally (false);
    }

    if (_sm.Is (State.FreeFalling) && IsExclusivelyActiveUnless (Input.Right, _sm.Is (State.FreeFalling)))
    {
      _velocity.x += _wasRunning ? RunningSpeed : WalkingSpeed;
      FlipHorizontally (true);
    }

    // TODO Check if walking/running or jumping
    // TODO Get rid of else if
    // Friction
    if (!IsAnyHorizontalArrowPressed()) _velocity.x *= HorizontalRunJumpStoppingFriction;
    else if (_sm.Is (State.Traversing)) _velocity.x *= TraverseFriction;
    else _velocity.x *= HorizontalRunJumpFriction;
  }

  private void VerticalVelocity()
  {
    _velocity.y += Gravity;

    // TODO For jumping, traversing, and cliffhanging, subtract gravity and round to 0.
    // Makes jumps less high when releasing jump button early.
    // (Holding down jump continuously allows gravity to take over.)
    // @formatter:off
    if (_sm.Is (State.Jumping) && WasJumpKeyReleased() && IsMovingUp()) _velocity.y = 0.0f;
    if (_sm.Is (State.ClimbingUp)) _velocity.y = AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 4 * 0.2f, 0.1f) || AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 9 * 0.2f, 0.1f) ? -VerticalClimbingSpeed * GetClimbingSpeedBoost() : 0.0f;
    if (_sm.Is (State.ClimbingDown)) _velocity.y = AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 6 * 0.2f, 0.1f) || AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 1 * 0.2f, 0.1f) ? VerticalClimbingSpeed * GetClimbingSpeedBoost() : 0.0f;
    if (_sm.Is (State.Traversing)) _velocity.y = 0.0f;
    if (_sm.Is (State.CliffHanging)) _velocity.y = 0.0f;
    // @formatter:on

    // ReSharper disable once InvertIf
    if (_sm.Is (State.CliffArresting))
    {
      _velocity.y -= CliffArrestingSpeed;
      _velocity.y = SafelyClampMin (_velocity.y, 0.0f);
    }
  }

  private bool IsTouchingGround() =>
    _groundDetectors.Any (x =>
      x.GetCollider() is StaticBody2D collider && collider.IsInGroup ("Ground") ||
      x.GetCollider() is TileMap collider2 && collider2.IsInGroup ("Ground"));

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

  // ReSharper disable once UnusedMember.Global
  public void _OnCameraSmoothingTimerTimeout()
  {
    if (IsMoving()) return;

    StartCameraSmoothing();
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
}