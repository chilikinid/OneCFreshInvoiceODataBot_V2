namespace OneCFreshInvoiceODataBot.Services;

public static class PathResolver
{
    public static string ResolveFromProjectRoot(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        var current = Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(current, path));
    }
}
