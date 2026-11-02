namespace Anvil.Security;

public static class DotEnvLoader
{
    public static void LoadFromWorkingDirectoryHierarchy()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            var dotEnvPath = Path.Combine(directory, ".env");
            if (File.Exists(dotEnvPath))
            {
                foreach (var rawLine in File.ReadAllLines(dotEnvPath))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var key = line[..separatorIndex].Trim();
                    var value = line[(separatorIndex + 1)..].Trim();
                    Environment.SetEnvironmentVariable(key, value);
                }

                break;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }
    }
}
