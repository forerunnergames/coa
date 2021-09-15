using System.Collections.Generic;

public static class DictionaryExtensions
{
  public static KeyValuePair <TKey, TValue> GetEntry <TKey, TValue> (this IDictionary <TKey, TValue> dictionary, TKey key)
  {
    return new KeyValuePair <TKey, TValue> (key, dictionary[key]);
  }
}