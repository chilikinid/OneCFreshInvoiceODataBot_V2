using System.Text.RegularExpressions;

namespace OneCFreshInvoiceODataBot.Services;

public static class UrlHelper
{
    public static string GetFreshId(string url)
    {
        // Ищет число, окруженное косыми чертами (слешами)
        Match match = Regex.Match(url, @"/(\d+)/");

        return match.Success ? match.Groups[1].Value : string.Empty;
    }

}
