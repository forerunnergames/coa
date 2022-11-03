using Godot;

public class Cabin : Area2D, IOpenableDoor
{
  [Export] public Log.Level LogLevel = Log.Level.Info;
  [Signal] public delegate void OnOpenedDoorCompleted();
  [Signal] public delegate void OnClosedDoorCompleted();
  public bool IsPlayerInCabin { get; private set; }
  private AudioStreamPlayer _doorSoundEffects;
  private AnimatedSprite _doorAnimatedSprite;
  private CollisionShape2D _doorCollider;
  private AudioStream _closeDoorSound;
  private AudioStream _openDoorSound;
  private Area2D _doorArea;
  private bool _isOpen;
  private Log _log;

  public override void _Ready()
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (Name) { CurrentLevel = LogLevel };
    _doorSoundEffects = GetNode <AudioStreamPlayer> ("DoorSoundEffects");
    _doorArea = GetNode <Area2D> ("Door");
    _doorCollider = _doorArea.GetNode <CollisionShape2D> ("CollisionShape2D");
    _doorAnimatedSprite = _doorArea.GetNode <AnimatedSprite> ("AnimatedSprite");
    _closeDoorSound = ResourceLoader.Load <AudioStream> ("res://assets/sounds/door_close.wav");
    _openDoorSound = ResourceLoader.Load <AudioStream> ("res://assets/sounds/door_open.wav");
  }

  // ReSharper disable once UnusedMember.Global
  // ReSharper disable once MemberCanBePrivate.Global
  public void _OnCabinEntered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"Player entered {Name}.");
    IsPlayerInCabin = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnCabinExited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _log.Info ($"Player exited {Name}.");
    IsPlayerInCabin = false;
  }

  // ReSharper disable once UnusedMember.Global
  public void _OnOpenCloseDoorAnimationFinished()
  {
    _doorAnimatedSprite.Stop();
    EmitSignal (_isOpen ? IOpenableDoor.Signal.OnOpenedDoorCompleted : IOpenableDoor.Signal.OnClosedDoorCompleted);
  }

  public Node AsNode() => this;

  public void Open()
  {
    _doorAnimatedSprite.Play ("open_door");
    _doorSoundEffects.Stream = _openDoorSound;
    _doorSoundEffects.PitchScale = 1.0f;
    _doorSoundEffects.Play();
    _isOpen = true;
  }

  public void Close()
  {
    _doorAnimatedSprite.Play ("open_door", backwards: true);
    _doorSoundEffects.Stream = _closeDoorSound;
    _doorSoundEffects.PitchScale = 0.5f;
    _doorSoundEffects.Play();
    _isOpen = false;
  }

  public bool IsOpen() => _isOpen;
  public bool Encloses (Rect2 colliderRect) => Tools.IsEnclosedBy (colliderRect, Tools.GetColliderRect (_doorArea, _doorCollider));

  public string NameOf (IOpenableDoor.Signal signal) =>
    signal switch
    {
      IOpenableDoor.Signal.OnOpenedDoorCompleted => nameof (OnOpenedDoorCompleted),
      IOpenableDoor.Signal.OnClosedDoorCompleted => nameof (OnClosedDoorCompleted),
      _ => ""
    };

  private void EmitSignal (IOpenableDoor.Signal signal) => EmitSignal (NameOf (signal));
}