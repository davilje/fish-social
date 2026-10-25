#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FishSocialOverlay;

static class OverlaySceneMotionNativeTests
{
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static T Await<T>(Task<T> task)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(5)};
        var start=DateTime.UtcNow;
        timer.Tick+=(s,e)=>{if(task.IsCompleted || (DateTime.UtcNow-start).TotalSeconds>10)frame.Continue=false;};
        timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
        Check(task.IsCompleted,"Scene motion load timed out");return task.GetAwaiter().GetResult();
    }
    public static void LifecycleAndGeometry()
    {
        var root=Path.Combine(Path.GetTempPath(),"scene-motion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        OverlaySceneMotion motion=null;
        try
        {
            const int w=32,h=16;var bytes=new byte[w*h*4];for(var i=0;i<bytes.Length;i++)bytes[i]=255;
            var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,bytes,w*4);bitmap.Freeze();
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(root,"weight.png")))encoder.Save(stream);
            File.WriteAllText(Path.Combine(root,"motion.json"),"{\"version\":1,\"width\":32,\"height\":16,\"weightTexture\":\"weight.png\",\"cloudId\":\"cloud\",\"cloudAmplitude\":12,\"cloudPeriod\":160,\"leafAmplitude\":2.2,\"verticalRatio\":0.26,\"primaryPeriod\":7.5,\"secondaryPeriod\":4.2,\"verticalPeriod\":9.3,\"leaves\":[{\"id\":\"tree\",\"left\":0,\"width\":32,\"height\":8,\"phase\":0}]}");
            var cloud=new Image{Source=bitmap,Width=64,Height=32,IsHitTestVisible=false};
            var tree=new Image{Source=bitmap,Width=64,Height=32,IsHitTestVisible=false};Canvas.SetLeft(tree,17);Canvas.SetTop(tree,23);Panel.SetZIndex(tree,4);
            var images=new Dictionary<string,Image>{{"cloud",cloud},{"tree",tree}};
            motion=Await(OverlaySceneMotion.TryCreateAsync(root,"sky.png",images,CancellationToken.None));
            Check(motion!=null&&!motion.IsRunning,"Should not run before scene publishes");
            var group=motion.Element("tree",tree) as Canvas;
            Check(group!=null&&group.Width==64&&group.Height==32&&!group.IsHitTestVisible,"Motion intercepts input or changes design size");
            Check(Canvas.GetLeft(group)==17&&Canvas.GetTop(group)==23&&Panel.GetZIndex(group)==4,"Changed scene geometry/z");
            Check(group.Children.Count==2,"Expected fixed root and moving crown");
            var fixedRoot=(Image)group.Children[0];var clip=(RectangleGeometry)fixedRoot.Clip;
            Check(clip.Rect.Top==16&&clip.Rect.Height==16,"Fixed root clip ignores display scale");
            motion.SetVisible(true);Check(motion.IsRunning,"Clock did not start");
            motion.SetVisible(false);Check(!motion.IsRunning,"Hidden scene keeps updating");
            motion.SetVisible(true);motion.Dispose();Check(motion.IsDisposed&&!motion.IsRunning,"Disposed scene keeps updating");
            motion.SetVisible(true);Check(!motion.IsRunning,"Disposed scene restarted");
            using(var canceled=new CancellationTokenSource())
            {canceled.Cancel();var rejected=false;try{Await(OverlaySceneMotion.TryCreateAsync(root,"sky.png",images,canceled.Token));}catch(OperationCanceledException){rejected=true;}Check(rejected,"Canceled scene load was applied");}
            File.Delete(Path.Combine(root,"motion.json"));
            Check(Await(OverlaySceneMotion.TryCreateAsync(root,"sky.png",images,CancellationToken.None))==null,"Static ponds acquired a clock");
        }
        finally{motion?.Dispose();Directory.Delete(root,true);}
    }
}
#endif
