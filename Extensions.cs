using System;

public static class Extensions
{
  public static T Next <T> (this T t) where T : struct
  {
    if (!typeof (T).IsEnum) throw new ArgumentException ($"{typeof (T).FullName} is not of type Enum.");

    var values = (T[])Enum.GetValues (t.GetType());
    var index = Array.IndexOf (values, t) + 1;

    return index < values.Length ? values[index] : values[0];
  }
}