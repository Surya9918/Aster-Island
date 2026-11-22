using System.Text.Json;

namespace Aster.Infrastructure;

public sealed class LocalStore
{
    public string Root { get; }
    private bool suppressed;
    public LocalStore(string root) => Root = Path.GetFullPath(root);
    public T Load<T>(string name, T fallback)
    {
        foreach (var suffix in new[] { "", ".bak" })
            try
            {
                var file = FileFor(name) + suffix;
                if (File.Exists(file) && new FileInfo(file).Length <= 2_000_000)
                    return JsonSerializer.Deserialize<T>(File.ReadAllText(file)) ?? fallback;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return fallback;
    }
    public void Save<T>(string name, T value)
    {
        if (suppressed) return;
        Directory.CreateDirectory(Root);
        RejectLinks();
        var file = FileFor(name);
        var temp = file + ".tmp";
        var content = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { WriteIndented = true });
        if (content.Length > 2_000_000) throw new InvalidDataException("Local data limit reached.");
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(content); stream.Flush(true); }
        if (File.Exists(file)) File.Replace(temp, file, file + ".bak");
        else File.Move(temp, file);
    }
    public void DeleteAll()
    {
        if (Directory.Exists(Root))
        {
            RejectLinks();
            Directory.Delete(Root, true);
        }
        suppressed = true;
    }
    private string FileFor(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z-]+\\.json$"))
            throw new ArgumentException("Invalid storage filename.", nameof(name));
        return Path.Combine(Root, name);
    }
    private void RejectLinks()
    {
        var directory = new DirectoryInfo(Root);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked data folder is not supported.");
        foreach (var entry in directory.EnumerateFileSystemInfos())
            if ((entry.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new IOException("Unexpected nested or linked data entry.");
    }
}
