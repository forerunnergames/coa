using System;

public interface ISignallingNode <in T> : INode where T : Enum
{
  public string NameOf (T signal);
}