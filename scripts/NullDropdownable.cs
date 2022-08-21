using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public class NullDropdownable : AbstractDropdownable
{
  // ReSharper disable once ExplicitCallerInfoArgument
  public NullDropdownable ([CallerFilePath] string name = "") : base (name) { }
  public override Task Drop() => Task.CompletedTask;
}