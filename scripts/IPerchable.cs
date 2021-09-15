using Godot;
using static Tools;

public interface IPerchable
{
  public string Name { get; }
  public PerchableDrawPrefs DrawPrefs { get; }
  public Vector2 GlobalOrigin { get; set; }
  public bool Disabled { get; set; }
  public bool FlippedHorizontally { get; }
  public bool FlippedVertically { get; }
  public void Draw (DrawRect draw, Vector2 perchPoint, GetLocalTransform getLocalTransform, GetGlobalScale getGlobalScale);
  public float DistanceFrom (Vector2 globalPosition);
  public Vector2 RandomPoint (RandomNumberGenerator rng, GetGlobalScale getGlobalScale);
  public bool Contains (Vector2 globalPosition);
  public bool Is (string name, Vector2 globalOrigin);
  public void FlipHorizontally();
  public void FlipVertically();
  public bool HasParentName (string name);
}