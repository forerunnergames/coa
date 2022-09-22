using System;
using System.Collections.Generic;
using System.Linq;

public interface IRequiredOptionalWrapper <out T>
{
  public bool ComposeFromRequired (bool requiredResult, Func <T, bool> activeFunc);
  public bool ComposeFromOptional (bool optionalResult, Func <T, bool> activeFunc);
  public bool Compose (Func <T, bool> activeFunc);
  public IEnumerable <T> Allowed();
  public IEnumerable <T> Disallowed();
  public IEnumerable <T> GetItems();
}

public abstract class AbstractRequiredOptionalWrapper <T> : IRequiredOptionalWrapper <T>
{
  protected readonly IEnumerable <T> Items;
  protected AbstractRequiredOptionalWrapper (IEnumerable <T> items) => Items = items.Distinct().ToArray();
  protected static IEnumerable <T> Values() => Enum.GetValues (typeof (T)).Cast <T>();
  public abstract IEnumerable <T> Allowed();
  public abstract IEnumerable <T> Disallowed();
  public abstract bool ComposeFromRequired (bool requiredResult, Func <T, bool> activeFunc);
  public abstract bool ComposeFromOptional (bool optionalResult, Func <T, bool> activeFunc);
  public abstract bool Compose (Func <T, bool> activeFunc);
  public IEnumerable <T> GetItems() => Items;
  public override string ToString() => Tools.ToString (Items);
}

public class RequiredWrapper <T> : AbstractRequiredOptionalWrapper <T>
{
  private readonly IEnumerable <T> _allowed;
  private readonly IEnumerable <T> _disallowed;

  protected RequiredWrapper (IEnumerable <T> items, IEnumerable <T> allowed, IEnumerable <T> disallowed) : base (items)
  {
    _allowed = allowed;
    _disallowed = disallowed;
  }

  public override IEnumerable <T> Allowed() => _allowed;
  public override IEnumerable <T> Disallowed() => _disallowed;

  public override bool ComposeFromRequired (bool requiredResult, Func <T, bool> activeFunc) =>
    Items.Aggregate (requiredResult, (result, item) => result && activeFunc.Invoke (item));

  public override bool ComposeFromOptional (bool optionalResult, Func <T, bool> activeFunc) =>
    Items.Aggregate (optionalResult, (result, item) => result && activeFunc.Invoke (item));

  public override bool Compose (Func <T, bool> activeFunc) => ComposeFromRequired (true, activeFunc);
  public override string ToString() => "Required: " + base.ToString();
}

public class OptionalWrapper <T> : AbstractRequiredOptionalWrapper <T>
{
  private readonly IEnumerable <T> _allowed;
  private readonly IEnumerable <T> _disallowed;

  protected OptionalWrapper (IEnumerable <T> items, IEnumerable <T> allowed, IEnumerable <T> disallowed) : base (items)
  {
    _allowed = allowed;
    _disallowed = disallowed;
  }

  public override IEnumerable <T> Allowed() => _allowed;
  public override IEnumerable <T> Disallowed() => _disallowed;
  public override bool ComposeFromRequired (bool requiredResult, Func <T, bool> activeFunc) => requiredResult;

  public override bool ComposeFromOptional (bool optionalResult, Func <T, bool> activeFunc) =>
    Items.Aggregate (optionalResult, (result, item) => result || activeFunc.Invoke (item));

  public override bool Compose (Func <T, bool> activeFunc) => ComposeFromOptional (false, activeFunc);
  public override string ToString() => "Optional: " + base.ToString();
}

public class CompositeRequiredOptionalWrapper <T> : IRequiredOptionalWrapper <T>
{
  private readonly IEnumerable <IRequiredOptionalWrapper <T>> _wrappers;
  private readonly IEnumerable <T> _allowed;
  private readonly IEnumerable <T> _disallowed;

  protected CompositeRequiredOptionalWrapper (IEnumerable <IRequiredOptionalWrapper <T>> wrappers)
  {
    var wrappersList = wrappers.ToList();
    _wrappers = wrappersList;
    _allowed = wrappersList.SelectMany (x => x.Allowed()).Distinct();
    _disallowed = wrappersList.SelectMany (x => x.Disallowed().Except (_wrappers.SelectMany (y => y.Allowed()))).Distinct();
  }

  public bool ComposeFromRequired (bool requiredResult, Func <T, bool> activeFunc) =>
    _wrappers.Aggregate (requiredResult, (result, wrapper) => wrapper.ComposeFromRequired (result, activeFunc)) &&
    _disallowed.All (x => !activeFunc.Invoke (x));

  public bool ComposeFromOptional (bool optionalResult, Func <T, bool> activeFunc) =>
    _wrappers.Aggregate (optionalResult, (result, wrapper) => wrapper.ComposeFromOptional (result, activeFunc)) &&
    _disallowed.All (x => !activeFunc.Invoke (x));

  public bool Compose (Func <T, bool> activeFunc) =>
    _wrappers.First().Compose (activeFunc) && _disallowed.All (x => !activeFunc.Invoke (x));

  public IEnumerable <T> Allowed() => _allowed;
  public IEnumerable <T> Disallowed() => _disallowed;
  public IEnumerable <T> GetItems() => _wrappers.SelectMany (x => x.GetItems());
  public override string ToString() => "Composite: " + Tools.ToString (_wrappers);
}