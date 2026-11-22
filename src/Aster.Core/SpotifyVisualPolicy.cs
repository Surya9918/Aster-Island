namespace Aster.Core;
public static class SpotifyVisualPolicy
{
    public static bool IsSpotify(string? source)=>source is not null &&
        (source.Equals("Spotify.exe",StringComparison.OrdinalIgnoreCase)||source.Equals("Spotify",StringComparison.OrdinalIgnoreCase)||
        (source.StartsWith("SpotifyAB.SpotifyMusic_",StringComparison.OrdinalIgnoreCase)&&source.EndsWith("!Spotify",StringComparison.OrdinalIgnoreCase))||
        System.Text.RegularExpressions.Regex.IsMatch(source,@"^open\.spotify\.com-[A-Za-z0-9]+_[A-Za-z0-9]+!App$",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant));
}

/// <summary>Bounded cover-art atmosphere, generated once per cover change. BGRA pixels, no external assets.</summary>
public static class AtmosphereRenderer
{
    public static byte[] Render(byte[] source,int sourceWidth,int sourceHeight,int width,int height,CancellationToken cancel=default)
    {
        if(sourceWidth<1||sourceHeight<1||sourceWidth>256||sourceHeight>256||source.Length!=sourceWidth*sourceHeight*4||width<64||height<64||width>2560||height>1600)throw new ArgumentOutOfRangeException(nameof(source));
        const int n=64;var small=new float[n*n*3];var blurred=new float[small.Length];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)for(int c=0;c<3;c++)small[(y*n+x)*3+c]=source[((y*sourceHeight/n)*sourceWidth+x*sourceWidth/n)*4+c];
        // Three separable box passes approximate a Gaussian; mirrored/clamped edges avoid dark seams.
        const int radius=9;
        for(int pass=0;pass<3;pass++)
        {
            cancel.ThrowIfCancellationRequested();
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)for(int c=0;c<3;c++){float sum=0;for(int k=-radius;k<=radius;k++)sum+=small[(y*n+Math.Clamp(x+k,0,n-1))*3+c];blurred[(y*n+x)*3+c]=sum/(radius*2+1);}
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)for(int c=0;c<3;c++){float sum=0;for(int k=-radius;k<=radius;k++)sum+=blurred[(Math.Clamp(y+k,0,n-1)*n+x)*3+c];small[(y*n+x)*3+c]=sum/(radius*2+1);}
        }
        var pixels=new byte[width*height*4];
        var aspect=(double)width/height;var coverAspect=(double)sourceWidth/sourceHeight;
        for(int y=0;y<height;y++)
        {
            if((y&31)==0)cancel.ThrowIfCancellationRequested();
            var v=(double)y/(height-1);var sy=Math.Clamp((aspect>coverAspect?(v-.5)*coverAspect/aspect+.5:v)*63,0,63);var y0=(int)sy;var y1=Math.Min(63,y0+1);var fy=sy-y0;
            for(int x=0;x<width;x++)
            {
                var u=(double)x/(width-1);var sx=Math.Clamp((aspect<coverAspect?(u-.5)*aspect/coverAspect+.5:u)*63,0,63);var x0=(int)sx;var x1=Math.Min(63,x0+1);var fx=sx-x0;
                var vignette=Math.Clamp(1-.32*((u-.5)*(u-.5)*4+(v-.42)*(v-.42)*3),.35,1);
                var light=(.46-.20*v)*vignette;var index=(y*width+x)*4;var grain=((x*17+y*31)%7-3)*.22;
                for(int c=0;c<3;c++)
                {
                    var a=small[(y0*n+x0)*3+c]*(1-fx)+small[(y0*n+x1)*3+c]*fx;
                    var b=small[(y1*n+x0)*3+c]*(1-fx)+small[(y1*n+x1)*3+c]*fx;
                    pixels[index+c]=(byte)Math.Clamp((a*(1-fy)+b*fy)*light+(c==0?9:c==1?7:5)+grain,0,255);
                }
                pixels[index+3]=255;
            }
        }
        return pixels;
    }
}
