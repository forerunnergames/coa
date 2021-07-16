using Godot;

public class CatmullRom
{
  private Vector2 _splinePoint = Vector2.Zero;

  public Vector2 GetSplinePoint (Line2D path, float t)
  {
    var ft = Mathf.FloorToInt (t);
    var p1Idx = ft + 1;
    var p2Idx = p1Idx + 1;
    var p3Idx = p2Idx + 1;
    var p0Idx = p1Idx - 1;
    t -= ft;
    var tt = t * t;
    var ttt = tt * t;
    var q1 = -ttt + 2.0f * tt - t;
    var q2 = 3.0f * ttt - 5.0f * tt + 2.0f;
    var q3 = -3.0f * ttt + 4.0f * tt + t;
    var q4 = ttt - tt;

    _splinePoint.x = 0.5f * (path.Points[p0Idx].x * q1 + path.Points[p1Idx].x * q2 + path.Points[p2Idx].x * q3 +
                             path.Points[p3Idx].x * q4);

    _splinePoint.y = 0.5f * (path.Points[p0Idx].y * q1 + path.Points[p1Idx].y * q2 + path.Points[p2Idx].y * q3 +
                             path.Points[p3Idx].y * q4);

    return _splinePoint;
  }
}