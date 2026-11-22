namespace Aster.Infrastructure;

public sealed class Diagnostics(string root)
{
    private readonly object gate = new();
    public void Record(string code)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z_]{3,48}$")) return;
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, "diagnostics.log");
                if (File.Exists(path) && new FileInfo(path).Length > 64_000) File.WriteAllText(path, "");
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {code}{Environment.NewLine}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
