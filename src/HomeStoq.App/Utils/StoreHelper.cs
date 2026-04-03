using System;
using System.IO;
using System.Linq;

namespace HomeStoq.App.Utils;

public static class StoreHelper
{
    public static string ResolveStoreName(string fileName, string language)
    {
        var unknownFallback = language == "Swedish" ? "Okänd" : "Unknown";

        if (string.IsNullOrWhiteSpace(fileName)) return unknownFallback;

        var name = Path.GetFileNameWithoutExtension(fileName).Trim();

        name = System.Text.RegularExpressions.Regex.Replace(name, @"[\s_\-]+", " ");

        name = System.Text.RegularExpressions.Regex.Replace(name, @"\d{4}[-_]\d{2}[-_]\d{2}.*$", "").Trim();

        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+\d+$", "").Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
            return unknownFallback;

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 6)
            name = string.Join(" ", words.Take(6));

        return name;
    }
}
