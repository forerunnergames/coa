using System.Threading.Tasks;
using Godot;

public class PhysicsBodyDropdownable : AbstractDropdownable
{
  private readonly PhysicsBody2D _droppedThroughNode;
  private readonly CollisionObject2D _droppingNode;

  public PhysicsBodyDropdownable (PhysicsBody2D droppedThroughNode, CollisionObject2D droppingNode)
  {
    _droppedThroughNode = droppedThroughNode;
    _droppingNode = droppingNode;
  }

  public override async Task Drop()
  {
    _IsDropping = true;
    Log.Info ($"Dropping down through [{_droppedThroughNode.Name}].");
    _droppedThroughNode.SetCollisionMaskBit (0, false);
    _droppingNode.SetCollisionMaskBit (1, false);
    await _droppingNode.ToSignal (_droppingNode.GetTree(), "idle_frame");
    _droppedThroughNode.SetCollisionMaskBit (0, true);
    _droppingNode.SetCollisionMaskBit (1, true);
    _IsDropping = false;
  }
}