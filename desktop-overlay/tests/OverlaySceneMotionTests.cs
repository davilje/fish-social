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
        // A middle-ground patch starts below the top of the source texture.
        var patch=new OverlayLeafWarp(source,weight,w,h,5,12,10,.4,7.2,.26,6.6,4.2,9.3,6);
        var patchResult=new byte[12*10*4];patch.Render(0,patchResult);
        for(var y=0;y<10;y++)for(var x=0;x<12;x++)for(var c=0;c<4;c++)
            Check(patchResult[(y*12+x)*4+c]==source[((y+6)*w+x+5)*4+c],"Middle patch sampled the wrong source row");
        patch.Render(1.8,patchResult);
        for(var y=2;y<10;y++)for(var x=0;x<12;x++)for(var c=0;c<4;c++)
            Check(patchResult[(y*12+x)*4+c]==source[((y+6)*w+x+5)*4+c],"Middle patch moved fixed roots");
        foreach(var speed in new[]{1.6,3.2,5.2})
        {
            var cycle=2172/speed;
            Check(Math.Abs(OverlaySceneMotionMath.CloudTravel(cycle,speed,2172))<1e-8,"Cloud failed to wrap");
            Check(Math.Abs(OverlaySceneMotionMath.CloudTravel(20,speed,2172)-20*speed)<1e-8,"Cloud reversed direction");
            var before=OverlaySceneMotionMath.CloudTravel(cycle-.001,speed,2172);
            var after=OverlaySceneMotionMath.CloudTravel(cycle+.001,speed,2172);
            Check(Math.Abs((after+2172-before)-.002*speed)<1e-8,"Cloud copies are discontinuous at wrap");
        }
        // At a sloped wrap, the preceding image becomes the main image at the same X and Y.
        foreach(var slope in new[]{-.05,0,.05})foreach(var speed in new[]{.025,.22,.8})
        {
            const double phase=100,epsilon=.001;
            var boundary=(2172-phase)/speed;
            OverlaySceneMotionMath.CloudPlacement(boundary-epsilon,speed,2172,phase,slope,-1,out var bx,out var by);
            OverlaySceneMotionMath.CloudPlacement(boundary+epsilon,speed,2172,phase,slope,0,out var ax,out var ay);
            Check(Math.Abs(ax-bx-2*epsilon*speed)<1e-8,"Sloped cloud jumped horizontally");
            Check(Math.Abs(ay-by-2*epsilon*speed*slope)<1e-8,"Sloped cloud jumped vertically");
            OverlaySceneMotionMath.CloudPlacement(60,speed,2172,0,slope,0,out ax,out ay);
            OverlaySceneMotionMath.CloudPlacement(60,speed,2172,0,slope,-1,out bx,out by);
            Check(Math.Abs(ax-bx-2172)<1e-8 && Math.Abs(ay-by-2172*slope)<1e-8,"Cloud copies follow different paths");
            Check(slope!=0 || (ay==0 && by==0),"Legacy horizontal cloud moved vertically");
        }
        var invalidSlope=false;
        try{OverlaySceneMotionMath.CloudPlacement(0,1,2172,0,double.NaN,0,out _,out _);}catch(ArgumentException){invalidSlope=true;}
        Check(invalidSlope,"Invalid sloped cloud accepted");
        // Independent clouds can travel in all four diagonal directions. Broad clouds stay horizontal.
        foreach(var vx in new[]{-1.2,1.2})foreach(var vy in new[]{-.5,0,.5})
        {
            OverlaySceneMotionMath.CloudSpritePlacement(0,vx,vy,2172,724,200,80,500,200,out var x0,out var y0);
            OverlaySceneMotionMath.CloudSpritePlacement(60,vx,vy,2172,724,200,80,500,200,out var x1,out var y1);
            Check(Math.Abs(x1-x0-vx*60)<1e-8 && Math.Abs(y1-y0-vy*60)<1e-8,"Independent cloud direction changed");
            Check(vy!=0 || y1==y0,"Large horizontal cloud moved vertically");
            foreach(var axis in new[]{0,1})
            {
                var v=axis==0?vx:vy;if(v==0)continue;
                double extent=axis==0?2172:724,size=axis==0?200:80,initial=axis==0?500:200;
                var boundary=(v>0?extent+2-initial:-size-2-initial)/v;
                OverlaySceneMotionMath.CloudSpritePlacement(boundary-.001,vx,vy,2172,724,200,80,500,200,out var bx,out var by);
                OverlaySceneMotionMath.CloudSpritePlacement(boundary+.001,vx,vy,2172,724,200,80,500,200,out var ax,out var ay);
                var before=axis==0?bx:by;var after=axis==0?ax:ay;
                Check((before+size<0||before>extent)&&(after+size<0||after>extent),"Independent cloud wrapped while visible");
            }
        }
        Console.WriteLine("PASS: independent cloud directions; stationary horizontal height; both-axis resets fully outside viewport");
        Console.WriteLine("PASS: slow/sloped cloud copies remain continuous in both axes, including legacy horizontal motion");
        Console.WriteLine("PASS: legacy motion; offset middle patches; fixed roots; transparent edges; three forward cloud speeds and wrap continuity");
        if(args.Length==2 && args[0]=="--scene")
        {
            var dir=args[1];using var manifest=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"motion.json")));
            var settings=manifest.RootElement;var warps=new System.Collections.Generic.List<OverlayLeafWarp>();
            var outputs=new System.Collections.Generic.List<byte[]>();
            foreach(var leaf in settings.GetProperty("leaves").EnumerateArray())
            {
                var id=leaf.GetProperty("id").GetString();var weightName=leaf.TryGetProperty("weightTexture",out var wn)?wn.GetString():settings.GetProperty("weightTexture").GetString();
                var primary=leaf.TryGetProperty("primaryPeriod",out var pp)?pp.GetDouble():settings.GetProperty("primaryPeriod").GetDouble();
                var real=new OverlayLeafWarp(File.ReadAllBytes(Path.Combine(dir,id+".pbgra")),File.ReadAllBytes(Path.Combine(dir,weightName+".gray")),2172,724,
                    leaf.GetProperty("left").GetInt32(),leaf.GetProperty("width").GetInt32(),leaf.GetProperty("height").GetInt32(),leaf.GetProperty("phase").GetDouble(),7.2,.26,primary,4.2,9.3,leaf.GetProperty("top").GetInt32());
                warps.Add(real);outputs.Add(new byte[real.Width*real.Height*4]);
            }
            var timer=Stopwatch.StartNew();for(var frame=0;frame<100;frame++)for(var i=0;i<warps.Count;i++)warps[i].Render(frame/24.0,outputs[i]);timer.Stop();
            for(var i=0;i<warps.Count;i++){warps[i].Render(1.8,outputs[i]);File.WriteAllBytes(Path.Combine(dir,"patch-"+i+".pbgra"),outputs[i]);}
            Console.WriteLine(warps.Count+" real foliage patches: "+(timer.Elapsed.TotalMilliseconds/100).ToString("F2")+" ms/frame (sampling only, reused buffers)");
        }
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
