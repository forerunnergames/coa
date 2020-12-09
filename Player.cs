using System.Collections.Generic;
using Godot;
using static Tools;

// Climbing terminology:
// Traversing: Climbing horizontally
// Scraping: Using pickaxe to slow a cliff fall
// Cliffhanging: Attached to a cliff above the ground, not moving
// TODO Use ray tracing to fix IsFullyIntersectingCliffs bug, where IsOnFloor is true and IsFullyIntersectingCliffs
// TODO   should be true, but is false, even when standing fully in front of cliffs.
public class Player : KinematicBody2D
{
  // Godot-configurable options
  [Export] public float HorizontalRunningSpeed = 50.0f;
  [Export] public float HorizontalClimbingSpeed = 200.0f;
  [Export] public float VerticalClimbingSpeed = 200.0f;
  [Export] public float HorizontalRunJumpFriction = 0.9f;
  [Export] public float HorizontalRunJumpStoppingFriction = 0.6f;
  [Export] public float HorizontalClimbFriction = 0.9f;
  [Export] public float HorizontalClimbStoppingFriction = 0.6f;
  [Export] public float CliffScrapingSpeed = 40.0f;
  [Export] public float CliffScrapingActivationVelocity = 800.0f;
  [Export] public float VelocityEpsilon = 100.0f;
  [Export] public float JumpPower = 800.0f;
  [Export] public float Gravity_ = 30.0f;
  [Export] public string IdleLeftAnimation = "player_idle_left";
  [Export] public string PreparingToClimbUpAnimation = "player_idle_back";
  [Export] public string FallingAnimation = "player_falling";
  [Export] public string ClimbingUpAnimation = "player_climbing_up";
  [Export] public string CliffHangingAnimation = "player_cliff_hanging";
  [Export] public string ClimbingHorizontallyAnimation = "player_climbing_horizontally";
  [Export] public string ScrapingAnimation = "player_scraping";
  [Export] public string RunAnimation = "player_running";
  [Export] public string CliffScrapingSoundFile = "res://cliff_scrape.wav";
  [Export] public bool CliffScrapingSoundLooping = true;
  [Export] public float CliffScrapingSoundLoopBeginSeconds = 0.0f;
  [Export] public float CliffScrapingSoundLoopEndSeconds = 4.5f;
  [Export] public float CliffScrapingSoundVelocityPitchScaleModulation = 4.0f;
  [Export] public float CliffScrapingSoundMinPitchScale = 2.0f;

  // Field must be publicly accessible from Cliffs.cs
  // ReSharper disable once MemberCanBePrivate.Global
  // ReSharper disable once UnassignedField.Global
  public bool IsIntersectingCliffs;

  // Field must be publicly accessible from Cliffs.cs
  // ReSharper disable once MemberCanBePrivate.Global
  // ReSharper disable once UnassignedField.Global
  public bool IsFullyIntersectingCliffs;

  private Vector2 _velocity;
  private RichTextLabel _label;
  private AnimatedSprite _sprite;
  private AudioStreamPlayer _audio;
  private Timer _preparingToClimbUpTimer;
  private bool _isFlippedHorizontally;
  private readonly List <string> _printLines = new List <string>();

  // TODO State machine
  private bool _isRunning;
  private bool _isJumping;
  private bool _isPreparingToClimbUp;
  private bool _isPreparedToClimbUp;
  private bool _isClimbingUp;
  private bool _isCliffHanging;
  private bool _isClimbingHorizontally;
  private bool _isClimbingLeft;
  private bool _isClimbingRight;
  private uint _fallingStartTimeMs;
  private uint _elapsedFallingTimeMs;
  private uint _lastTotalFallingTimeMs;
  private float _highestVerticalVelocity;
  private bool _isScrapingCliff;

  private bool IsScrapingCliff
  {
    get => _isScrapingCliff;
    set
    {
      // TODO State Machine
      if (!_isScrapingCliff && value && IsFalling() && !_audio.Playing)
      {
        _audio.Play();
        PrintLine ("Sound effects: Playing cliff scraping sound.");
      }
      else if (_isScrapingCliff && !value && _audio.Playing)
      {
        _audio.Stop();
        PrintLine ("Sound effects: Stopped cliff scraping sound.");
      }

      _isScrapingCliff = value;
    }
  }

  public override void _Ready()
  {
    _audio = GetNode <AudioStreamPlayer> ("AudioStreamPlayer");
    _audio.Stream = ResourceLoader.Load <AudioStream> (CliffScrapingSoundFile);
    LoopAudio (_audio.Stream, CliffScrapingSoundLoopBeginSeconds, CliffScrapingSoundLoopEndSeconds);
    _label = GetNode <RichTextLabel> ("DebuggingText");
    _sprite = GetNode <AnimatedSprite> ("AnimatedSprite");
    _preparingToClimbUpTimer = GetNode <Timer> ("ClimbingReadyTimer");
    _sprite.Animation = IdleLeftAnimation;
  }

  public override void _PhysicsProcess (float delta)
  {
    Run();
    Jump();
    Climb();
    Friction();
    Gravity();
    PostGravity();
    SoundEffects();

    _velocity = MoveAndSlide (_velocity, Vector2.Up);

    if (ShouldBecomeIdle()) BecomeIdle();
    Animations();
    CalculateFallingStats();

    // @formatter:off
    PrintLine ("IsRightArrowPressed(): " + IsRightArrowPressed() + "\nIsLeftArrowPressed(): " + IsLeftArrowPressed());
    PrintLine ("IsInMotion(): " + IsInMotion());
    PrintLine ("IsFalling(): " + IsFalling());
    PrintLine ("_velocity: " + _velocity);
    PrintLine ("Vertical velocity (mph): " + _velocity.y * 0.028334573333333);
    PrintLine ("Highest vertical velocity: " + _highestVerticalVelocity);
    PrintLine ("Highest vertical velocity (mph): " + _highestVerticalVelocity * 0.028334573333333);
    PrintLine ("Falling time (sec): " + (_elapsedFallingTimeMs > 0 ? _elapsedFallingTimeMs / 1000.0f : _lastTotalFallingTimeMs / 1000.0f));
    Print();
    // @formatter:on
  }

  // Godot Timer callback
  // ReSharper disable once UnusedMember.Global
  public void _OnClimbingReadyTimerTimeout()
  {
    if (!_isPreparingToClimbUp) return;

    _isPreparedToClimbUp = IsUpArrowPressed();
    _isPreparingToClimbUp = false;
  }

  public override void _UnhandledInput (InputEvent @event)
  {
    if (!(@event is InputEventKey eventKey)) return;

    if (eventKey.IsActionReleased ("use_item")) IsScrapingCliff = false;
    else if (eventKey.IsActionReleased ("show_text")) _label.Visible = !_label.Visible;
    else if (eventKey.IsActionReleased ("respawn")) GlobalPosition = new Vector2 (952, -4032);
  }

  private void Run()
  {
    PrintLine ("IsAnyHorizontalArrowPressed(): " + IsAnyHorizontalArrowPressed());
    PrintLine ("IsAnyVerticalArrowPressed(): " + IsAnyVerticalArrowPressed());
    PrintLine ("_isRunning: " + _isRunning);
    PrintLine ("shouldRun(): " + ShouldRun());

    if (!IsInMotionHorizontally() || !IsOnFloor()) _isRunning = false;

    if (!ShouldRun()) return;
    if (ShouldRunRight()) RunRight();
    else if (ShouldRunLeft()) RunLeft();

    _isRunning = true;
    _isJumping = false;
    _isPreparingToClimbUp = false;
    _isPreparedToClimbUp = false;
    _isClimbingUp = false;
    _isClimbingHorizontally = false;
    _isClimbingLeft = false;
    _isClimbingRight = false;
    IsScrapingCliff = false;
    _isCliffHanging = false;
  }

  private bool ShouldRun()
  {
    return IsExclusivelyActiveUnless (InputType.Horizontal, _isRunning) && !ShouldClimbHorizontally();
  }

  private static bool ShouldRunRight()
  {
    return IsRightArrowPressed() && !IsLeftArrowPressed();
  }

  private void RunRight()
  {
    _velocity.x += HorizontalRunningSpeed;
    FlipHorizontally (true);
  }

  private static bool ShouldRunLeft()
  {
    return IsLeftArrowPressed() && !IsRightArrowPressed();
  }

  private void RunLeft()
  {
    _velocity.x -= HorizontalRunningSpeed;
    FlipHorizontally (false);
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

  private void Jump()
  {
    if (IsOnFloor()) _isJumping = false;

    if (Input.IsActionJustPressed ("jump") && IsOnFloor())
    {
      _isJumping = true;
      _isRunning = false;
      _isPreparingToClimbUp = false;
      _isPreparedToClimbUp = false;
      _isClimbingUp = false;
      _isClimbingHorizontally = false;
      _isClimbingLeft = false;
      _isClimbingRight = false;
      IsScrapingCliff = false;
      _isCliffHanging = false;
      _velocity.y -= JumpPower;
    }

    // Make jumps less high when releasing jump button early.
    // (Holding down jump continuously allows gravity to take over.)
    if (Input.IsActionJustReleased ("jump") && _isJumping && IsMovingUp()) _velocity.y = 0.0f;

    PrintLine ("_isJumping: " + _isJumping);
  }

  // TODO Test adding && !_isJumping
  // TODO Differentiate between jump-falling (landing) and other falling.
  private bool IsFalling()
  {
    return _velocity.y - VelocityEpsilon > 0.0f;
  }

  private bool IsMovingUp()
  {
    return _velocity.y + VelocityEpsilon < 0.0f;
  }

  private void Climb()
  {
    if (ShouldPrepareToClimbUp()) PrepareToClimbUp();
    else if (ShouldScrapeCliff()) ScrapeCliff();
    else if (ShouldCliffHang()) CliffHang();
    else if (ShouldClimbHorizontally()) ClimbHorizontally();
    else if (_isClimbingHorizontally) CliffHang();
    else if (ShouldClimbUp()) ClimbUp();
    else if (_isClimbingUp) DropFromCliff();
    else if (ShouldClimbDownFromFloor()) ClimbDownFromFloor();
    else if (ShouldDropFromCliff()) DropFromCliff();

    // @formatter:off
    PrintLine ("_isPreparingToClimbUp: " + _isPreparingToClimbUp +
               "\n_isPreparedToClimbUp: " + _isPreparedToClimbUp +
               "\nShouldClimbUp(): " + ShouldClimbUp() +
               "\n_isClimbingUp: " + _isClimbingUp +
               "\nIsFalling(): " + IsFalling() +
               "\nIsMovingUp(): " + IsMovingUp() +
               "\n_isScrapingCliff: " + IsScrapingCliff +
               "\nShouldScrapeCliff(): " + ShouldScrapeCliff() +
               "\nIsOnFloor(): " + IsOnFloor() +
               "\nIsIntersectingCliffs: " + IsIntersectingCliffs +
               "\nIsFullyIntersectingCliffs: " + IsFullyIntersectingCliffs +
               "\n_isClimbingHorizontally: " + _isClimbingHorizontally +
               "\n_isCliffHanging: " + _isCliffHanging +
               "\nShouldClimbHorizontally(): " + ShouldClimbHorizontally());
    // @formatter:on
  }

  // TODO Test ignore input exclusion condition _isReadyToClimbUp
  // TODO Use IsFullyIntersectingCliffs after using ray tracing to fix errors.
  // Currently, the running state is allowed to transition instantly to the getting ready to climb state.
  // Otherwise, !IsInMotion() would need to be a condition here.
  private bool ShouldPrepareToClimbUp()
  {
    return IsIntersectingCliffs && !_isPreparingToClimbUp && !_isPreparedToClimbUp && !_isClimbingUp && IsOnFloor() &&
           !IsFalling() && WasUpArrowPressedOnce();
  }

  private void PrepareToClimbUp()
  {
    FlipHorizontally (false);

    _isPreparingToClimbUp = true;
    _isPreparedToClimbUp = false;
    IsScrapingCliff = false;
    _isRunning = false;
    _isJumping = false;
    _isClimbingUp = false;
    _isClimbingHorizontally = false;
    _isClimbingLeft = false;
    _isClimbingRight = false;
    _isCliffHanging = false;

    _preparingToClimbUpTimer.Start();
  }

  // TODO Create _isPreparingToScrapeCliff, which is true when attempting scraping, but falling hasn't
  // TODO   gotten fast enough yet to active it (i.e., ShouldScrapeCliff() would return true except
  // TODO   _velocity.y < CliffScrapingActivationVelocity) In this case show an animation of pulling out pickaxe and
  // TODO   swinging it into the cliff, timing it so that the pickaxe sinks into the cliff when
  // TODO   CliffScrapingActivationVelocity is reached.
  // TODO Test with !IsOnFloor() removed since IsFalling() makes it redundant.
  // TODO Check if item being used is pickaxe. For now it's the default and only item.
  private bool ShouldScrapeCliff()
  {
    return IsFullyIntersectingCliffs && !IsOnFloor() && IsFalling() && _velocity.y >= CliffScrapingActivationVelocity &&
           IsExclusivelyActiveUnless (InputType.Item, IsScrapingCliff);
  }

  private void ScrapeCliff()
  {
    IsScrapingCliff = true;
    _isRunning = false;
    _isJumping = false;
    _isPreparingToClimbUp = false;
    _isPreparedToClimbUp = false;
    _isClimbingUp = false;
    _isClimbingHorizontally = false;
    _isClimbingLeft = false;
    _isClimbingRight = false;
    _isCliffHanging = false;
  }

  private bool ShouldCliffHang()
  {
    return IsScrapingCliff && !IsInMotion() && !IsOnFloor();
  }

  private void CliffHang()
  {
    _isCliffHanging = true;
    _isRunning = false;
    _isJumping = false;
    _isPreparingToClimbUp = false;
    _isPreparedToClimbUp = false;
    _isClimbingUp = false;
    _isClimbingHorizontally = false;
    _isClimbingLeft = false;
    _isClimbingRight = false;
    IsScrapingCliff = false;
  }

  private bool ShouldClimbHorizontally()
  {
    return (_isCliffHanging || _isClimbingHorizontally) && IsExclusivelyActiveUnless (InputType.Horizontal, _isClimbingHorizontally);
  }

  private void ClimbHorizontally()
  {
    if (ShouldClimbLeft()) ClimbLeft();
    else if (ShouldClimbRight()) ClimbRight();

    _isClimbingHorizontally = true;
    _isRunning = false;
    _isJumping = false;
    _isPreparingToClimbUp = false;
    _isPreparedToClimbUp = false;
    _isClimbingUp = false;
    IsScrapingCliff = false;
    _isCliffHanging = false;
  }

  private static bool ShouldClimbLeft()
  {
    return IsLeftArrowPressed() && !IsRightArrowPressed();
  }

  private void ClimbLeft()
  {
    _isClimbingLeft = true;
    _isClimbingRight = false;
  }

  private static bool ShouldClimbRight()
  {
    return IsRightArrowPressed() && !IsLeftArrowPressed();
  }

  private void ClimbRight()
  {
    _isClimbingRight = true;
    _isClimbingLeft = false;
  }

  private bool ShouldClimbUp()
  {
    return (_isPreparedToClimbUp || _isClimbingUp || _isCliffHanging) && IsIntersectingCliffs &&
           IsExclusivelyActiveUnless (InputType.Up, _isClimbingUp);
  }

  private void ClimbUp()
  {
    FlipHorizontally (false);

    _isClimbingUp = true;
    _isRunning = false;
    _isJumping = false;
    _isPreparingToClimbUp = false;
    _isPreparedToClimbUp = false;
    _isClimbingHorizontally = false;
    _isClimbingLeft = false;
    _isClimbingRight = false;
    IsScrapingCliff = false;
    _isCliffHanging = false;
  }

  private bool ShouldDropFromCliff()
  {
    return _isCliffHanging && WasDownArrowPressedOnce();
  }

  private void DropFromCliff()
  {
    FlipHorizontally (false);

    _isCliffHanging = false;
    _isRunning = false;
    _isJumping = false;
    _isPreparingToClimbUp = false;
    _isPreparedToClimbUp = false;
    _isClimbingUp = false;
    _isClimbingHorizontally = false;
    _isClimbingLeft = false;
    _isClimbingRight = false;
    IsScrapingCliff = false;
  }

  private bool ShouldClimbDownFromFloor()
  {
    return WasDownArrowPressedOnce() && IsOnFloor();
  }

  // Only for ground/floor that has climbable cliffs below it
  private void ClimbDownFromFloor()
  {
    var isClimbingDown = false;

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
      isClimbingDown = true;
    }

    if (!isClimbingDown) return;

    _isRunning = false;
    _isJumping = false;
    _isPreparingToClimbUp = false;
    _isPreparedToClimbUp = false;
    _isClimbingUp = false;
    _isClimbingHorizontally = false;
    _isClimbingLeft = false;
    _isClimbingRight = false;
    IsScrapingCliff = false;
    _isCliffHanging = false;

    FlipHorizontally (false);
  }

  private void Friction()
  {
    if (!IsAnyHorizontalArrowPressed()) _velocity.x *= HorizontalRunJumpStoppingFriction;
    else if (_isClimbingHorizontally) _velocity.x *= HorizontalClimbFriction;
    else _velocity.x *= HorizontalRunJumpFriction;
  }

  private void Gravity()
  {
    _velocity.y += Gravity_;
  }

  private void PostGravity()
  {
    if (_isClimbingUp)
    {
      _velocity.y -= Gravity_ + VerticalClimbingSpeed * 0.01f;
      _velocity.y = SafelyClampMin (_velocity.y, -VerticalClimbingSpeed);
    }
    else if (_isClimbingHorizontally)
    {
      _velocity.x = _isClimbingLeft ? _velocity.x - HorizontalClimbingSpeed : _velocity.x + HorizontalClimbingSpeed;
      _velocity.x = SafelyClamp (_velocity.x, -HorizontalClimbingSpeed, HorizontalClimbingSpeed);
      _velocity.y = 0.0f;
    }
    else if (_isCliffHanging)
    {
      _velocity.y = 0.0f;
    }
    else if (IsScrapingCliff)
    {
      _velocity.y -= CliffScrapingSpeed;
      _velocity.y = SafelyClampMin (_velocity.y, 0.0f);
    }
  }

  private bool IsInMotion()
  {
    return IsInMotionHorizontally() || IsInMotionVertically();
  }

  private bool IsInMotionHorizontally()
  {
    return Mathf.Abs (_velocity.x) > VelocityEpsilon;
  }

  private bool IsInMotionVertically()
  {
    return Mathf.Abs (_velocity.y) > VelocityEpsilon;
  }

  private void SoundEffects()
  {
    if (IsScrapingCliff)
    {
      _audio.PitchScale = CliffScrapingSoundMinPitchScale +
                          _velocity.y / CliffScrapingActivationVelocity /
                          CliffScrapingSoundVelocityPitchScaleModulation;
    }
  }

  private bool ShouldBecomeIdle()
  {
    return IsOnFloor() && !IsInMotion() && !_isPreparingToClimbUp && !_isPreparedToClimbUp && !_isClimbingUp;
  }

  private void BecomeIdle()
  {
    _isRunning = false;
    _isJumping = false;
    _isPreparingToClimbUp = false;
    _isPreparedToClimbUp = false;
    _isClimbingUp = false;
    _isClimbingHorizontally = false;
    _isClimbingLeft = false;
    _isClimbingRight = false;
    IsScrapingCliff = false;
    _isCliffHanging = false;
  }

  private void Animations()
  {
    var isFalling = IsFalling();
    var isInMotion = IsInMotion();

    // TODO Use a running animation
    // TODO _isRunning should not be true when moving horizontally through air; these are 2 different states
    // TODO Falling from a jump should be a different state than falling from a cliff.
    // @formatter:off
    if (_isRunning && !isFalling) _sprite.Animation = IdleLeftAnimation;
    else if (_isPreparingToClimbUp || _isPreparedToClimbUp) _sprite.Animation = PreparingToClimbUpAnimation;
    else if (IsScrapingCliff) _sprite.Animation = ScrapingAnimation;
    else if (_isCliffHanging) _sprite.Animation = CliffHangingAnimation;
    else if (_isClimbingHorizontally) _sprite.Animation = ClimbingHorizontallyAnimation;
    else if (_isClimbingUp) _sprite.Animation = ClimbingUpAnimation;
    else if (isFalling && !_isJumping && IsItemKeyPressed() && IsFullyIntersectingCliffs) _sprite.Animation = ScrapingAnimation;
    else if (isFalling && !_isJumping) _sprite.Animation = FallingAnimation;
    else if (!isInMotion) _sprite.Animation = IdleLeftAnimation;
    // @formatter:on
  }

  private void CalculateFallingStats()
  {
    if (_velocity.y > _highestVerticalVelocity) _highestVerticalVelocity = _velocity.y;

    if (IsFalling() && _fallingStartTimeMs == 0)
    {
      _fallingStartTimeMs = OS.GetTicksMsec();
    }
    else if (IsFalling())
    {
      _elapsedFallingTimeMs = OS.GetTicksMsec() - _fallingStartTimeMs;
    }
    else if (!IsFalling() && _elapsedFallingTimeMs > 0)
    {
      _lastTotalFallingTimeMs = OS.GetTicksMsec() - _fallingStartTimeMs;
      _fallingStartTimeMs = 0;
      _elapsedFallingTimeMs = 0;
    }
  }

  private void PrintLine (string line)
  {
    _printLines.Add (line);
  }

  private void Print()
  {
    _label.Text = "";
    _label.BbcodeText = "";
    foreach (var line in _printLines) _label.AddText (line + "\n");
    _printLines.Clear();
  }
}