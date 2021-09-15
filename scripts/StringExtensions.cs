public static class StringExtensions
{
  public static string NullIfEmpty (this string s) => string.IsNullOrEmpty (s) ? null : s;
}