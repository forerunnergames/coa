using Godot;

public interface IOpenableDoor : ISignallingNode <IOpenableDoor.Signal>
{
  enum Signal
  {
    OnOpenedDoorCompleted,
    OnClosedDoorCompleted
  }

  public void Open();
  public void Close();
  public bool IsOpen();
  public bool Encloses (Rect2 colliderRect);
}