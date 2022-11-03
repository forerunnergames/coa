using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;

public class PhysicsBodyDropdownable : AbstractDropdownable
{
  private readonly PhysicsBody2D _droppingThroughBody;
  private readonly CollisionObject2D _droppingNode;

  // ReSharper disable once ExplicitCallerInfoArgument
  public PhysicsBodyDropdownable (PhysicsBody2D droppingThroughBody, CollisionObject2D droppingNode,
    Dictionary <string, int> droppingThroughNamesToDropdownFrames, [CallerFilePath] string name = "") : base (
    droppingThroughNamesToDropdownFrames, name)
  {
    _droppingThroughBody = droppingThroughBody;
    _droppingNode = droppingNode;
  }

  public override async Task Drop()
  {
    _IsDropping = true;
    Log.Info ($"Dropping down through [{_droppingThroughBody.Name}].");
    _droppingThroughBody.SetCollisionMaskBit (0, false);
    _droppingNode.SetCollisionMaskBit (1, false);

    await IdleFrames (_droppingThroughBody.Name, _droppingNode);

    _droppingThroughBody.SetCollisionMaskBit (0, true);
    _droppingNode.SetCollisionMaskBit (1, true);
    _IsDropping = false;
  }
}