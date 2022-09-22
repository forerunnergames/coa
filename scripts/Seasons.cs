using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using static Inputs;
using static Tools;
using Input = Inputs.Input;

public class Seasons
{
  public Season CurrentSeason { get; private set; }
  private readonly Color _initialClearColor;

  public enum Season
  {
    Summer,
    Winter
  }

  private readonly AudioStreamPlayer _ambiencePlayer;
  private readonly AudioStreamPlayer _musicPlayer;
  private readonly Dictionary <Season, AudioStream> _music = new();
  private readonly Dictionary <Season, AudioStream> _ambience = new();
  private readonly Dictionary <Season, float> _musicVolumes = new();
  private readonly Dictionary <Season, float> _ambienceVolumes = new();
  private readonly List <Waterfall> _waterfalls;
  private float _currentSeasonFadeInTimeSeconds;
  private float _currentSeasonFadeOutTimeSeconds;
  private Color _clearColor;
  private bool _seasonChangeInProgress;
  private Season _newSeason;
  private Season _oldSeason;
  private bool _fadeIn;
  private bool _skipFade;
  private bool _isMusicPlaying;
  private readonly Log _log;

  public Seasons (SceneTree sceneTree, Season initialSeason, Color initialClearColor, bool isMusicPlayingInitially, Log.Level logLevel,
    [CallerFilePath] string name = "")
  {
    // ReSharper disable once ExplicitCallerInfoArgument
    _log = new Log (name) { CurrentLevel = logLevel };
    _initialClearColor = initialClearColor;
    _clearColor = initialClearColor;
    _isMusicPlaying = isMusicPlayingInitially;
    _waterfalls = GetNodesInGroups <Waterfall> (sceneTree, "Waterfall", "Parent");
    _ambience.Add (Season.Summer, ResourceLoader.Load <AudioStream> ("res://assets/sounds/ambience_summer.wav"));
    _ambience.Add (Season.Winter, ResourceLoader.Load <AudioStream> ("res://assets/sounds/ambience_winter.wav"));
    _ambienceVolumes.Add (Season.Summer, -10);
    _ambienceVolumes.Add (Season.Winter, -20);
    _music.Add (Season.Winter, ResourceLoader.Load <AudioStream> ("res://assets/music/music6.wav"));
    _music.Add (Season.Summer, ResourceLoader.Load <AudioStream> ("res://assets/music/music5.wav"));
    _musicVolumes.Add (Season.Winter, -15);
    _musicVolumes.Add (Season.Summer, -25);
    _ambiencePlayer = sceneTree.CurrentScene.GetNode <AudioStreamPlayer> ("Audio Players/Ambience");
    _musicPlayer = sceneTree.CurrentScene.GetNode <AudioStreamPlayer> ("Audio Players/Music");
    InitializeSeasons (sceneTree, initialSeason);
  }

  public void OnInput (InputEvent @event)
  {
    if (WasPressed (Input.Season, @event) && !_seasonChangeInProgress) NextSeason();
  }

  public void ToggleMusic() => _isMusicPlaying = !_isMusicPlaying;

  public void Update (SceneTree sceneTree, CanvasItem canvas, float delta)
  {
    if (!_seasonChangeInProgress)
    {
      _fadeIn = false;
      _skipFade = false;

      return;
    }

    if (!_skipFade && !_fadeIn)
    {
      _currentSeasonFadeOutTimeSeconds += delta;
      canvas.Modulate = canvas.Modulate.LinearInterpolate (Colors.Black, _currentSeasonFadeOutTimeSeconds * 0.2f);
      _clearColor = _clearColor.LinearInterpolate (Colors.Black, _currentSeasonFadeOutTimeSeconds * 0.2f);
      VisualServer.SetDefaultClearColor (_clearColor);
      _fadeIn = AreAlmostEqual (canvas.Modulate, Colors.Black, 0.01f) && AreAlmostEqual (_clearColor, Colors.Black, 0.1f);

      return;
    }

    CurrentSeason = _newSeason;

    if (_skipFade || AreAlmostEqual (canvas.Modulate, Colors.Black, 0.01f) && AreAlmostEqual (_clearColor, Colors.Black, 0.1f))
    {
      SetGroupVisible (Enum.GetName (typeof (Season), _oldSeason), false, sceneTree);
      SetGroupVisible (Enum.GetName (typeof (Season), _newSeason), true, sceneTree);
      _musicPlayer.Stream = _music[_newSeason];
      _ambiencePlayer.Stream = _ambience[_newSeason];
      _musicPlayer.VolumeDb = _musicVolumes[_newSeason];
      _ambiencePlayer.VolumeDb = _ambienceVolumes[_newSeason];
      LoopAudio (_musicPlayer.Stream);
      LoopAudio (_ambiencePlayer.Stream);
      _ambiencePlayer.Play();
      _waterfalls.ForEach (x => x.OnSeason (_newSeason));
    }

    if (!_skipFade)
    {
      _currentSeasonFadeInTimeSeconds += delta;
      canvas.Modulate = canvas.Modulate.LinearInterpolate (Colors.White, _currentSeasonFadeInTimeSeconds * 0.1f);
      _clearColor = _clearColor.LinearInterpolate (_initialClearColor, _currentSeasonFadeInTimeSeconds * 0.1f);
      VisualServer.SetDefaultClearColor (_clearColor);
    }

    _seasonChangeInProgress = !AreAlmostEqual (canvas.Modulate, Colors.White, 0.01f) ||
                              !AreAlmostEqual (_clearColor, _initialClearColor, 0.1f);

    if (_seasonChangeInProgress) return;

    if (_isMusicPlaying) _musicPlayer.Play();
    _log.Info ($"Current season is now: {CurrentSeason}");
  }

  private void InitializeSeasons (SceneTree sceneTree, Season initialSeason)
  {
    VisualServer.SetDefaultClearColor (_initialClearColor);

    Enum.GetValues (typeof (Season)).Cast <Season>().ToList()
      .ForEach (x => SetGroupVisible (Enum.GetName (typeof (Season), x), false, sceneTree));

    ChangeSeasonTo (initialSeason);
    _skipFade = true;
  }

  private void NextSeason() => ChangeSeasonTo (CurrentSeason.Next());

  private void ChangeSeasonTo (Season season)
  {
    _oldSeason = CurrentSeason;
    _newSeason = season;
    _seasonChangeInProgress = true;
    _currentSeasonFadeOutTimeSeconds = 0.0f;
    _currentSeasonFadeInTimeSeconds = 0.0f;
    _ambiencePlayer.Stop();
    _musicPlayer.Stop();
  }
}