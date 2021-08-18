using System.Collections.Generic;
using Godot;

public class TestPerchableImpl : AbstractPerch
{
  public TestPerchableImpl (string name, Vector2 globalOrigin, PerchableDrawPrefs drawPrefs, List <Rect2> perchableAreasInLocalSpace,
    float positionEpsilon) : base (name, globalOrigin, drawPrefs, perchableAreasInLocalSpace, positionEpsilon)
  {
  }
}