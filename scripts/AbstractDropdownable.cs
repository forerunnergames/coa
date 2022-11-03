using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;

public abstract class AbstractDropdownable : IDropdownable
{
  // ReSharper disable once InconsistentNaming
  protected volatile bool _IsDropping;
  protected readonly Log Log;
  private readonly Dictionary <string, int> _droppingThroughNamesToDropdownFrames;

  // ReSharper disable once ExplicitCallerInfoArgument
  protected AbstractDropdownable (Dictionary <string, int> droppingThroughNamesToDropdownFrames, [CallerFilePath] string name = "")
  {
    Log = new Log (name);
    _droppingThroughNamesToDropdownFrames = droppingThroughNamesToDropdownFrames;
  }

  public abstract Task Drop();
  public bool IsDropping() => _IsDropping;

  protected async Task IdleFrames (string droppingThroughNodeName, Node droppingNode)
  {
    var frames = GetDropDownFrames (droppingThroughNodeName);
    for (var i = 0; i < frames; ++i) await droppingNode.ToSignal (droppingNode.GetTree(), "idle_frame");
  }

  private int GetDropDownFrames (string droppingThroughNodeName) =>
    _droppingThroughNamesToDropdownFrames.ContainsKey (droppingThroughNodeName)
      ? _droppingThroughNamesToDropdownFrames[droppingThroughNodeName]
      : 1;
}