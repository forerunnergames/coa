using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public abstract class AbstractDropdownable : IDropdownable
{
  // ReSharper disable once ExplicitCallerInfoArgument
  protected AbstractDropdownable ([CallerFilePath] string name = "") => Log = new Log (name);

  // ReSharper disable once InconsistentNaming
  protected volatile bool _IsDropping;
  protected readonly Log Log;
  public abstract Task Drop();
  public bool IsDropping() => _IsDropping;
}