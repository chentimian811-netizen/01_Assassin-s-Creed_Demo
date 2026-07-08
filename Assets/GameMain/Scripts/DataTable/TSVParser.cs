using System.Collections.Generic;

public static class TSVParser
{
    public static List<string[]> Parse(string text)
    {
        var result = new List<string[]>();
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;
            result.Add(trimmed.Split('\t'));
        }
        return result;
    }
}
