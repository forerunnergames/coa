using Godot;

public interface IAscendableStair : INode
{
  public enum Mode
  {
    Idle,
    Ascending,
    Descending
  }

  public bool IsHorizontalCenterPointTouching (Rect2 colliderRect);
  public void SetMode (Mode mode);
}