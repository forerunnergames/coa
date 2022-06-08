using System.Collections.Generic;
using Godot;

public class DefaultPerch : AbstractPerch
{
  public DefaultPerch (Vector2 localScale, Vector2 globalScale, Vector2 globalOrigin, PerchableDrawPrefs drawPrefs,
    List <Rect2> perchableAreasInLocalSpace, float positionEpsilon) : base ("Default", localScale, globalScale, globalOrigin,
    drawPrefs, perchableAreasInLocalSpace, positionEpsilon)
  {
  }
}