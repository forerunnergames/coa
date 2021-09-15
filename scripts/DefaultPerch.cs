using System.Collections.Generic;
using Godot;

public class DefaultPerch : AbstractPerch
{
  public DefaultPerch (Vector2 globalOrigin, PerchableDrawPrefs drawPrefs, List <Rect2> perchableAreasInLocalSpace,
    float positionEpsilon) : base ("Default", globalOrigin, drawPrefs, perchableAreasInLocalSpace, positionEpsilon)
  {
  }
}