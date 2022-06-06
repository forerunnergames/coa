using System.Collections.Generic;
using Godot;
using static BooleanOperators;
using static Gravity;
using static Inputs;
using static Motions;
using static Positionings;
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
// Cliff arresting: Using pickaxe to slow a cliff fall
// Cliffhanging: Attached to a cliff above the ground, not moving
// Traversing: Climbing horizontally
// Free falling: Moving down, without cliff arresting, can also be coming down from a jump.
public class Player : KinematicBody2D
{
  [Export] public int WalkingSpeed = 20;
  [Export] public int RunningSpeed = 40;
  [Export] public int TraversingSpeed = 100;
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
  [Export] public string IdleLeftAnimation = "player_idle_left";
  [Export] public string IdleBackAnimation = "player_idle_back";
  [Export] public string ClimbingPrepAnimation = "player_idle_back";
  [Export] public string FreeFallingAnimation = "player_falling";
  [Export] public string ClimbingUpAnimation = "player_climbing_up";
  [Export] public string ClimbingDownAnimation = "player_climbing_down";
  [Export] public string CliffHangingAnimation = "player_cliff_hanging";
  [Export] public string TraversingAnimation = "player_traversing";
  [Export] public string CliffArrestingAnimation = "player_cliff_arresting";
  [Export] public string WalkingAnimation = "player_walking";
  [Export] public string RunningAnimation = "player_running";
  [Export] public string CliffArrestingSoundFile = "res://assets/sounds/cliff_arresting.wav";
  [Export] public bool CliffArrestingSoundLooping = true;
  [Export] public float CliffArrestingSoundLoopBeginSeconds = 0.5f;
  [Export] public float CliffArrestingSoundLoopEndSeconds = 3.8f;
  [Export] public float CliffArrestingSoundVelocityPitchScaleModulation = 4.0f;
  [Export] public float CliffArrestingSoundMinPitchScale = 2.0f;
  [Export] public float ClimbingUpToNewLevelRestTimeSeconds = 1.0f;
  [Export] public State InitialState = State.Idle;

  [Export]
  public Log.Level LogLevel
  {
    get => _logLevel;
    set
    {
      _logLevel = value;
      if (_log != null) _log.CurrentLevel = _logLevel;
      _sm?.SetLogLevel (_logLevel);
    }
  }

  // Field must be publicly accessible from Cliffs.cs
  public bool IsInCliffs;

  // Field must be publicly accessible from Cliffs.cs
  public bool IsInCliffIce;

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
  private RichTextLabel _debuggingTextLabel = null!;
  private AnimatedSprite _sprite = null!;
  private Area2D _area = null!;
  private TextureProgress _energyMeter = null!;
  private AudioStreamPlayer _audio = null!;
  private Timer _energyTimer = null!;
  private Timer _climbingPrepTimer = null!;
  private int _energy;
  private int _currentHorizontalSpeed;
  private float _energyMeterReplenishRatePerUnit;
  private float _energyMeterDepletionRatePerUnit;
  private bool _isFlippedHorizontally;
  private bool _wasFlippedHorizontally;
  private IStateMachine <State> _sm = null!;
  private uint _fallingStartTimeMs;
  private uint _elapsedFallingTimeMs;
  private uint _lastTotalFallingTimeMs;
  private float _highestVerticalVelocity;
  private bool _isInGround;
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
  private Log.Level _logLevel = Log.Level.All;
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
    { State.CliffArresting, new[] { State.CliffHanging, State.Traversing, State.FreeFalling, State.Idle }},
    { State.FreeFalling, new[] { State.CliffArresting, State.CliffHanging, State.Traversing, State.Idle, State.Walking, State.Running, State.Jumping }},
    { State.ReadingSign, new[] { State.Idle, State.Walking, State.Running }}
  };

  // @formatter:on

  public override void _Ready()
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (Name) { CurrentLevel = LogLevel };
    _camera = GetNode <Camera2D> ("Camera2D");
    _rayChest = GetNode <RayCast2D> ("Chest");
    _rayFeet = GetNode <RayCast2D> ("Feet");
    _cliffs = GetNode <Cliffs> ("../Cliffs");
    _waterfall = _cliffs.GetNode <Area2D> ("Waterfall");
    _audio = GetNode <AudioStreamPlayer> ("PlayerSoundEffectsPlayer");
    _audio.Stream = ResourceLoader.Load <AudioStream> (CliffArrestingSoundFile);
    _debuggingTextLabel = GetNode <RichTextLabel> ("../UI/Control/Debugging Text");
    _debuggingTextLabel.Visible = false;
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
    StateMachine (delta);
    Collisions();
    Animations();
    Debugging();
  }

  public override void _UnhandledInput (InputEvent @event)
  {
    if (WasPressed (Input.Respawn, @event)) Respawn();
    if (WasPressed (Input.Down, @event)) CheckDropDownThrough();
    if (WasPressed (Input.Text, @event)) ToggleDebuggingText();
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

    _log.Debug ($"Player entered ground.");
    _isInGround = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnGroundExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Debug ($"Player exited ground.");
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
    UpdateVelocity (_sm.ToIf (State.Idle, _sm.Is (State.ReadingSign)));
  }

  private void Respawn()
  {
    _sm.Reset (IStateMachine <State>.ResetOption.ExecuteTransitionActions);
    GlobalPosition = new Vector2 (952, -4032);
    _sprite.Animation = IdleLeftAnimation;
    _justRespawned = true;
  }

  private void CheckDropDownThrough()
  {
    for (var i = 0; i < GetSlideCount(); ++i)
    {
      var collision = GetSlideCollision (i);

      if (_sm.Is (State.ReadingSign) || !WasPressed (Input.Down) || collision.Collider is not Node2D node ||
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
    if (_readableSign == null || !HasReadableSign()) return;

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
  private string GetReadableSignName() => _signsTileMap != null ? GetReadableSignName (GetTileCellAtCenterOf (_area, _sprite.Animation, _signsTileMap)) : GetReadableSignName (Vector2.Zero);
  private static string GetReadableSignName (Vector2 tileSignCell) => "Readable Sign (" + tileSignCell.x + ", " + tileSignCell.y + ")";
  private Sprite GetReadableSign (string name) => _signsTileMap?.GetNode <Sprite> ("../" + name);
  private bool IsSpeedClimbing() => _sm.Is (State.ClimbingUp) && IsPressed (Input.Energy);
  private static int GetClimbingSpeedBoost() => IsPressed (Input.Energy) ? 2 : 1;
  private bool IsDepletingEnergy() => _energy > 0 && IsPressed (Input.Energy);
  private bool JustDepletedAllEnergy() => _energy == 0 && IsPressed (Input.Energy);
  private bool IsReplenishingEnergy() => _energy < MaxEnergy && !IsPressed (Input.Energy);
  private void ToggleDebuggingText() => _debuggingTextLabel.Visible = !_debuggingTextLabel.Visible;
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
    _waterfall.SetBlockSignals (false);
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
        _fallingStartTimeMs = OS.GetTicksMsec();

        break;
      case true:
        _elapsedFallingTimeMs = OS.GetTicksMsec() - _fallingStartTimeMs;

        break;
      case false when _elapsedFallingTimeMs > 0:
        _lastTotalFallingTimeMs = OS.GetTicksMsec() - _fallingStartTimeMs;
        _fallingStartTimeMs = 0;
        _elapsedFallingTimeMs = 0;

        break;
    }
  }

  private void PrintDebuggingText (string text)
  {
    _debuggingTextLabel.Text = "";
    _debuggingTextLabel.BbcodeText = "";
    _debuggingTextLabel.AddText (text);
  }

  // @formatter:off

  private string GetDebuggingText()
  {
    var physicsBodyData = new PhysicsBodyData (_velocity, GravityType.AfterApplied, GetPositioning (this));

    return
    "\nState: " + _sm.GetState() +
    "\nAnimation: " + _sprite.Animation +
    "\nSeason: " + _cliffs.CurrentSeason +
    "\nEnergy: " + _energy +
    "\nEnergy Timer: " + _energyTimer.TimeLeft +
    "\nDepleting Energy: " + IsDepletingEnergy()  +
    "\nReplenishing Energy: " + IsReplenishingEnergy() +
    "\nClimbing Prep Timer: " + _climbingPrepTimer.TimeLeft +
    "\nCan Climb Prep: " + CanClimbPrep() +
    "\nSpeed Climbing: " + IsSpeedClimbing() +
    "\nCan Cliff Arrest: " + CanCliffArrest() +
    "\nIn Cliff Ice: " + IsInCliffIce +
    "\nOn Floor: " + IsOnFloor() +
    "\nHitting Wall: " + IsHittingWall() +
    "\nIn Cliffs: " + IsInCliffs +
    "\nIn Ground: " + _isInGround +
    "\nIn Climbable Location: " + IsInClimbableLocation() +
    "\nIn Sign: " + _isInSign +
    "\nCan Read Sign: " + CanReadSign() +
    "\nResting: " + _isResting +
    "\nHorizontal Speed: " + _currentHorizontalSpeed +
    "\nWas In Cliff Edge: " + _wasInCliffEdge +
    "\nDropping Down: " + _isDroppingDown +
    "\nJust Respawned: " + _justRespawned +
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
    "\nPosition: " + Position +
    "\nScale: " + Scale +
    "\nVelocity: " + _velocity +
    "\nVertical velocity (mph): " + _velocity.y * 0.028334573333333 +
    "\nHighest Vertical Velocity: " + _highestVerticalVelocity +
    "\nHighest Vertical Velocity (mph): " + _highestVerticalVelocity * 0.028334573333333 +
    "\nFalling Time (sec): " + (_elapsedFallingTimeMs > 0 ? _elapsedFallingTimeMs / 1000.0f : _lastTotalFallingTimeMs / 1000.0f);
  }

  // @formatter:on

  private void InitializeStateMachine()
  {
    _sm = new StateMachine <State> (TransitionTable, InitialState, LogLevel);
    _sm.OnTransitionFrom (State.ReadingSign, StopReadingSign);
    _sm.OnTransitionTo (State.ReadingSign, ReadSign);
    _sm.OnTransitionFrom (State.ClimbingPrep, () => _climbingPrepTimer.Stop());
    _sm.OnTransitionTo (State.FreeFalling, () => _sprite.Animation = FreeFallingAnimation);

    _sm.OnTransitionTo (State.Traversing, () =>
    {
      _sprite.Animation = TraversingAnimation;
      _currentHorizontalSpeed = TraversingSpeed;
    });

    _sm.OnTransitionFrom (State.Idle, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
      if (_sprite.IsInGroup ("Perchable")) _sprite.RemoveFromGroup ("Perchable");
    });

    _sm.OnTransitionTo (State.Idle, () =>
    {
      _sprite.Animation = IdleLeftAnimation;
      if (!IsInGroup ("Perchable Parent")) AddToGroup ("Perchable Parent");
      if (!_sprite.IsInGroup ("Perchable")) _sprite.AddToGroup ("Perchable");
    });

    _sm.OnTransitionFrom (State.CliffHanging, () =>
    {
      if (IsInGroup ("Perchable Parent")) RemoveFromGroup ("Perchable Parent");
      if (_sprite.IsInGroup ("Perchable")) _sprite.RemoveFromGroup ("Perchable");
    });

    _sm.OnTransitionTo (State.CliffHanging, () =>
    {
      _sprite.Animation = CliffHangingAnimation;
      FlipHorizontally (false);
      if (!IsInGroup ("Perchable Parent")) AddToGroup ("Perchable Parent");
      if (!_sprite.IsInGroup ("Perchable")) _sprite.AddToGroup ("Perchable");
    });

    _sm.OnTransitionTo (State.Walking, () =>
    {
      _sprite.Animation = WalkingAnimation;
      _currentHorizontalSpeed = WalkingSpeed;
    });

    _sm.OnTransitionTo (State.Jumping, velocityAction: () =>
    {
      _velocity.y -= JumpPower;
      _sprite.Animation = IdleLeftAnimation;

      return _velocity;
    });

    _sm.OnTransitionFrom (State.CliffArresting, () => _audio.Stop());

    _sm.OnTransitionTo (State.CliffArresting, () =>
    {
      _sprite.Animation = CliffArrestingAnimation;

      if (_audio.Playing) return;

      _audio.Play();
    });

    _sm.OnTransition (State.ClimbingPrep, State.Idle, () => FlipHorizontally (_wasFlippedHorizontally));

    _sm.OnTransitionTo (State.ClimbingPrep, () =>
    {
      _sprite.Animation = ClimbingPrepAnimation;
      _wasFlippedHorizontally = _isFlippedHorizontally;
      FlipHorizontally (false);
      _climbingPrepTimer.Start();
    });

    // TODO Add state machine method OnTransitionExceptFrom (State.ReadingSign, State.Idle, () => _sprite.Animation = IdleLeftAnimation);
    // TODO This eliminates transition repetition when an exception is needed (all transitions to a state, except from a specific state).
    // TODO Catch-all OnTransitionFrom (State.Running) replenishes the energy meter before the running => jumping transition has a chance to start depleting it.
    // TODO OnTransitionFromExcept (State.Running, State.Jumping, () => _currentHorizontalSpeed = WalkingSpeed)
    // TODO Running and jumping uses energy energy meter while jumping.
    _sm.OnTransition (State.Running, State.Jumping, () =>
    {
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
      _currentHorizontalSpeed = RunningSpeed;
    });

    _sm.OnTransitionFromExceptTo (State.Running, State.Jumping, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
      _currentHorizontalSpeed = WalkingSpeed;
    });

    _sm.OnTransitionTo (State.Running, () =>
    {
      _sprite.Animation = RunningAnimation;
      _energyTimer.Start (_energyMeterDepletionRatePerUnit);
      _currentHorizontalSpeed = RunningSpeed;
    });

    _sm.OnTransitionFrom (State.ClimbingUp, () =>
    {
      if (_energy < MaxEnergy) _energyTimer.Start (_energyMeterReplenishRatePerUnit);
    });

    _sm.OnTransitionTo (State.ClimbingUp, () =>
    {
      _sprite.Animation = ClimbingUpAnimation;
      FlipHorizontally (false);
    });

    _sm.OnTransitionTo (State.ClimbingDown, () =>
    {
      _sprite.Animation = ClimbingDownAnimation;
      FlipHorizontally (false);
    });

    _sm.OnTransitionFrom (State.FreeFalling, () =>
    {
      _wasInCliffEdge = false;
      _justRespawned = false;
    });

    _sm.AddFrameAction (State.Idle);
    _sm.AddFrameAction (State.FreeFalling, velocityAction: MoveHorizontally);
    _sm.AddFrameAction (State.Walking, velocityAction: MoveHorizontally);
    _sm.AddFrameAction (State.Running, velocityAction: MoveHorizontally);
    _sm.AddFrameAction (State.CliffHanging, GravityType.None);

    _sm.AddFrameAction (State.ClimbingDown, GravityType.None, velocityAction: (velocity, _) =>
    {
      velocity.y = _sprite.Frame is 3 or 8 ? VerticalClimbingSpeed * GetClimbingSpeedBoost() : 0.0f;

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
      velocity.y = _sprite.Frame is 3 or 8 ? -VerticalClimbingSpeed * GetClimbingSpeedBoost() : 0.0f;
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

    _sm.AddTrigger (State.Idle, State.Walking, Input.Horizontal, positioning: Positioning.Ground, conditions: _ (And (() => !IsDepletingEnergy(), () => !_isDroppingDown)));
    _sm.AddTrigger (State.Idle, State.Running, inputs: _ (Required (Input.Horizontal, Input.Energy)), positioning: Positioning.Ground, conditions: _ (And (IsDepletingEnergy, () => !IsHittingWall(), () => !_isDroppingDown)));
    _sm.AddTrigger (State.Idle, State.Jumping, inputs: _ (Required (Input.Jump), Optional (Input.Horizontal)), positioning: Positioning.Ground, and: () => !_isDroppingDown);
    _sm.AddTrigger (State.Idle, State.ClimbingPrep, Input.Up, positioning: Positioning.Ground, conditions: _ (And (CanClimbPrep, () => !IsDepletingEnergy(), () => !_isDroppingDown, () => !_isResting)));
    _sm.AddTrigger (State.Idle, State.ClimbingDown, Input.Down, motion: Motion.Down, positioning: Positioning.Air, conditions: _ (And (() => IsInCliffs, () => !_isDroppingDown)));

    // TODO Try to eliminate custom input conditions.
    _sm.AddTrigger (State.Idle, State.FreeFalling, motion: Motion.Down, positioning: Positioning.Air, conditions: _ (And (() => !IsPressed (Input.Down), () => !WasPressed (Input.Jump)), Or (() => _isDroppingDown)));

    _sm.AddTrigger (State.Idle, State.ReadingSign, Input.Up, positioning: Positioning.Ground, conditions: _ (And (CanReadSign, () => !_isDroppingDown, () => !_isResting)));
    _sm.AddTrigger (State.Walking, State.Idle, motion: Motion.None, positioning: Positioning.Ground);
    _sm.AddTrigger (State.Walking, State.Running, inputs: _ (Required (Input.Horizontal, Input.Energy)), motion: Motion.Horizontal, positioning: Positioning.Ground, and: IsDepletingEnergy);
    _sm.AddTrigger (State.Walking, State.Jumping, inputs: _ (Required (Input.Jump), Optional (Input.Horizontal)), positioning: Positioning.Ground);

    // TODO Try to eliminate custom input conditions.
    _sm.AddTrigger (State.Walking, State.FreeFalling, motion: Motion.Down, positioning: Positioning.Air, conditions: _(And (() => !WasPressed (Input.Jump), () => !IsHittingWall())));

    _sm.AddTrigger (State.Walking, State.ClimbingPrep, Input.Up, motion: Motion.None, positioning: Positioning.Ground, and: CanClimbPrep);
    _sm.AddTrigger (State.Walking, State.ReadingSign, Input.Up, motion: Motion.None, positioning: Positioning.Ground, and: CanReadSign);
    _sm.AddTrigger (State.Running, State.Idle, Input.None, motion: Motion.None, positioning: Positioning.Ground);
    _sm.AddTrigger (State.Running, State.Walking, Input.Horizontal, motion: Motion.Horizontal, positioning: Positioning.Ground, conditions: _ (And (() => !IsDepletingEnergy()), Or (JustDepletedAllEnergy)));

    // TODO Testing
    _sm.AddTrigger (State.Running, State.Jumping, inputs: _ (Required (Input.Jump, Input.Left)), positioning: Positioning.Ground, motion: Motion.Left);

    _sm.AddTrigger (State.Running, State.FreeFalling, motion: Motion.Down, positioning: Positioning.Air, and: () => !IsHittingWall());
    _sm.AddTrigger (State.Running, State.ClimbingPrep, Input.Up, motion: Motion.None, positioning: Positioning.Ground, and: CanClimbPrep);
    _sm.AddTrigger (State.Running, State.ReadingSign, Input.Up, motion: Motion.None, positioning: Positioning.Ground, and: CanReadSign);

    // TODO Testing
    _sm.AddTrigger (State.Jumping, State.FreeFalling, motions: _ (Required (Motion.Down), Optional (Motion.Horizontal)), positioning: Positioning.Air); // TODO Test with input condition.

    _sm.AddTrigger (State.ClimbingPrep, State.Idle, Input.None, motion: Motion.None, positioning: Positioning.Ground);

    // TODO Test with positioning.
    _sm.AddTrigger (State.ClimbingPrep, State.ClimbingUp, Input.Up, conditions: _ (And (() => _climbingPrepTimer.TimeLeft == 0, () => !IsInCliffIce)));
    _sm.AddTrigger (State.ClimbingUp, State.Idle, condition: () => _isResting); // TODO Test more strict conditions.

    _sm.AddTrigger (State.ClimbingUp, State.FreeFalling, Input.Jump, positioning: Positioning.Air, conditions: _ (Or (() => !IsInClimbableLocation(), () => IsInCliffIce, JustDepletedAllEnergy), And (() => !_isResting)));

    // TODO Testing.
    _sm.AddTrigger (State.ClimbingUp, State.CliffHanging, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.ClimbingDown, State.CliffHanging, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);
    // _sm.AddTrigger (State.ClimbingUp, State.ClimbingDown, Input.Down, positioning: Positioning.Air, and: IsInClimbableLocation);

    _sm.AddTrigger (State.ClimbingDown, State.Idle, positioning: Positioning.Ground); // TODO Test more strict conditions.
    _sm.AddTrigger (State.ClimbingDown, State.ClimbingUp, Input.Up, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.ClimbingDown, State.FreeFalling, Input.Jump, positioning: Positioning.Air, conditions: _ (Or (() => !IsInClimbableLocation(), () => IsInCliffIce, () => _isResting)));
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
    _sm.AddTrigger (State.Traversing, State.FreeFalling, inputs: _ (Required (Input.Jump), Optional (Input.Horizontal)), positioning: Positioning.Air, conditions: _ (And (() => !IsInClimbableLocation()), Or (() => IsInCliffIce)));
    _sm.AddTrigger (State.Traversing, State.CliffHanging, inputs: _ (Optional (Required (Input.Left, Input.Right))), motion: Motion.None, positioning: Positioning.Air, conditions: _ (And (IsInClimbableLocation, () => !IsInCliffIce)));

    _sm.AddTrigger (State.CliffArresting, State.FreeFalling, Input.None, motions: _ (Required (Motion.Down), Optional (Motion.Horizontal)), positioning: Positioning.Air);

    _sm.AddTrigger (State.CliffArresting, State.CliffHanging, Input.Item, motion: Motion.None, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.CliffArresting, State.Traversing, inputs: _ (Required (Input.Item), Optional (Input.Horizontal)), motion: Motion.Horizontal, positioning: Positioning.Air, and: IsInClimbableLocation);
    _sm.AddTrigger (State.CliffArresting, State.Idle, positioning: Positioning.Ground); // TODO Test more strict conditions.
    _sm.AddTrigger (State.ReadingSign, State.Idle, Input.Down, motion: Motion.None, positioning: Positioning.Ground);
    _sm.AddTrigger (State.ReadingSign, State.Walking, Input.Horizontal, motion: Motion.Horizontal, positioning: Positioning.Ground);
    _sm.AddTrigger (State.ReadingSign, State.Running, Input.Horizontal, motion: Motion.Horizontal, positioning: Positioning.Ground, and: IsDepletingEnergy);

    // @formatter:on
  }
}