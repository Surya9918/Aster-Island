namespace Aster.Core;

public sealed record IslandNotification(string Key,string AppId,string AppName,string Title,string Body,DateTimeOffset Created,bool External=false)
{
 public string Initial=>string.IsNullOrWhiteSpace(AppName)?"•":AppName[..1].ToUpperInvariant();
 public string Time=>Created.ToLocalTime().ToString("HH:mm");
 public IslandNotification Bounded()=>this with{Key=Cut(Key,512),AppId=Cut(AppId,256),AppName=Cut(AppName,100),Title=Cut(Title,250),Body=Cut(Body,2000)};
 private static string Cut(string s,int max)=>new((s??"").Where(c=>!char.IsControl(c)||c=='\n').Take(max).ToArray());
}
/// <summary>Session-only, bounded inbox. Hiding an entry never deletes a Windows notification.</summary>
public sealed class NotificationInbox
{
 private readonly List<IslandNotification> local=[];
 private readonly HashSet<string> hidden=[];
 private readonly Queue<string> hiddenOrder=[];
 private List<IslandNotification> external=[];
 private bool seeded;
 private DateTimeOffset newest=DateTimeOffset.MinValue;
 public IReadOnlyList<IslandNotification> Items=>local.Concat(external).Where(n=>!hidden.Contains(n.Key)).OrderByDescending(n=>n.Created).ThenBy(n=>n.Key,StringComparer.Ordinal).Take(100).ToArray();
 public IReadOnlyList<IslandNotification> Sync(IEnumerable<IslandNotification> snapshot)
 {
  var old=external.Select(n=>n.Key).ToHashSet();
  external=snapshot.Where(n=>n.External).Select(n=>n.Bounded()).Where(n=>n.Key.Length>0).DistinctBy(n=>n.Key).OrderByDescending(n=>n.Created).Take(100).ToList();
  var arrivals=seeded?external.Where(n=>n.Created>=newest&&!old.Contains(n.Key)&&!hidden.Contains(n.Key)).ToArray():[];
  if(external.Count>0)newest=newest>external.Max(n=>n.Created)?newest:external.Max(n=>n.Created);
  seeded=true;return arrivals;
 }
 public void AddLocal(string title,string body,DateTimeOffset time){local.Insert(0,new IslandNotification("local:"+Guid.NewGuid(),"aster","Aster",title,body,time).Bounded());if(local.Count>100)local.RemoveAt(local.Count-1);}
 public void Hide(string key){local.RemoveAll(n=>n.Key==key);if(hidden.Add(key)){hiddenOrder.Enqueue(key);while(hiddenOrder.Count>2048)hidden.Remove(hiddenOrder.Dequeue());}}
 public void ClearVisible(){foreach(var n in Items)Hide(n.Key);}
 public void Disconnect(){external.Clear();hidden.Clear();hiddenOrder.Clear();seeded=false;newest=DateTimeOffset.MinValue;}
 public void ClearAll(){local.Clear();Disconnect();}
}

