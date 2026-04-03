using System.Reflection;

namespace HomeStoq.Contracts;

public static class PathHelper
{
    private static readonly string _repoRoot = typeof(PathHelper).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(a => a.Key == "RepoRoot")
        .Value!;

    public static string RepoRoot => _repoRoot;

    public static string ResolveConfigIni() => Path.Combine(_repoRoot, "config.ini");

    public static string ResolveEnvFile() => Path.Combine(_repoRoot, ".env");

    public static string ResolveDatabasePath() => Path.Combine(_repoRoot, "data", "homestoq.db");
}
