#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FishSocialOverlay;

public static class OverlayStaticPerformanceTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    static void Wait(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready()) { if (watch.ElapsedMilliseconds > 15000) throw new Exception("Static-art timeout"); Pump(); Thread.Sleep(5); }
        Pump();
    }
    static BitmapSource Pattern(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        { pixels[i*4] = 80; pixels[i*4+1] = 120; pixels[i*4+2] = 180; pixels[i*4+3] = (byte)(i%256); }
        var result = BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);
        result.Freeze(); return result;
    }
    static string Save(BitmapSource image)
    {
        var path = Path.Combine(Path.GetTempPath(), "overlay-static-"+Guid.NewGuid().ToString("N")+".png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (var stream = File.Create(path)) encoder.Save(stream);
        return path;
    }
    public static void AlphaExtraction()
    {
        var path = Save(Pattern(17,67));
        try
        {
            var task = OverlayStaticArt.LoadAsync(path,true,CancellationToken.None);
            Wait(()=>task.IsCompleted); var image = task.GetAwaiter().GetResult();
            Check(image.Bitmap.IsFrozen && image.Alpha.Bytes == 17*67, "result not frozen or full BGRA retained");
            for (var y=0;y<67;y++) for(var x=0;x<17;x++)
                Check(image.Alpha.Sample(x+.5,y+.5,17,67)==(byte)((y*17+x)%256), "stripe extraction altered alpha");
            Check(OverlayStaticArt.TryGetAlpha(image.Bitmap,out var alpha) && ReferenceEquals(alpha,image.Alpha), "display source lacks prepared alpha");
        }
        finally { File.Delete(path); }
    }
    public static void SharedStaticLoads()
    {
        var path = Save(Pattern(256,256));
        try
        {
            using(var cancel = new CancellationTokenSource())
            {
                var start = OverlayStaticArt.DecodeCount;
                var leaving = OverlayStaticArt.LoadAsync(path,true,cancel.Token);
                var staying = OverlayStaticArt.LoadAsync(path,true,CancellationToken.None);
                cancel.Cancel(); Wait(()=>leaving.IsCompleted && staying.IsCompleted);
                try { leaving.GetAwaiter().GetResult(); } catch(OperationCanceledException) { }
                var image = staying.GetAwaiter().GetResult();
                Check(image.Bitmap != null && OverlayStaticArt.DecodeCount == start+1, "shared decoder canceled or repeated");
                var warm = OverlayStaticArt.LoadAsync(path,true,CancellationToken.None);
                Check(warm.IsCompleted && ReferenceEquals(warm.Result.Bitmap,image.Bitmap), "warm load touched disk again");
            }
        }
        finally { File.Delete(path); }
    }
    public static void StaticCacheBudget()
    {
        var paths = new System.Collections.Generic.List<string>();
        BitmapSource live = null;
        try
        {
            var pattern = Pattern(1024,1024);
            long produced = 0;
            for (var i=0;i<64 && produced<=64L*1024*1024;i++)
            {
                var path = Save(pattern); paths.Add(path);
                var task = OverlayStaticArt.LoadAsync(path,true,CancellationToken.None);
                Wait(()=>task.IsCompleted);
                var result = task.GetAwaiter().GetResult();
                Check(result.Bitmap != null, "static budget fixture failed");
                produced += result.Bytes;
                if (i==0) live = result.Bitmap;
            }
            Check(produced>48L*1024*1024, "fixture did not create enough decoded pressure");
            Check(OverlayStaticArt.CachedBytes<=48L*1024*1024, "static cache exceeded budget");
            Check(live.PixelWidth==1024 && live.IsFrozen, "eviction invalidated displayed bitmap");
            var before = OverlayStaticArt.DecodeCount;
            var reload = OverlayStaticArt.LoadAsync(paths[0],true,CancellationToken.None);
            Wait(()=>reload.IsCompleted); reload.GetAwaiter().GetResult();
            Check(OverlayStaticArt.DecodeCount==before+1, "oldest unreferenced static image was not evicted");
        }
        finally { foreach(var path in paths) File.Delete(path); }
    }

    public static void SceneCancellation()
    {
        var actors=new Canvas(); var background=new Image();
        var scene = new PondScenePresenter(new Canvas(),actors,new Canvas(),new Canvas(),background,
            new System.Windows.Shapes.Rectangle(),new System.Windows.Shapes.Rectangle(),new System.Windows.Shapes.Rectangle(),null);
        Check(background.Source == null && !scene.BackgroundPending && !scene.IsPresentationReady, "startup displayed a default pond before the first snapshot");
        scene.Apply(new IpcMessage { PondId="pond-calm",OwnPetId="orange",PetVisualState="sit",Users=Array.Empty<OverlayUserDto>() });
        scene.Apply(new IpcMessage { PondId="pond-bamboo",OwnPetId="orange",PetVisualState="sit",Users=Array.Empty<OverlayUserDto>() });
        if (scene.BackgroundPending) Check(!scene.IsPresentationReady, "scene became visible before target art was ready");
        Wait(()=>scene.IsPresentationReady);
        var latest = background.Source;
        Check(latest != null, "latest pond fixture did not load");
        Wait(()=>OverlayStaticArt.PendingCount==0);
        Check(ReferenceEquals(latest,background.Source), "late pond overwrote current background");
        scene.Apply(new IpcMessage { PondId="pond-calm",OwnPetId="orange",PetVisualState="sit",Users=Array.Empty<OverlayUserDto>() });
        scene.Dispose(); Wait(()=>OverlayStaticArt.PendingCount==0);
        Check(background.Source==null && actors.Children.Count==0, "late static load revived disposed scene");
    }
    public static void DecorationGeometry()
    {
        var path = Save(Pattern(4,4));
        try
        {
            var layout=new OverlayPondLayout();
            var dto=new OverlayLayoutObjectDto { sprite=path,w=100,h=60,x=200,y=150,z=7,anchor="center" };
            var task=layout.CreateSpriteImageAsync(dto,Path.GetDirectoryName(path),CancellationToken.None);
            Wait(()=>task.IsCompleted); var image=task.GetAwaiter().GetResult();
            Check(image.Source!=null && image.Width==100 && image.Height==60, "decor size changed");
            Check(Canvas.GetLeft(image)==150 && Canvas.GetTop(image)==120 && Panel.GetZIndex(image)==7, "decor anchor/z-order changed");
            Check(!image.IsHitTestVisible, "decor captures input");
        }
        finally { File.Delete(path); }
    }
    public static void NineSliceBudget()
    {
        var source=Pattern(64,64); var border=new[]{12,12,12,12};
        var live=OverlayNineSlice.TryGetBrush(source,border,80,40);
        Check(live!=null && ReferenceEquals(live,OverlayNineSlice.TryGetBrush(source,border,80,40)), "warm brush recreated");
        for(var i=0;i<160;i++)
            Check(OverlayNineSlice.TryGetBrush(source,border,320+i,140)!=null, "nine-slice render failed");
        Check(OverlayNineSlice.CachedBytes<=16L*1024*1024, "nine-slice cache unbounded");
        Check(live.ImageSource!=null && live.IsFrozen, "eviction broke a live brush");
        var other=Pattern(64,64);
        Check(!ReferenceEquals(OverlayNineSlice.TryRender(source,border,81,41),OverlayNineSlice.TryRender(other,border,81,41)), "source identities collided");
    }
    static Border Surface(out Border horizontal)
    {
        horizontal=new Border { Width=144,Height=96,Child=new Border { Background=Brushes.White } };
        return new Border { Width=144,Height=96,Child=horizontal };
    }
    static byte[] Render(Border host)
    {
        host.Measure(new Size(144,96)); host.Arrange(new Rect(0,0,144,96)); host.UpdateLayout();
        var rtb=new RenderTargetBitmap(144,96,96,96,PixelFormats.Pbgra32); rtb.Render(host);
        var pixels=new byte[144*96*4]; rtb.CopyPixels(pixels,144*4,0); return pixels;
    }
    public static void MaskParity()
    {
        var dual=Surface(out var dualInner); var combined=Surface(out var combinedInner);
        using(var a=new OverlayEdgeVignette(dual,dualInner,false))
        using(var b=new OverlayEdgeVignette(combined,combinedInner,true))
        {
            a.ApplySize(144,96); var original=a.Mode;
            var mask=dual.OpacityMask; a.ApplySize(144,96);
            Check(ReferenceEquals(mask,dual.OpacityMask), "same size rebuilt masks");
            b.ApplySize(144,96); Wait(()=>b.Mode=="combined");
            Check(dual.OpacityMask!=null && dualInner.OpacityMask!=null && combined.OpacityMask==null, "A/B modes not isolated");
            var before=Render(dual); var after=Render(combined);
            for(var i=3;i<before.Length;i+=4)
                Check(Math.Abs(before[i]-after[i])<=2, "combined mask changed visible edge opacity");
        }
    }
}
#endif
