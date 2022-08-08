using System.Threading.Tasks;

public interface IDropdownable
{
  public Task Drop();
  public bool IsDropping();
}