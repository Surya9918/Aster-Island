namespace Aster.Core;
/// <summary>Undo only fields changed by one edit, preserving newer permissions and unrelated choices.</summary>
public static class PreferenceEdits
{
 public static Preferences Undo(Preferences current,Preferences before,Preferences after)
 {
  var result=current with{};foreach(var property in typeof(Preferences).GetProperties())
   if(property.CanRead&&property.CanWrite&&!Equals(property.GetValue(before),property.GetValue(after)))property.SetValue(result,property.GetValue(before));
  return result.Validated();
 }
}
