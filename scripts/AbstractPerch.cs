using System.Collections.Generic;
using System.Linq;
using Godot;
using static Tools;

public abstract class AbstractPerch : IPerchable
{
  public string Name { get; }
  public PerchableDrawPrefs DrawPrefs { get; }
  public bool Disabled { get; set; }
  public bool FlippedHorizontally { get; private set; }
  public bool FlippedVertically { get; private set; }

  public Vector2 GlobalOrigin
  {
    get => _globalOrigin;
    set
    {
      if (_globalOrigin != value)
      {
        UpdateGlobalPerchableAreas (value);
      }

      _globalOrigin = value;
    }
  }

  protected string PerchableAreasInGlobalSpaceString;
  private readonly List <Rect2> _perchableAreasInLocalSpace;
  private readonly List <Rect2> _perchableAreasInGlobalSpace;
  private Rect2 _drawableRect;
  private Vector2 _globalOrigin;
  private readonly Vector2 _globalScale;
  private readonly Vector2 _localScale;
  private readonly float _positionEpsilon;

  protected AbstractPerch (string name, Vector2 localScale, Vector2 globalScale, Vector2 globalOrigin, PerchableDrawPrefs drawPrefs,
    List <Rect2> perchableAreasInLocalSpace, float positionEpsilon)
  {
    Name = name;
    _localScale = localScale;
    _globalScale = globalScale;
    _perchableAreasInLocalSpace = perchableAreasInLocalSpace;
    _perchableAreasInGlobalSpace = new List <Rect2>();
    _positionEpsilon = positionEpsilon;
    DrawPrefs = drawPrefs;
    GlobalOrigin = globalOrigin;
    UpdateGlobalPerchableAreas (GlobalOrigin);
  }

  public void Draw (DrawRect draw, Vector2 perchPoint, GetLocalTransform getLocalTransform)
  {
    if (DrawPrefs.DrawAreas)
    {
      foreach (var area in _perchableAreasInGlobalSpace)
      {
        _drawableRect.Position = getLocalTransform() (area.Position);
        _drawableRect.Size = area.Size / _localScale;
        draw (_drawableRect, DrawPrefs.AreasColor, DrawPrefs.AreasFilled);
      }
    }

    if (!DrawPrefs.DrawPerchPoint) return;

    _drawableRect.Position = getLocalTransform() (perchPoint);
    _drawableRect.Size = _globalScale / _localScale;
    draw (_drawableRect, DrawPrefs.PerchPointColor, DrawPrefs.PerchPointFilled);
  }

  public Vector2 RandomPoint (RandomNumberGenerator rng) =>
    RandomPointIn (_perchableAreasInGlobalSpace.ElementAt (rng.RandiRange (0, _perchableAreasInGlobalSpace.Count - 1)), rng,
      _globalScale);

  // @formatter:off
  public float DistanceFrom (Vector2 globalPosition) => globalPosition.DistanceTo (GlobalOrigin);
  public bool Contains (Vector2 globalPosition) => _perchableAreasInGlobalSpace.Any (x => AlmostHasPoint (x, globalPosition, _positionEpsilon));
  public virtual bool HasParentName (string name) => false;
  public bool Is (string name, Vector2 globalOrigin) => Name == name && AreAlmostEqual (GlobalOrigin, globalOrigin, _positionEpsilon);
  // ReSharper disable once MemberCanBePrivate.Global
  public bool Equals (IPerchable other) => !ReferenceEquals (null, other) && (ReferenceEquals (this, other) || Is (other.Name, other.GlobalOrigin));
  public override bool Equals (object obj) => !ReferenceEquals (null, obj) && (ReferenceEquals (this, obj) || obj.GetType() == GetType() && Equals ((IPerchable)obj));
  // @formatter:on

  public override string ToString() =>
    $"[Name: [{Name}], Global Origin: [{GlobalOrigin}], Global Scale: [{_globalScale}], Local Scale: [{_localScale}], " +
    $"Disabled: [{Disabled}], FlippedHorizontally: {FlippedHorizontally}, " +
    $"FlippedVertically: {FlippedVertically}, Global Areas:\n{PerchableAreasInGlobalSpaceString}]";

  public override int GetHashCode()
  {
    unchecked
    {
      return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ GlobalOrigin.GetHashCode();
    }
  }

  public void FlipHorizontally()
  {
    for (var i = 0; i < _perchableAreasInLocalSpace.Count; ++i)
    {
      var area = _perchableAreasInLocalSpace[i];

      area.Position = new Vector2 (-_perchableAreasInLocalSpace[i].Position.x - _perchableAreasInLocalSpace[i].Size.x,
        _perchableAreasInLocalSpace[i].Position.y);

      _perchableAreasInLocalSpace[i] = area;
    }

    UpdateGlobalPerchableAreas (GlobalOrigin);
    FlippedHorizontally = !FlippedHorizontally;
  }

  public void FlipVertically()
  {
    for (var i = 0; i < _perchableAreasInLocalSpace.Count; ++i)
    {
      var area = _perchableAreasInLocalSpace[i];

      area.Position = new Vector2 (-_perchableAreasInLocalSpace[i].Position.y - _perchableAreasInLocalSpace[i].Size.y,
        _perchableAreasInLocalSpace[i].Position.y);

      _perchableAreasInLocalSpace[i] = area;
    }

    UpdateGlobalPerchableAreas (GlobalOrigin);
    FlippedVertically = !FlippedVertically;
  }

  private void UpdateGlobalPerchableAreas (Vector2 globalOrigin)
  {
    _perchableAreasInGlobalSpace.Clear();
    PerchableAreasInGlobalSpaceString = "";
    var last = _perchableAreasInLocalSpace.Count - 1;

    for (var i = 0; i < _perchableAreasInLocalSpace.Count; ++i)
    {
      var perch = _perchableAreasInLocalSpace[i];
      perch.Position *= _globalScale / _localScale;
      perch.Position += globalOrigin;
      perch.Size *= _globalScale / _localScale;
      _perchableAreasInGlobalSpace.Add (perch);
      PerchableAreasInGlobalSpaceString += $"        {(i > 0 ? " " : "[")}[Area {i}: {perch}]{(i < last ? ",\n" : "]")}";
    }
  }
}