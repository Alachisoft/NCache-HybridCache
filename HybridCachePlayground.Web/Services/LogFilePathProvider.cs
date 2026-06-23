namespace HybridCachePlayground.Web.Services;

/// <summary>
/// Singleton that holds the path to the current startup log file.
/// Registered in Program.cs after the startup timestamp is known.
/// </summary>
public sealed class LogFilePathProvider
{
    /// <summary>
    /// Full path to the current session's log file.
    /// </summary>
    public string CurrentLogPath { get; }

    /// <summary>
    /// Directory containing all log files.
    /// </summary>
    public string LogsDirectory { get; }

    /// <summary>
    /// Just the filename (without directory).
    /// </summary>
    public string FileName => Path.GetFileName(CurrentLogPath);

    public LogFilePathProvider(string logPath)
    {
        CurrentLogPath = logPath;
        LogsDirectory = Path.GetDirectoryName(logPath) ?? "logs";
    }

    /// <summary>
    /// Reads the current log file contents.
    /// </summary>
    public string ReadLogContent()
    {
        if (!File.Exists(CurrentLogPath))
            return "[Log file not yet created]";

        try
        {
            // Use FileShare.ReadWrite to read while Serilog is still writing
            using var fs = new FileStream(CurrentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            return $"[Error reading log file: {ex.Message}]";
        }
    }

    /// <summary>
    /// Reads the last N lines from the log file.
    /// </summary>
    public string ReadLastLines(int lineCount = 100)
    {
        if (!File.Exists(CurrentLogPath))
            return "[Log file not yet created]";

        try
        {
            using var fs = new FileStream(CurrentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var lines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }

            var result = lines.Skip(Math.Max(0, lines.Count - lineCount)).ToList();
            return string.Join(Environment.NewLine, result);
        }
        catch (Exception ex)
        {
            return $"[Error reading log file: {ex.Message}]";
        }
    }
}
