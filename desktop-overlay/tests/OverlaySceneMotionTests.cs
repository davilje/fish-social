#if SCENE_MOTION_TESTS
using System;
using System.Diagnostics;
using System.IO;
using FishSocialOverlay;

static class OverlaySceneMotionTests
{
    static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static OverlayLeafWarp Warp(byte[] source, byte[] weight, int w, int h, int left, int cw, int ch, double phase=0)
        => new OverlayLeafWarp(source,weight,w,h,left,cw,ch,phase,2.2,.26,7.5,4.2,9.3);
    static void Main(string[] args)
    {
        const int w=32,h=16;var source=new byte[w*h*4];var weight=new byte[w*h];
        for(var y=0;y<h;y++)for(var x=0;x<w;x++){var i=(y*w+x)*4;source[i]=(byte)(x*6);source[i+1]=(byte)(y*12);source[i+2]=100;source[i+3]=255;if(y<8)weight[y*w+x]=255;}
        var warp=Warp(source,weight,w,h,4,24,16);var result=new byte[24*16*4];warp.Render(0,result);
        for(var y=0;y<h;y++)for(var x=0;x<24;x++)for(var c=0;c<4;c++)Check(result[(y*24+x)*4+c]==source[(y*w+x+4)*4+c],"Rest crop is not exact");
        warp.Render(1.8,result);var moved=0;for(var y=0;y<h;y++)for(var x=0;x<24;x++)for(var c=0;c<4;c++){var changed=result[(y*24+x)*4+c]!=source[(y*w+x+4)*4+c];if(y>=8)Check(!changed,"Fixed trunk moved");else if(changed)moved++;}Check(moved>0,"No leaf motion");
        var zero=new byte[w*h];Warp(source,zero,w,h,0,w,h).Render(1e6,source=(byte[])source.Clone());
        var transparent=new byte[w*h*4];var transparentResult=new byte[w*h*4];Warp(transparent,weight,w,h,0,w,h).Render(4,transparentResult);foreach(var b in transparentResult)Check(b==0,"Transparent edge got a color");
        var scale=1680.0/2172;Check(Math.Abs(OverlaySceneMotionMath.CloudOffset(40,12,160,scale)-12*scale)<1e-10,"Cloud design scale");
        Check(Math.Abs(OverlaySceneMotionMath.CloudOffset(0,12,160,1)-OverlaySceneMotionMath.CloudOffset(160,12,160,1))<1e-10,"Cloud loop jumps");
        Check(Math.Abs(OverlaySceneMotionMath.CloudOffset(120,12,160,1)+12)<1e-10,"Cloud negative excursion");
        var invalid=false;try{Warp(transparent,weight,w,h,30,10,16);}catch(ArgumentException){invalid=true;}Check(invalid,"Invalid crop accepted");
        Console.WriteLine("PASS: rest crop, moving leaves, fixed roots, transparent edges, scale, cloud cycle, negative excursion, invalid crop");
        if(args.Length==1)
        {
            var dir=args[0];var bytes=File.ReadAllBytes(Path.Combine(dir,"tree-left.pbgra"));var map=File.ReadAllBytes(Path.Combine(dir,"wind.gray"));
            var full=Warp(bytes,map,2172,724,0,714,304);var output=new byte[714*304*4];full.Render(0,output);
            var timer=Stopwatch.StartNew();for(var i=0;i<100;i++)full.Render(i/24.0,output);timer.Stop();
            Console.WriteLine("Real left canopy: "+(timer.Elapsed.TotalMilliseconds/100).ToString("F2")+" ms/frame, reused buffer");
            full.Render(1.8,output);File.WriteAllBytes(Path.Combine(dir,"left-at-1.8.pbgra"),output);
            for(var y=300;y<304;y++)for(var x=0;x<714;x++)for(var c=0;c<4;c++)Check(output[(y*714+x)*4+c]==bytes[(y*2172+x)*4+c],"Real roots changed");
        }
    }
}
#endif
