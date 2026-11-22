using System.Runtime.InteropServices;
namespace Aster.Platform.Windows;
/// <summary>One untyped COM activation path for both endpoint discovery and volume control.
/// Avoids conflicting managed coclass wrappers for the same Windows CLSID.</summary>
internal static class CoreAudioFactory
{
 private static readonly Guid EnumeratorId=new("BCDE0395-E52F-467C-8E3D-C4579291692E");
 public static object CreateEnumerator()=>Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeratorId,true)!)!;
}
