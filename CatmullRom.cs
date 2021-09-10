using System.Collections.Generic;
using System.Linq;
using Godot;
using static Tools;
using Transform = Tools.Transform;

public class CatmullRom
{
  public float Length { get; private set; }
  public Color DrawColor { get => _drawColor == null ? Colors.White : _drawColor[0]; set => _drawColor = new[] { value }; }
  public bool DrawPath { get; set; }

  // ReSharper disable once MemberCanBePrivate.Global
  public List <Vector2> Path
  {
    get => _path;
    set
    {
      _path = value;
      OnPathChanged();
    }
  }

  private List <Vector2> _path;
  private Vector2 _splinePoint = Vector2.Zero;
  private readonly List <float> _splineLengths = new();
  private readonly List <Vector2> _segmentPoints = new();
  private readonly Transform _globalTransform;
  private readonly Vector2[] _drawUv = { Vector2.Zero };
  private readonly Vector2[] _drawPoint = { Vector2.Zero };
  private Color[] _drawColor;

  public CatmullRom (List <Vector2> path, Transform globalTransform)
  {
    if (path.Count < 4) path = FixPartialPath (path);
    _globalTransform = globalTransform;
    Path = path;
  }

  public Vector2 GetSplinePoint (float length)
  {
    var lengthRemainder = length;
    var node = 0;

    foreach (var splineLength in _splineLengths.TakeWhile (splineLength => lengthRemainder - splineLength >= 0))
    {
      lengthRemainder -= splineLength;
      ++node;
    }

    return _GetSplinePoint (node < _splineLengths.Count ? node + lengthRemainder / _splineLengths[node] : _splineLengths.Count);
  }

  public void Draw (DrawPrimitive draw, GetLocalTransform getLocalTransform)
  {
    if (!DrawPath) return;

    foreach (var segmentPoint in _segmentPoints)
    {
      _drawPoint[0] = getLocalTransform() (segmentPoint);
      draw (_drawPoint, _drawColor, _drawUv);
    }
  }

  private Vector2 _GetSplinePoint (float t)
  {
    if (t >= _path.Count - 3) return _globalTransform (_path[_path.Count - 2]);

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
    _splinePoint.x = 0.5f * (_path[p0Idx].x * q1 + _path[p1Idx].x * q2 + _path[p2Idx].x * q3 + _path[p3Idx].x * q4);
    _splinePoint.y = 0.5f * (_path[p0Idx].y * q1 + _path[p1Idx].y * q2 + _path[p2Idx].y * q3 + _path[p3Idx].y * q4);

    return _globalTransform (_splinePoint);
  }

  private void OnPathChanged()
  {
    AddControlPoints();
    CalculateLength();
  }

  private void AddControlPoints()
  {
    _path.Insert (0, _path[0]);
    _path.Add (_path[_path.Count - 1]);
  }

  private void CalculateLength()
  {
    _splineLengths.Clear();
    _segmentPoints.Clear();
    var nextNode = 1;
    var splineLength = 0.0f;

    for (var t = 0.0f; t < _path.Count - 3; t += 0.1f)
    {
      _segmentPoints.Add (_GetSplinePoint (t));

      splineLength += _segmentPoints.Count > 1
        ? _segmentPoints[_segmentPoints.Count - 2].DistanceTo (_segmentPoints[_segmentPoints.Count - 1])
        : 0.0f;

      if (Mathf.FloorToInt (t) != nextNode) continue;

      _splineLengths.Add (splineLength);
      splineLength = 0.0f;
      ++nextNode;
    }

    _splineLengths.Add (splineLength);
    Length = _splineLengths.Sum();
  }

  private static List <Vector2> FixPartialPath (List <Vector2> path)
  {
    if (path.Count == 0)
    {
      for (var i = 0; i < 4; ++i)
      {
        path.Add (Vector2.Zero);
      }
    }

    if (path.Count == 1)
    {
      for (var i = 0; i < 3; ++i)
      {
        path.Add (path[0]);
      }
    }

    if (path.Count == 2)
    {
      path.Insert (0, path[0]);
      path.Add (path[2]);
    }

    if (path.Count == 3) path.Insert (1, path[1]);

    return path;
  }
}