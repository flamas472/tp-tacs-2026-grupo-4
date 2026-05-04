namespace Figuritas.Shared.Utils;

public static class StringExtension
{
    
     /* 
     Checks if origin contains all the words contained in filter in any order.
     eg.: filter = "Hola Mundo"; origin = "Mundo Hola Pan"; is true
     eg.: filer = "Hola Mundo"; origin = "Hola Pan"; is false
      */
    public static bool AllWordsAreContainedBy(this string? filter, string origin)
    {
        return string.IsNullOrEmpty(filter) || filter.Split(" ").All(words => origin.Contains(words, StringComparison.OrdinalIgnoreCase));
    }
}