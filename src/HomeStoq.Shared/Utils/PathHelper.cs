using System.Reflection;

namespace HomeStoq.Shared.Utils;

public static class PathHelper
{
    private static readonly string _repoRoot = DetectRepoRoot();

    public static string RepoRoot => _repoRoot;

    private static string DetectRepoRoot()
    {
        // Check for Docker container (/.dockerenv file is present in containers)
        if (File.Exists("/.dockerenv"))
            return "/app";

        // Use assembly metadata for local development
        return typeof(PathHelper)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RepoRoot")
            .Value!;
    }

    public static string ResolveConfigIni() => Path.Combine(_repoRoot, "config.ini");

    public static string ResolveEnvFile() => Path.Combine(_repoRoot, ".env");

    public static string ResolveDatabasePath() => Path.Combine(_repoRoot, "data", "homestoq.db");

    public static string ResolveFutureConfigPath() => Path.Combine(_repoRoot, "data", "config.ini.future");
}
