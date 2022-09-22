using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using static BooleanOperators;
using static Gravity;
using static Inputs;
using static Motions;
using static PlayerStateMachine;
using static Positionings;
using static Seasons;
using static Tools;
using Input = Inputs.Input;

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
  [Export] public float HorizontalFriction = 0.9f;
  [Export] public float HorizontalStoppingFriction = 0.6f;
  [Export] public float CameraSmoothingDeactivationVelocity = 800.0f;
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
  private int _currentHorizontalSpeed;
  private RichTextLabel _debuggingTextLabel = null!;
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
    _debuggingTextLabel = GetNode <RichTextLabel> ("../UI/Control/Debugging Text");
    _debuggingTextLabel.Visible = false;
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
    Camera();
    _weapon.Update (delta);
    Animations();
    SoundEffects();
    Debugging();
  }

  public override async void _UnhandledInput (InputEvent @event)
  {
    if (WasPressed (Input.Text, @event)) ToggleDebuggingText();
    if (WasPressed (Input.Respawn, @event)) Respawn();
    if (WasMouseLeftClicked (@event)) _clothing.UpdateAll (_primaryAnimator.CurrentAnimation);

    // This async call must be the last line in this method.
    if (WasPressed (Input.Down) && !_sm.Is (State.ReadingSign)) await _dropdown.Drop();
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
    var physicsBodyData = new PhysicsBodyData (_velocity, GravityType.None, null);

    if (Motion.Any.IsActive (ref physicsBodyData)) return;

    StartCameraSmoothing();
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
  private void StateMachine (float delta) => UpdateVelocity (_sm.Update (this, x => Godot.Input.IsActionPressed (x), _velocity, delta));
  private void Collisions() => UpdateVelocity (MoveAndSlide (_velocity, Vector2.Up));
  private void UpdateVelocity (Vector2? velocity) => _velocity = velocity ?? _velocity;
  private Vector2 MoveHorizontally (Vector2 velocity, float delta) => MoveHorizontally (velocity, true);
  private bool IsInClimbableLocation() => IsInCliffs() || _isInGround;
  private bool CanReadSign() => _isInSign && HasReadableSign();
  private bool CanClimbPrep() => !CanReadSign() && IsInCliffs();
  private bool CanCliffArrest() => _velocity.y >= CliffArrestingActivationVelocity && IsInCliffs(); // TODO Test InClimbingLocation()
  private bool HasReadableSign() => HasReadableSign (GetReadableSignName());
  private bool HasReadableSign (string name) => _signsTileMap?.HasNode ("../" + name) ?? false;
  private string GetReadableSignName() => GetReadableSignName (GetIntersectingTileCell (_animationColliderParent, _primaryAnimator.CurrentAnimation, _signsTileMap));
  private static string GetReadableSignName (Vector2 tileSignCell) => "Readable Sign (" + tileSignCell.x + ", " + tileSignCell.y + ")";
  private Sprite GetReadableSign (string name) => _signsTileMap?.GetNode <Sprite> ("../" + name);
  private bool IsSpeedClimbing() => (_sm.Is (State.ClimbingUp) || _sm.Is (State.ClimbingDown)) && IsPressed (Input.Energy);
  private static int GetClimbingSpeedBoost() => IsPressed (Input.Energy) ? 2 : 1;
  private bool IsDepletingEnergy() => _energy > 0 && IsPressed (Input.Energy);
  private bool JustDepletedAllEnergy() => _energy == 0 && IsPressed (Input.Energy);
  private bool IsReplenishingEnergy() => _energy < MaxEnergy && !IsPressed (Input.Energy);
  private float GetFallingTimeSeconds() => _elapsedFallingTimeMs > 0 ? _elapsedFallingTimeMs / 1000.0f : _lastTotalFallingTimeMs / 1000.0f;
  private void PrintLineOnce (string line) => _printLinesOnce.Add (line);
  private void PrintLineContinuously (string line) => _printLinesContinuously.Add (line);
  private void StopPrintingContinuousLine (string line) => _printLinesContinuously.Remove (line);
  private void ToggleDebuggingText() => _debuggingTextLabel.Visible = !_debuggingTextLabel.Visible;
  private bool IsInCliffs() => _cliffs.Encloses (GetCurrentAnimationColliderRect);
  private bool IsTouchingWall() => _wallDetectors.Any (x => x.GetCollider() is StaticBody2D collider && collider.IsInGroup ("Walls") || x.GetCollider() is TileMap collider2 && collider2.IsInGroup ("Walls"));
  private bool IsTouchingGround() => _groundDetectors.Any (x => x.GetCollider() is StaticBody2D collider && collider.IsInGroup ("Ground") || x.GetCollider() is TileMap collider2 && collider2.IsInGroup ("Ground"));
  private bool IsTouchingCliffIce() => _cliffs.IsTouchingIce (GetCurrentAnimationColliderRect);
  private Rect2 GetCurrentAnimationColliderRect => GetColliderRect (_animationColliderParent, GetCurrentAnimationCollider());
  private CollisionShape2D GetCurrentAnimationCollider() => _animationColliderParent.GetNode <CollisionShape2D> (_primaryAnimator.CurrentAnimation);
  // @formatter:on

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

  private void FlipHorizontally (bool flip)
  {
    if (_isFlippedHorizontally == flip) return;

    _isFlippedHorizontally = flip;
    var scale = Scale;
    scale.x = -scale.x;
    Scale = scale;
  }

  private void Camera()
  {
    if (!AreAlmostEqual (_camera.GetCameraScreenCenter().x, GlobalPosition.x, 1.0f) &&
        !(Mathf.Abs (_velocity.x) >= CameraSmoothingDeactivationVelocity) &&
        !(Mathf.Abs (_velocity.y) >= CameraSmoothingDeactivationVelocity)) return;

    StopCameraSmoothing();
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
    if (_sm.Is (State.CliffArresting))
    {
      _audio.PitchScale = CliffArrestingSoundMinPitchScale +
                          _velocity.y / CliffArrestingActivationVelocity / CliffArrestingSoundVelocityPitchScaleModulation;
    }
  }

  private void Debugging()
  {
    CalculateDebuggingStats();
    var physicsBodyData = new PhysicsBodyData (_velocity, GravityType.AfterApplied, GetPositioning (this));
    PrintLineOnce (DumpState (ref physicsBodyData));
    Print();
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
    _debuggingTextLabel.Text = "";
    _debuggingTextLabel.BbcodeText = "";
    _debuggingTextLabel.GetVScroll().Visible = false;
    foreach (var line in _printLinesContinuously) _debuggingTextLabel.AddText (line + "\n");
    foreach (var line in _printLinesOnce) _debuggingTextLabel.AddText (line + "\n");
    _printLinesOnce.Clear();
  }

  // @formatter:off

  private string DumpState (ref PhysicsBodyData physicsBodyData) =>
    "\nState: " + _sm.GetState() +
    "\nWeapon state: " + _weapon.GetState() +
    "\nAnimation: " + _primaryAnimator.CurrentAnimation +
    "\nSeason: " + _cliffs.GetCurrentSeason() +
    "\nIdle: " + _sm.Is (State.Idle) +
    "\nWalking: " + _sm.Is (State.Walking) +
    "\nRunning: " + _sm.Is (State.Running) +
    "\nJumping: " + _sm.Is (State.Jumping) +
    "\nClimbing prep: " + _sm.Is (State.ClimbingPrep) +
    "\nClimbing up: " + _sm.Is (State.ClimbingUp) +
    "\nClimbing down: " + _sm.Is (State.ClimbingDown) +
    "\nTraversing: " + _sm.Is (State.Traversing) +
    "\nCliff arresting: " + _sm.Is (State.CliffArresting) +
    "\nCliff hanging: " + _sm.Is (State.CliffHanging) +
    "\nFree falling: " + _sm.Is (State.FreeFalling) +
    "\nDropping down: " + _dropdown.IsDropping() +
    "\nEnergy: " + _energy +
    "\nEnergy Timer: " + _energyTimer.TimeLeft +
    "\nDepleting Energy: " + IsDepletingEnergy()  +
    "\nReplenishing Energy: " + IsReplenishingEnergy() +
    "\nClimbing Prep Timer: " + _climbingPrepTimer.TimeLeft +
    "\nCan Climb Prep: " + CanClimbPrep() +
    "\nSpeed Climbing: " + IsSpeedClimbing() +
    "\nCan Cliff Arrest: " + CanCliffArrest() +
    "\nIn Cliff Ice: " + IsTouchingCliffIce() +
    "\nOn Floor: " + IsOnFloor() +
    "\nTouching Ground: " + IsTouchingGround() +
    "\nTouching Wall: " + IsTouchingWall() +
    "\nIn Sign: " + _isInSign +
    "\nIn Cliffs: " + IsInCliffs() +
    "\nIn Ground: " + _isInGround +
    "\nIn Waterfall: " +  IsInWaterfall() +
    "\nIn FrozenWaterfall: " + IsInFrozenWaterfall() +
    "\nIn Climbable Location: " + IsInClimbableLocation() +
    "\nCan Read Sign: " + CanReadSign() +
    "\nWas In Cliff Edge: " + _wasInCliffEdge +
    "\nJust Respawned: " + _justRespawned +
    "\nResting: " + _isResting +
    "\nWas Running: " + _wasRunning +
    "\nHorizontal Speed: " + _currentHorizontalSpeed +
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
    "\nFlipped Horizontally: " + _isFlippedHorizontally +
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
    // ReSharper disable once ExplicitCallerInfoArgument
    _sm = new PlayerStateMachine (InitialState, LogLevel, Name);
    _sm.OnTransitionFrom (State.ReadingSign, StopReadingSign);
    _sm.OnTransitionTo (State.ReadingSign, ReadSign);
    _sm.OnTransitionFrom (State.ClimbingPrep, () => _climbingPrepTimer.Stop());
    _sm.OnTransitionTo (State.FreeFalling, () => _primaryAnimator.Play (FreeFallingAnimation));
    _sm.OnTransition (State.ReadingSign, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _sm.OnTransition (State.CliffArresting, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _sm.OnTransition (State.ClimbingDown, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));
    _sm.OnTransition (State.FreeFalling, State.Idle, () => _primaryAnimator.Play (IdleLeftAnimation));

    _sm.OnTransitionTo (State.Traversing, () =>
    {
      _primaryAnimator.Play (TraversingAnimation);
      _currentHorizontalSpeed = TraversingSpeed;
    });

    _sm.OnTransitionFrom (State.Idle, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
      _cameraSmoothingTimer.Stop();
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
      _currentHorizontalSpeed = WalkingSpeed;
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
    _sm.OnTransition (State.Running, State.Jumping, () =>
    {
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
      _currentHorizontalSpeed = RunningSpeed;
    });

    _sm.OnTransitionFrom (State.Running, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _sm.OnTransitionTo (State.Running, () =>
    {
      _primaryAnimator.Play (RunLeftAnimation);
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
      _currentHorizontalSpeed = RunningSpeed;
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

    _sm.AddFrameAction (State.Idle);
    _sm.AddFrameAction (State.FreeFalling, velocityAction: MoveHorizontally);
    _sm.AddFrameAction (State.Walking, velocityAction: MoveHorizontally);
    _sm.AddFrameAction (State.Running, velocityAction: MoveHorizontally);
    _sm.AddFrameAction (State.CliffHanging, GravityType.None);

    _sm.AddFrameAction (State.ClimbingDown, GravityType.None, velocityAction: (velocity, _) =>
    {
      velocity.y = AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 6 * 0.2f, 0.1f) ||
                   AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 1 * 0.2f, 0.1f)
        ? VerticalClimbingSpeed * GetClimbingSpeedBoost()
        : 0.0f;

      return velocity;
    });

    _sm.AddFrameAction (State.Jumping, GravityType.AfterApplied, velocityAction: (velocity, delta) =>
    {
      MoveHorizontally (velocity, delta);
      var physicsBodyData = new PhysicsBodyData (velocity, GravityType.AfterApplied, GetPositioning (this));
      if (WasReleased (Input.Jump) && Motion.Up.IsActive (ref physicsBodyData)) velocity.y = 0;

      return velocity;
    });

    _sm.AddFrameAction (State.Traversing, GravityType.None, velocityAction: (velocity, _) => MoveHorizontally (velocity, false));

    _sm.AddFrameAction (State.ClimbingUp, GravityType.None, velocityAction: (velocity, _) =>
    {
      velocity.y = AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 4 * 0.2f, 0.1f) ||
                   AreAlmostEqual (_primaryAnimator.CurrentAnimationPosition, 9 * 0.2f, 0.1f)
        ? -VerticalClimbingSpeed * GetClimbingSpeedBoost()
        : 0.0f;

      var isTimerDepleting = Mathf.IsEqualApprox (_energyTimer.WaitTime, _energyMeterDepletionRatePerUnit);
      var isTimerReplenishing = Mathf.IsEqualApprox (_energyTimer.WaitTime, _energyMeterReplenishRatePerUnit);
      if (IsDepletingEnergy() && !isTimerDepleting) _energyTimer.Start (_energyMeterDepletionRatePerUnit);
      if (IsReplenishingEnergy() && !isTimerReplenishing) _energyTimer.Start (_energyMeterReplenishRatePerUnit);

      return velocity;
    });

    _sm.AddFrameAction (State.CliffArresting, GravityType.BeforeApplied, velocityAction: (velocity, _) =>
    {
      velocity.y -= CliffArrestingSpeed;
      velocity.y = SafelyClampMin (velocity.y, 0.0f);

      _audio.PitchScale = CliffArrestingSoundMinPitchScale +
                          velocity.y / CliffArrestingActivationVelocity / CliffArrestingSoundVelocityPitchScaleModulation;

      return velocity;
    });

    // @formatter:off

    // TODO Add a Not boolean operator, or try and combine conditions into a new method that doesn't require it.
    // TODO Try to convert custom conditions into states, such as resting, dropping down.
    // TODO Replace optionalMotion with motion: Optional (Motion.Horizontal)
    // TODO Change InClimbingLocation, !IsInCliffIce, to Positioning.Climbable
    // TODO If a trigger has no input condition, it should allow any input, no input should only be for Input.None. Same for motion.

    _sm.AddTrigger (State.Idle, State.Walking, Input.Horizontal, positioning: Positioning.Ground, conditions: _ (And (() => !IsDepletingEnergy(), () => !_dropdown.IsDropping())));
    _sm.AddTrigger (State.Idle, State.Running, inputs: _ (Required (Input.Horizontal, Input.Energy)), positioning: Positioning.Ground, conditions: _ (And (IsDepletingEnergy, () => !IsTouchingWall(), () => !_dropdown.IsDropping())));
    _sm.AddTrigger (State.Idle, State.Jumping, inputs: _ (Required (Input.Jump), Optional (Input.Horizontal)), positioning: Positioning.Ground, and: () => !_dropdown.IsDropping());
    _sm.AddTrigger (State.Idle, State.ClimbingPrep, Input.Up, positioning: Positioning.Ground, conditions: _ (And (CanClimbPrep, () => !IsDepletingEnergy(), () => !_dropdown.IsDropping(), () => !_isResting)));
    _sm.AddTrigger (State.Idle, State.ClimbingDown, Input.Down, motion: Motion.Down, positioning: Positioning.Air, conditions: _ (And (IsInCliffs, () => !_dropdown.IsDropping())));

    // TODO Try to eliminate custom input conditions.
    _sm.AddTrigger (State.Idle, State.FreeFalling, motion: Motion.Down, positioning: Positioning.Air, conditions: _ (And (() => !IsPressed (Input.Down), () => !WasPressed (Input.Jump)), Or (() => _dropdown.IsDropping())));

    _sm.AddTrigger (State.Idle, State.ReadingSign, Input.Up, positioning: Positioning.Ground, conditions: _ (And (CanReadSign, () => !_dropdown.IsDropping(), () => !_isResting)));
    _sm.AddTrigger (State.Walking, State.Idle, motion: Motion.None, positioning: Positioning.Ground);
    _sm.AddTrigger (State.Walking, State.Running, inputs: _ (Required (Input.Horizontal, Input.Energy)), motion: Motion.Horizontal, positioning: Positioning.Ground, and: IsDepletingEnergy);
    _sm.AddTrigger (State.Walking, State.Jumping, inputs: _ (Required (Input.Jump), Optional (Input.Horizontal)), positioning: Positioning.Ground);

    // TODO Try to eliminate custom input conditions.
    _sm.AddTrigger (State.Walking, State.FreeFalling, motion: Motion.Down, positioning: Positioning.Air, conditions: _(And (() => !WasPressed (Input.Jump), () => !IsTouchingWall())));

    _sm.AddTrigger (State.Walking, State.ClimbingPrep, Input.Up, motion: Motion.None, positioning: Positioning.Ground, and: CanClimbPrep);
    _sm.AddTrigger (State.Walking, State.ReadingSign, Input.Up, motion: Motion.None, positioning: Positioning.Ground, and: CanReadSign);
    _sm.AddTrigger (State.Running, State.Idle, Input.None, motion: Motion.None, positioning: Positioning.Ground);
    _sm.AddTrigger (State.Running, State.Walking, Input.Horizontal, motion: Motion.Horizontal, positioning: Positioning.Ground, conditions: _ (And (() => !IsDepletingEnergy()), Or (JustDepletedAllEnergy)));

    // TODO Testing
    _sm.AddTrigger (State.Running, State.Jumping, inputs: _ (Required (Input.Jump, Input.Left)), positioning: Positioning.Ground, motion: Motion.Left);

    _sm.AddTrigger (State.Running, State.FreeFalling, motion: Motion.Down, positioning: Positioning.Air, and: () => !IsTouchingWall());
    _sm.AddTrigger (State.Running, State.ClimbingPrep, Input.Up, motion: Motion.None, positioning: Positioning.Ground, and: CanClimbPrep);
    _sm.AddTrigger (State.Running, State.ReadingSign, Input.Up, motion: Motion.None, positioning: Positioning.Ground, and: CanReadSign);

    // TODO Testing
    _sm.AddTrigger (State.Jumping, State.FreeFalling, motions: _ (Required (Motion.Down), Optional (Motion.Horizontal)), positioning: Positioning.Air); // TODO Test with input condition.

    _sm.AddTrigger (State.ClimbingPrep, State.Idle, Input.None, motion: Motion.None, positioning: Positioning.Ground);

    // TODO Test with positioning.
    _sm.AddTrigger (State.ClimbingPrep, State.ClimbingUp, Input.Up, conditions: _ (And (() => _climbingPrepTimer.TimeLeft == 0, () => !IsTouchingCliffIce())));
    _sm.AddTrigger (State.ClimbingUp, State.Idle, condition: () => _isResting); // TODO Test more strict conditions.

    _sm.AddTrigger (State.ClimbingUp, State.FreeFalling, Input.Jump, positioning: Positioning.Air, conditions: _ (Or (() => !IsInClimbableLocation(), IsTouchingCliffIce, JustDepletedAllEnergy), And (() => !_isResting)));

    // TODO Testing.
    _sm.AddTrigger (State.ClimbingUp, State.CliffHanging, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.ClimbingDown, State.CliffHanging, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);
    // _sm.AddTrigger (State.ClimbingUp, State.ClimbingDown, Input.Down, positioning: Positioning.Air, and: IsInClimbableLocation);

    _sm.AddTrigger (State.ClimbingDown, State.Idle, positioning: Positioning.Ground); // TODO Test more strict conditions.
    _sm.AddTrigger (State.ClimbingDown, State.ClimbingUp, Input.Up, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.ClimbingDown, State.FreeFalling, Input.Jump, positioning: Positioning.Air, conditions: _ (Or (() => !IsInClimbableLocation(), IsTouchingCliffIce, () => _isResting)));
    _sm.AddTrigger (State.FreeFalling, State.Idle, Input.None, motion: Motion.None, positioning: Positioning.Ground);
    _sm.AddTrigger (State.FreeFalling, State.Walking, Input.Horizontal, motion: Motion.Horizontal, positioning: Positioning.Ground, and: () => !IsDepletingEnergy());
    _sm.AddTrigger (State.FreeFalling, State.Running, Input.Horizontal, motion: Motion.Horizontal, positioning: Positioning.Ground, and: IsDepletingEnergy);
    _sm.AddTrigger (State.FreeFalling, State.CliffArresting, Input.Item, positioning: Positioning.Air, and: CanCliffArrest);
    _sm.AddTrigger (State.FreeFalling, State.CliffHanging, motion: Motion.Down, positioning: Positioning.Air, conditions: _ (And (() => _wasInCliffEdge, IsInClimbableLocation)));
    _sm.AddTrigger (State.FreeFalling, State.Traversing, motion: Motion.Horizontal, positioning: Positioning.Air, conditions: _ (And (() => _wasInCliffEdge, IsInClimbableLocation)));

    // TODO Testing.
    _sm.AddTrigger (State.CliffHanging, State.ClimbingUp, Input.Up, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);

    _sm.AddTrigger (State.CliffHanging, State.ClimbingDown, Input.Down, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.CliffHanging, State.FreeFalling, Input.Jump, positioning: Positioning.Air); // TODO Test more strict conditions.
    _sm.AddTrigger (State.CliffHanging, State.Traversing, Input.Horizontal, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.Traversing, State.FreeFalling, inputs: _ (Required (Input.Jump), Optional (Input.Horizontal)), positioning: Positioning.Air, conditions: _ (And (() => !IsInClimbableLocation()), Or (IsTouchingCliffIce)));
    _sm.AddTrigger (State.Traversing, State.CliffHanging, inputs: _ (Optional (Required (Input.Left, Input.Right))), motion: Motion.None, positioning: Positioning.Air, conditions: _ (And (IsInClimbableLocation, () => !IsTouchingCliffIce())));

    _sm.AddTrigger (State.CliffArresting, State.FreeFalling, Input.None, motions: _ (Required (Motion.Down), Optional (Motion.Horizontal)), positioning: Positioning.Air);

    _sm.AddTrigger (State.CliffArresting, State.CliffHanging, Input.Item, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.CliffArresting, State.Traversing, inputs: _ (Required (Input.Item), Optional (Input.Horizontal)), motion: Motion.Horizontal, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.CliffArresting, State.Idle, positioning: Positioning.Ground); // TODO Test more strict conditions.
    _sm.AddTrigger (State.ReadingSign, State.Idle, Input.Down, motion: Motion.None, positioning: Positioning.Ground);
    _sm.AddTrigger (State.ReadingSign, State.Walking, Input.Horizontal, motion: Motion.Horizontal, positioning: Positioning.Ground);
    _sm.AddTrigger (State.ReadingSign, State.Running, Input.Horizontal, motion: Motion.Horizontal, positioning: Positioning.Ground, and: IsDepletingEnergy);
  }

  // @formatter:on
}