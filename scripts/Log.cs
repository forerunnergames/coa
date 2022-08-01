using System;
using System.Runtime.CompilerServices;
using Godot;
using Path = System.IO.Path;

public class Log
{
  public Level CurrentLevel { get; set; } = Level.Info;
  private readonly string _name;

  public enum Level
  {
    Off,
    Warn,
    Info,
    Debug,
    All
  }

  public Log ([CallerFilePath] string name = "") => _name = Path.GetFileNameWithoutExtension (name);
  public bool All (string message) => CurrentLevel > Level.Debug && Print (message, Level.All);
  public bool Debug (string message) => CurrentLevel > Level.Info && Print (message, Level.Debug);
  public bool Info (string message) => CurrentLevel > Level.Warn && Print (message, Level.Info);
  public bool Warn (string message) => CurrentLevel > Level.Off && Print (message, Level.Warn);

  private bool Print (string message, Level level)
  {
    GD.Print ($"{_name}{(_name.Empty() ? "" : " ")}{level}: {DateTime.Now:HH:mm:ss:ffff} ", message);

    return true;
  }
}