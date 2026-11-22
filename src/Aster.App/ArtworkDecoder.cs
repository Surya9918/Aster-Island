using System;
using System.IO;
using System.Windows.Media.Imaging;
namespace Aster.App;
internal static class ArtworkDecoder
{
    public static BitmapSource? Decode(byte[]? bytes)
    {
        if(bytes is null||bytes.Length==0||bytes.Length>4*1024*1024)return null;
        try
        {
            using var stream=new MemoryStream(bytes,false);
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
            image.DecodePixelWidth=256;image.DecodePixelHeight=256;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
        }
        catch{return null;}
    }
}
