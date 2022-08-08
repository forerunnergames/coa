using System.Threading.Tasks;

public class NullDropdownable : AbstractDropdownable
{
  public override Task Drop() => Task.CompletedTask;
}