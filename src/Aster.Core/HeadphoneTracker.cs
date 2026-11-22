namespace Aster.Core;
public sealed record OutputEndpoint(string Id,string Name,bool Active,bool Headphone,bool Default);
public sealed record HeadphoneEvent(string Title,string Detail);
/// <summary>Quiet initial snapshot, one card per connection transition, explicit routing changes.</summary>
public sealed class HeadphoneTracker
{
 private Dictionary<string,OutputEndpoint>? previous;
 public IReadOnlyList<HeadphoneEvent> Update(IEnumerable<OutputEndpoint> endpoints)
 {
  var now=endpoints.DistinctBy(e=>e.Id).ToDictionary(e=>e.Id);var events=new List<HeadphoneEvent>();
  if(previous is not null){foreach(var e in now.Values.Where(e=>e.Headphone&&e.Active))if(!previous.TryGetValue(e.Id,out var old)||!old.Active)events.Add(new("Headphones connected",e.Name+(e.Default?" · current output":" · available in Windows sound settings")));
   foreach(var old in previous.Values.Where(e=>e.Headphone&&e.Active))if(!now.TryGetValue(old.Id,out var e)||!e.Active)events.Add(new("Headphones disconnected",old.Name));
   var selected=now.Values.FirstOrDefault(e=>e.Headphone&&e.Active&&e.Default);var before=previous.Values.FirstOrDefault(e=>e.Default);
   if(selected is not null&&selected.Id!=before?.Id&&previous.TryGetValue(selected.Id,out var connected)&&connected.Active)events.Add(new("Headphone output selected",selected.Name));}
  previous=now;return events.DistinctBy(e=>(e.Title,e.Detail)).Take(4).ToArray();
 }
}
public static class NightMotion
{
 public static SpringConfiguration Adapt(SpringConfiguration c,bool night){c=c.Validated();if(!night)return c;double k=c.Stiffness*.72;double z=c.Damping/(2*Math.Sqrt(c.Stiffness*c.Mass));return new SpringConfiguration(k,2*Math.Clamp(z*1.08,.95,1.3)*Math.Sqrt(k*c.Mass),c.Mass).Validated();}
}

