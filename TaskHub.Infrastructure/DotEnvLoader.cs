namespace TaskHub.Infrastructure;

// Reads a ".env" file (one NAME=value per line) and turns each line into an
// environment variable. Real environment variables always win over the file,
// so a production server can override anything without touching the file.
public static class DotEnvLoader
{
    public static void Load()
    {
        // Look for the file in the current folder and then in every folder above it.
        // The second search starts from where the program itself lives, so it also
        // works when you start the program from a different folder.
        string? path = FindFile(Directory.GetCurrentDirectory()) ?? FindFile(AppContext.BaseDirectory);

        if (path is null)
            return;   // no .env file: fine, the settings may come from the real environment

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();

            // Skip blank lines and comments (lines starting with #)
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            // Split at the FIRST equals sign only, because values may contain more of them
            int equals = line.IndexOf('=');
            if (equals <= 0)
                continue;

            string key = line[..equals].Trim();
            string value = line[(equals + 1)..].Trim();

            // Allow values wrapped in quotes: KEY="some value"
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // A real environment variable beats the file
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindFile(string startFolder)
    {
        var folder = new DirectoryInfo(startFolder);

        while (folder is not null)
        {
            string candidate = Path.Combine(folder.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;

            folder = folder.Parent;   // go up one folder and look again
        }

        return null;
    }
}