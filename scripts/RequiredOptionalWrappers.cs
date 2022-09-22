using System;
using System.Collections.Generic;
using System.Linq;

public interface IRequiredOptionalWrapper <out T1, T2>
{
  public bool ComposeFromRequired (bool requiredResult, ref T2 data, Func <T1, T2, bool> activeFunc);
  public bool ComposeFromOptional (bool optionalResult, ref T2 data, Func <T1, T2, bool> activeFunc);
  public bool Compose (Func <T1, T2, bool> activeFunc);
  public IEnumerable <T1> Allowed();
  public IEnumerable <T1> Disallowed();
  public IEnumerable <T1> GetItems();
}

public abstract class AbstractRequiredOptionalWrapper <T1, T2> : IRequiredOptionalWrapper <T1, T2>
{
  protected readonly IEnumerable <T1> Items;
  protected AbstractRequiredOptionalWrapper (IEnumerable <T1> items) => Items = items.Distinct().ToArray();
  protected static IEnumerable <T1> Values() => Enum.GetValues (typeof (T1)).Cast <T1>();
  public abstract IEnumerable <T1> Allowed();
  public abstract IEnumerable <T1> Disallowed();
  public abstract bool ComposeFromRequired (bool requiredResult, ref T2 data, Func <T1, T2, bool> activeFunc);
  public abstract bool ComposeFromOptional (bool optionalResult, ref T2 data, Func <T1, T2, bool> activeFunc);
  public abstract bool Compose (ref T2 data, Func <T1, T2, bool> activeFunc);
  public IEnumerable <T1> GetItems() => Items;
  public override string ToString() => Tools.ToString (Items);
}

public class RequiredWrapper <T1, T2> : AbstractRequiredOptionalWrapper <T1, T2> where T1 : Enum
{
  private readonly IEnumerable <T1> _allowed;
  private readonly IEnumerable <T1> _disallowed;

  protected RequiredWrapper (IEnumerable <T1> items, IEnumerable <T1> allowed, IEnumerable <T1> disallowed) : base (items)
  {
    _allowed = allowed;
    _disallowed = disallowed;
  }

  public override IEnumerable <T1> Allowed() => _allowed;
  public override IEnumerable <T1> Disallowed() => _disallowed;

  public override bool ComposeFromRequired (bool requiredResult, ref T2 data, Func <T1, T2, bool> activeFunc)
  {
    var dataCopy = data;

    return Items.Aggregate (requiredResult, (result, item) => result && activeFunc.Invoke (item, dataCopy));
  }

  public override bool ComposeFromOptional (bool optionalResult, ref T2 data, Func <T1, T2, bool> activeFunc)
  {
    var dataCopy = data;

    return Items.Aggregate (optionalResult, (result, item) => result && activeFunc.Invoke (item, dataCopy));
  }

  public override bool Compose (ref T2 data, Func <T1, T2, bool> activeFunc) => ComposeFromRequired (true, ref data, activeFunc);
  public override string ToString() => "Required: " + base.ToString();
}

public class OptionalWrapper <T1, T2> : AbstractRequiredOptionalWrapper <T1, T2> where T1 : Enum
{
  private readonly IEnumerable <T1> _allowed;
  private readonly IEnumerable <T1> _disallowed;

  protected OptionalWrapper (IEnumerable <T1> items, IEnumerable <T1> allowed, IEnumerable <T1> disallowed) : base (items)
  {
    _allowed = allowed;
    _disallowed = disallowed;
  }

  public override IEnumerable <T1> Allowed() => _allowed;
  public override IEnumerable <T1> Disallowed() => _disallowed;
  public override bool ComposeFromRequired (bool requiredResult, ref T2 data, Func <T1, T2, bool> activeFunc) => requiredResult;

  public override bool ComposeFromOptional (bool optionalResult, ref T2 data, Func <T1, T2, bool> activeFunc)
  {
    var dataCopy = data;

    return Items.Aggregate (optionalResult, (result, item) => result || activeFunc.Invoke (item, dataCopy));
  }

  public override bool Compose (ref T2 data, Func <T1, T2, bool> activeFunc) => ComposeFromOptional (false, ref data, activeFunc);
  public override string ToString() => "Optional: " + base.ToString();
}

public class CompositeRequiredOptionalWrapper <T1, T2> : IRequiredOptionalWrapper <T1, T2> where T1 : Enum
{
  private readonly IEnumerable <IRequiredOptionalWrapper <T1, T2>> _wrappers;
  private readonly IEnumerable <T1> _allowed;
  private readonly IEnumerable <T1> _disallowed;

  protected CompositeRequiredOptionalWrapper (IEnumerable <IRequiredOptionalWrapper <T1, T2>> wrappers)
  {
    var wrappersList = wrappers.ToList();
    _wrappers = wrappersList;
    _allowed = wrappersList.SelectMany (x => x.Allowed()).Distinct();
    _disallowed = wrappersList.SelectMany (x => x.Disallowed().Except (_wrappers.SelectMany (y => y.Allowed()))).Distinct();
  }

  public bool ComposeFromRequired (bool requiredResult, ref T2 data, Func <T1, T2, bool> activeFunc)
  {
    var dataCopy = data;

    return _wrappers.Aggregate (requiredResult, (result, wrapper) => wrapper.ComposeFromRequired (result, ref dataCopy, activeFunc)) &&
           _disallowed.All (x => !activeFunc.Invoke (x, dataCopy));
  }

  public bool ComposeFromOptional (bool optionalResult, ref T2 data, Func <T1, T2, bool> activeFunc)
  {
    var dataCopy = data;

    return _wrappers.Aggregate (optionalResult, (result, wrapper) => wrapper.ComposeFromOptional (result, ref dataCopy, activeFunc)) &&
           _disallowed.All (x => !activeFunc.Invoke (x, dataCopy));
  }

  public bool Compose (ref T2 data, Func <T1, T2, bool> activeFunc)
  {
    var dataCopy = data;

    return _wrappers.First().Compose (ref data, activeFunc) && _disallowed.All (x => !activeFunc.Invoke (x, dataCopy));
  }

  public IEnumerable <T1> Allowed() => _allowed;
  public IEnumerable <T1> Disallowed() => _disallowed;
  public IEnumerable <T1> GetItems() => _wrappers.SelectMany (x => x.GetItems());
  public override string ToString() => "Composite: " + Tools.ToString (_wrappers);
}