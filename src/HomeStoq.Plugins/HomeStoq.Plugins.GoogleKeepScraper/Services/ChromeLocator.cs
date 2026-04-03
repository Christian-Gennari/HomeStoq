namespace HomeStoq.Plugins.GoogleKeepScraper.Services;

public static class ChromeLocator
{
    public static string? FindChrome()
    {
        if (OperatingSystem.IsWindows())
        {
            return FindChromeWindows();
        }

        if (OperatingSystem.IsMacOS())
        {
            return FindChromeMacOS();
        }

        if (OperatingSystem.IsLinux())
        {
            return FindChromeLinux();
        }

        return null;
    }

    private static string? FindChromeWindows()
    {
        var paths = new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Google\Chrome\Application\chrome.exe"),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string? FindChromeMacOS()
    {
        var paths = new[]
        {
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            "/Applications/Chromium.app/Contents/MacOS/Chromium",
        };

        foreach (var path in paths)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            if (File.Exists(expandedPath))
                return expandedPath;
        }

        return null;
    }

    private static string? FindChromeLinux()
    {
        // Try common binary paths
        var paths = new[]
        {
            "/usr/bin/google-chrome",
            "/usr/bin/google-chrome-stable",
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
            "/snap/bin/google-chrome",
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find via which command
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "google-chrome",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                return output;
        }
        catch { /* Ignore which command failures */ }

        return null;
    }

    public static int FindAvailablePort(int startPort = 9222, int maxAttempts = 100)
    {
        for (int port = startPort; port < startPort + maxAttempts; port++)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch { /* Port in use */ }
        }

        throw new InvalidOperationException($"No available ports found between {startPort} and {startPort + maxAttempts - 1}");
    }
}
