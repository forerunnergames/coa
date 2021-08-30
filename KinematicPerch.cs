using System.Collections.Generic;
using Godot;

public class KinematicPerch : AbstractPerch
{
  public KinematicPerch (string name, Vector2 globalOrigin, PerchableDrawPrefs drawPrefs, List <Rect2> perchableAreasInLocalSpace,
    float positionEpsilon) : base (name, globalOrigin, drawPrefs, perchableAreasInLocalSpace, positionEpsilon)
  {
  }
}