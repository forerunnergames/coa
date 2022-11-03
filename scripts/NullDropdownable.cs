using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public class NullDropdownable : AbstractDropdownable
{
  // ReSharper disable once ExplicitCallerInfoArgument
  public NullDropdownable ([CallerFilePath] string name = "") : base (new Dictionary <string, int>(), name) { }
  public override Task Drop() => Task.CompletedTask;
}