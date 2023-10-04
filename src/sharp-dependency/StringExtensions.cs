namespace sharp_dependency;

public static class StringExtensions
{
    private static readonly string[] Separator = { "\r\n", "\r", "\n" };

    //https://stackoverflow.com/a/25196003
    public static IEnumerable<string> GetLines(this string str, bool removeEmptyLines = false)
    {
        return str.Split(Separator, removeEmptyLines ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
    }
}