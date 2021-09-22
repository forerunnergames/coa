using System;
using System.Linq;

public static class BooleanOperators
{
  // @formatter:off
  public static IBooleanOperator And (params Func <bool>[] ands) => new CompositeAndOperator (ands);
  public static IBooleanOperator Or (params Func <bool>[] ors) => new CompositeOrOperator (ors);
  public static IBooleanOperator[] _ (params IBooleanOperator[] operators) => new IBooleanOperator[] { new CompositeBooleanOperator (operators) };
  // @formatter:on
}

public interface IBooleanOperator
{
  public bool Compose (bool leftOperandResult);
  public bool Compose();
}

public class CompositeAndOperator : IBooleanOperator
{
  private readonly Func <bool>[] _ands;
  public CompositeAndOperator (Func <bool>[] ands) => _ands = ands;
  public bool Compose (bool leftOperandResult) => _ands.Aggregate (leftOperandResult, (result, and) => result && and());
  public bool Compose() => Compose (true);
  public override string ToString() => Tools.ToString (_ands, f: and => "And: " + and());
}

public class CompositeOrOperator : IBooleanOperator
{
  private readonly Func <bool>[] _ors;
  public CompositeOrOperator (Func <bool>[] ors) => _ors = ors;
  public bool Compose (bool leftOperandResult) => _ors.Aggregate (leftOperandResult, (result, or) => result || or());
  public bool Compose() => Compose (false);
  public override string ToString() => Tools.ToString (_ors, f: or => "Or: " + or());
}

public class CompositeBooleanOperator : IBooleanOperator
{
  private readonly IBooleanOperator[] _operators;
  public CompositeBooleanOperator (IBooleanOperator[] operators) => _operators = operators;

  public bool Compose (bool leftOperandResult) =>
    _operators.Aggregate (leftOperandResult, (result, @operator) => @operator.Compose (result));

  public bool Compose()
  {
    var @operator = _operators[0];
    CompositeBooleanOperator innermostComposite = null;

    while (@operator is CompositeBooleanOperator composite)
    {
      innermostComposite = composite;
      @operator = composite._operators[0];
    }

    return innermostComposite?.Compose (@operator.Compose()) ?? Compose (@operator.Compose());
  }

  public override string ToString() => Tools.ToString (_operators);
}