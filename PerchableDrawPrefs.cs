using Godot;

public class PerchableDrawPrefs
{
  public bool DrawAreas { get; set; }
  public Color AreasColor { get; set; } = Colors.White;
  public bool AreasFilled { get; set; }
  public bool DrawPerchPoint { get; set; }
  public Color PerchPointColor { get; set; } = Colors.White;
  public bool PerchPointFilled { get; set; } = true;
}