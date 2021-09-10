using System.Collections.Generic;
using Godot;

public class AnimatedSpritePerch : AbstractPerch
{
  public AnimatedSpritePerch (string animationName, Vector2 globalOrigin, PerchableDrawPrefs drawPrefs,
    List <Rect2> perchableAreasInLocalSpace, float positionEpsilon) : base (animationName, globalOrigin, drawPrefs,
    perchableAreasInLocalSpace, positionEpsilon)
  {
  }
}