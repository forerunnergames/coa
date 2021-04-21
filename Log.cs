using System;
using Godot;

public static class Log
{
  public static Level CurrentLevel { get; set; } = Level.Info;

  public enum Level
  {
    Off,
    Warn,
    Info,
    Debug,
    All
  }

  public static bool All (string message) => CurrentLevel > Level.Debug && Print (message, Level.All);
  public static bool Debug (string message) => CurrentLevel > Level.Info && Print (message, Level.Debug);
  public static bool Info (string message) => CurrentLevel > Level.Warn && Print (message, Level.Info);
  public static bool Warn (string message) => CurrentLevel > Level.Off && Print (message, Level.Warn);

  private static bool Print (string message, Level level)
  {
    GD.Print ($"{level}: {DateTime.Now:HH:mm:ss:ffff} ", message);
    return true;
  }
}