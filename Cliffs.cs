using Godot;
using static Tools;

public class Cliffs : Area2D
{
  private Area2D _playerArea;
  private Rect2 _playerRect;
  private Rect2 _cliffsRect;
  private Vector2 _playerExtents;
  private Vector2 _cliffsExtents;
  private Vector2 _playerPosition;
  private Vector2 _cliffsPosition;
  private CollisionShape2D _cliffsCollider;
  private bool _isPlayerIntersectingCliffs;
  private AudioStreamPlayer _ambiencePlayer;
  private AudioStreamPlayer _musicPlayer;
  private TileMap _iceTileMap;
  private Vector2 _topLeft;
  private Vector2 _bottomRight;
  private Vector2 _topRight;
  private Vector2 _bottomLeft;

  public override void _Ready()
  {
    _ambiencePlayer = GetNode <AudioStreamPlayer> ("../AmbiencePlayer");
    _ambiencePlayer.Stream = ResourceLoader.Load <AudioStream> ("res://ambience_summer.wav");
    _musicPlayer = GetNode <AudioStreamPlayer> ("../MusicPlayer");
    _musicPlayer.Stream = ResourceLoader.Load <AudioStream> ("res://music2_trimmed.wav");
    LoopAudio (_ambiencePlayer.Stream);
    LoopAudio (_musicPlayer.Stream);
    _cliffsCollider = GetNode <CollisionShape2D> ("CollisionShape2D");
    _cliffsPosition = _cliffsCollider.GlobalPosition;
    _iceTileMap = GetNode <TileMap> ("Ice");

    // TODO Remove.
    for (var i = 1; i < 11; ++i)
    {
      if (Name != "Upper Cliffs") return;

      GetNode <AnimatedSprite> ("Waterfall/waterfall " + i).Play();
    }

    // TODO Remove.
    for (var i = 1; i < 4; ++i)
    {
      if (Name != "Upper Cliffs") return;

      GetNode <AnimatedSprite> ("Waterfall/waterfall mist " + i).Play();
    }
  }

  // ReSharper disable once UnusedMember.Global
  public void _on_cliffs_area_entered (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _playerArea = area;
    _isPlayerIntersectingCliffs = true;
  }

  // ReSharper disable once UnusedMember.Global
  public void _on_cliffs_area_exited (Area2D area)
  {
    if (!area.IsInGroup ("Player")) return;

    _playerArea = area;
    _isPlayerIntersectingCliffs = false;
    GetNode <Player> ("../Player").IsInCliffs = false;
  }

  public override void _Process (float delta)
  {
    Update();
    Sounds();

    if (!_isPlayerIntersectingCliffs) return;

    // @formatter:off

    _playerExtents = GetExtents (_playerArea);
    _cliffsExtents = GetExtents (this);
    _playerPosition = _playerArea.GlobalPosition;
    _cliffsPosition = _cliffsCollider.GlobalPosition;

    _playerRect.Position = _playerPosition - _playerExtents;
    _playerRect.Size = _playerExtents * 2;
    _cliffsRect.Position = _cliffsPosition - _cliffsExtents;
    _cliffsRect.Size = _cliffsExtents * 2;

    GetNode <Player> ("../Player").IsInCliffs = _isPlayerIntersectingCliffs && _cliffsRect.Encloses (_playerRect);

    _topLeft = _playerArea.GlobalPosition - _playerExtents;
    _bottomRight = _playerArea.GlobalPosition + _playerExtents;
    _topRight.x = _playerArea.GlobalPosition.x + _playerExtents.x;
    _topRight.y = _playerArea.GlobalPosition.y - _playerExtents.y;
    _bottomLeft.x = _playerArea.GlobalPosition.x - _playerExtents.x;
    _bottomLeft.y = _playerArea.GlobalPosition.y + _playerExtents.y;

    GetNode <Player> ("../Player").IsTouchingCliffIce = _iceTileMap.Visible &&
                                                        (IsIntersectingAnyTile (_topLeft, _iceTileMap) ||
                                                        IsIntersectingAnyTile (_bottomRight, _iceTileMap) ||
                                                        IsIntersectingAnyTile (_topRight, _iceTileMap) ||
                                                        IsIntersectingAnyTile (_bottomLeft, _iceTileMap));

    // @formatter:on
  }

  private void Sounds()
  {
    if (!_ambiencePlayer.Playing) _ambiencePlayer.Play();
    if (!_musicPlayer.Playing) _musicPlayer.Play();
  }

  private static Vector2 GetExtents (Area2D area)
  {
    var collisionShape = area.GetNode <CollisionShape2D> ("CollisionShape2D");
    var collisionRect = collisionShape.Shape as RectangleShape2D;

    // ReSharper disable once InvertIf
    if (collisionRect == null)
    {
      OnWrongCollisionShape (area, collisionShape.Shape);

      return Vector2.Zero;
    }

    return collisionRect.Extents;
  }

  private static void OnWrongCollisionShape (Area2D area, Shape2D shape)
  {
    GD.PrintErr (area.Name + " collision shape must be a " + typeof (RectangleShape2D) + ", not a " + shape.GetType());
  }
}