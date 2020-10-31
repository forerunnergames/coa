using Godot;
using System;

public class Icon : Sprite
{
  // Called when the node enters the scene tree for the first time.
  public override void _Ready()
  {
    GD.Print("Hello, World!");
  }

  // Called every frame. 'delta' is the elapsed time since the previous frame.
  public override void _Process(float delta)
  {
  }
}