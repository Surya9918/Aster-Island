namespace Aster.Core;

public sealed record ClipboardEntry(Guid Id, string Text, DateTimeOffset Created);
public sealed class ClipboardHistory(IClock clock)
{
    private readonly List<ClipboardEntry> items = [];
    public bool Enabled { get; private set; }
    public IReadOnlyList<ClipboardEntry> Items { get { Expire(); return items.ToArray(); } }
    public void SetEnabled(bool enabled) { Enabled = enabled; if (!enabled) Clear(); }
    public void Add(string text)
    {
        Expire();
        if (!Enabled || string.IsNullOrWhiteSpace(text) || text.Length > 8192) return;
        items.RemoveAll(i => i.Text == text);
        items.Insert(0, new(Guid.NewGuid(), text, clock.UtcNow));
        if (items.Count > 20) items.RemoveRange(20, items.Count - 20);
    }
    public void Remove(Guid id) => items.RemoveAll(i => i.Id == id);
    public void Clear() => items.Clear();
    private void Expire() => items.RemoveAll(i => clock.UtcNow - i.Created >= TimeSpan.FromMinutes(15));
}
